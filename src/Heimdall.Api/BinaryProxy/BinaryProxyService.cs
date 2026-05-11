using Heimdall.Api.Audit;
using Heimdall.Core.Configuration;
using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;
using Heimdall.Ecosystems.NuGet.V3;
using Microsoft.AspNetCore.Mvc;
using Semver;

namespace Heimdall.Api.BinaryProxy;

public sealed class BinaryProxyService
{
	private const string Ecosystem = "nuget";

	private readonly IFeedConfigLookup _lookup;
	private readonly INuGetMetadataService _metadata;
	private readonly INuGetUpstreamClient _upstream;
	private readonly IUpstreamUrlResolver _urls;
	private readonly ISingleVersionGate _gate;
	private readonly TimeProvider _time;
	private readonly AuditLogger _audit;

	public BinaryProxyService(
		IFeedConfigLookup lookup,
		INuGetMetadataService metadata,
		INuGetUpstreamClient upstream,
		IUpstreamUrlResolver urls,
		ISingleVersionGate gate,
		TimeProvider time,
		AuditLogger audit)
	{
		ArgumentNullException.ThrowIfNull(lookup);
		ArgumentNullException.ThrowIfNull(metadata);
		ArgumentNullException.ThrowIfNull(upstream);
		ArgumentNullException.ThrowIfNull(urls);
		ArgumentNullException.ThrowIfNull(gate);
		ArgumentNullException.ThrowIfNull(time);
		ArgumentNullException.ThrowIfNull(audit);
		_lookup = lookup;
		_metadata = metadata;
		_upstream = upstream;
		_urls = urls;
		_gate = gate;
		_time = time;
		_audit = audit;
	}

	public async Task<IActionResult?> ProxyAsync(
		HttpContext context, string feedName, string packageId, string version, string fileName, CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (!_lookup.TryGet(Ecosystem, feedName, out var feed))
		{
			return new ObjectResult(new ProblemDetails
			{
				Status = 404,
				Title = "Unknown feed",
				Detail = $"feed '{feedName}' is not configured",
			})
			{
				StatusCode = 404,
			};
		}

		var leaf = await _metadata.GetVersionLeafAsync(feedName, packageId, version, ct).ConfigureAwait(false);
		if (leaf?.CatalogEntry is null)
		{
			return new NotFoundResult();
		}

		var entry = leaf.CatalogEntry;
		if (!SemVersion.TryParse(entry.Version, SemVersionStyles.Any, out var sv))
		{
			return new NotFoundResult();
		}

		var coords = new PackageCoordinates(Ecosystem, entry.PackageId, sv);
		var meta = new PackageVersionMetadata(coords, entry.Published, new Dictionary<string, string>());

		var verdict = _gate.Check(meta, feed, _time.GetUtcNow());
		var ip = context.Connection.RemoteIpAddress?.ToString() ?? "-";
		var ua = context.Request.Headers.UserAgent.ToString();

		if (verdict.IsDeny)
		{
			_audit.Deny(Ecosystem, feedName, entry.PackageId, entry.Version,
				verdict.Reason!.RuleName, verdict.Reason.Message, ip, ua);

			return new ObjectResult(new ProblemDetails
			{
				Status = 403,
				Title = "Package version blocked by rule",
				Detail = verdict.Reason.Message,
				Extensions =
				{
					["rule"] = verdict.Reason.RuleName,
					["package"] = entry.PackageId,
					["version"] = entry.Version,
				},
			})
			{
				StatusCode = 403,
			};
		}

		var packageBase = await _urls.GetPackageBaseAddressAsync(feed.Upstream, ct).ConfigureAwait(false);
		var upstreamUri = new Uri(
			packageBase,
			$"{entry.PackageId.ToLowerInvariant()}/{entry.Version.ToLowerInvariant()}/{fileName}");

		using var request = BuildUpstreamRequest(context.Request, upstreamUri);
		var response = await _upstream.SendBinaryAsync(request, ct).ConfigureAwait(false);

		await PipeResponseAsync(response, context, ct).ConfigureAwait(false);

		if (response.IsSuccessStatusCode)
		{
			_audit.Allow(Ecosystem, feedName, entry.PackageId, entry.Version, ip, ua);
		}

		response.Dispose();
		return null;
	}

	private static HttpRequestMessage BuildUpstreamRequest(HttpRequest source, Uri target)
	{
		var method = HttpMethods.IsHead(source.Method) ? HttpMethod.Head : HttpMethod.Get;
		var req = new HttpRequestMessage(method, target);

		foreach (var header in source.Headers)
		{
			if (HopByHopHeaders.Contains(header.Key))
			{
				continue;
			}

			req.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
		}

		req.Headers.AcceptEncoding.Clear();

		return req;
	}

	private static async Task PipeResponseAsync(
		HttpResponseMessage response, HttpContext context, CancellationToken ct)
	{
		context.Response.StatusCode = (int)response.StatusCode;

		foreach (var header in response.Headers)
		{
			if (HopByHopHeaders.Contains(header.Key))
			{
				continue;
			}
			context.Response.Headers[header.Key] = header.Value.ToArray();
		}

		foreach (var header in response.Content.Headers)
		{
			if (HopByHopHeaders.Contains(header.Key))
			{
				continue;
			}
			context.Response.Headers[header.Key] = header.Value.ToArray();
		}

		context.Response.Headers.Remove("transfer-encoding");

		if (HttpMethods.IsHead(context.Request.Method) || context.Response.StatusCode == 304)
		{
			return;
		}

		await using var upstream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
		await upstream.CopyToAsync(context.Response.Body, ct).ConfigureAwait(false);
	}

	private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
	{
		"connection",
		"keep-alive",
		"proxy-authenticate",
		"proxy-authorization",
		"te",
		"trailer",
		"transfer-encoding",
		"upgrade",
		"host",
	};
}
