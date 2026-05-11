using Heimdall.Api.Audit;
using Heimdall.Api.Http;
using Heimdall.Core.Configuration;
using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;
using Heimdall.Ecosystems.NuGet.V3;
using Microsoft.AspNetCore.Mvc;
using Semver;

namespace Heimdall.Api.BinaryProxy;

/// <summary>
/// Coordinates the policy-gated proxying of NuGet <c>.nupkg</c> downloads. Resolves the version,
/// runs the single-version gate, then streams the upstream response body to the client without
/// buffering. Emits an audit record for every decision.
/// </summary>
public sealed class NuGetV3BinaryProxyService
{
	private const string Ecosystem = "nuget";

	private readonly IFeedConfigLookup _lookup;
	private readonly INuGetV3MetadataService _metadata;
	private readonly INuGetV3UpstreamClient _upstream;
	private readonly INuGetV3UpstreamUrlResolver _urls;
	private readonly ISingleVersionGate _gate;
	private readonly TimeProvider _time;
	private readonly AuditLogger _audit;

	/// <summary>
	/// Initializes a new instance of the <see cref="NuGetV3BinaryProxyService"/> class.
	/// </summary>
	/// <param name="lookup">Lookup that resolves the feed configuration by ecosystem and name.</param>
	/// <param name="metadata">Metadata service used to obtain the registration leaf for the requested version.</param>
	/// <param name="upstream">HTTP client wrapper used to issue the binary request to the upstream feed.</param>
	/// <param name="urls">Resolver that returns the package base address of the upstream feed.</param>
	/// <param name="gate">Filtering gate that decides whether the requested version is allowed.</param>
	/// <param name="time">Time abstraction used to stamp the gate evaluation.</param>
	/// <param name="audit">Audit logger that records the allow/deny decision.</param>
	/// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
	public NuGetV3BinaryProxyService(
		IFeedConfigLookup lookup,
		INuGetV3MetadataService metadata,
		INuGetV3UpstreamClient upstream,
		INuGetV3UpstreamUrlResolver urls,
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

	/// <summary>
	/// Evaluates the policy gate for the requested package version and, when allowed, streams the
	/// upstream binary response directly to the client. When denied or not found, returns a
	/// ProblemDetails action result that the controller can return as-is.
	/// </summary>
	/// <param name="context">Current HTTP context; used to read request headers and write the response.</param>
	/// <param name="feedName">Configured logical feed name.</param>
	/// <param name="packageId">NuGet package identifier from the route (case-insensitive).</param>
	/// <param name="version">Package version from the route (case-insensitive).</param>
	/// <param name="fileName">Final URL segment, typically <c>{id}.{version}.nupkg</c>.</param>
	/// <param name="ct">Token used to cancel the upstream call and response streaming.</param>
	/// <returns>
	/// <c>null</c> when the response has been streamed and the controller should produce no further
	/// content; otherwise a ProblemDetails result describing why the request was rejected.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
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

		var leaf = await _metadata.GetVersionLeafAsync(feedName, packageId, version, ct);
		if (leaf?.CatalogEntryV3 is null)
		{
			return new NotFoundResult();
		}

		var entry = leaf.CatalogEntryV3;
		if (!SemVersion.TryParse(entry.Version, SemVersionStyles.Any, out var sv))
		{
			return new NotFoundResult();
		}

		var coords = new PackageCoordinates(Ecosystem, entry.PackageId, sv);
		var meta = new PackageVersionMetadata(coords, entry.PublishedUtc, new Dictionary<string, string>());

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

		var packageBase = await _urls.GetPackageBaseAddressAsync(feed.Upstream, ct);
		var upstreamUri = new Uri(
			packageBase,
			$"{entry.PackageId.ToLowerInvariant()}/{entry.Version.ToLowerInvariant()}/{fileName}");

		using var request = HttpProxyHelpers.BuildUpstreamRequest(context.Request, upstreamUri);
		var response = await _upstream.SendBinaryAsync(request, ct);

		// Stream the upstream body straight into Response.Body to avoid buffering whole .nupkg files in memory.
		await HttpProxyHelpers.PipeResponseAsync(response, context, ct);

		if (response.IsSuccessStatusCode)
		{
			_audit.Allow(Ecosystem, feedName, entry.PackageId, entry.Version, ip, ua);
		}

		response.Dispose();
		return null;
	}
}
