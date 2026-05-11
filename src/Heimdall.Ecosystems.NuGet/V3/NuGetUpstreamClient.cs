using System.Net.Http.Json;
using System.Text.Json;
using Heimdall.Ecosystems.NuGet.V3.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Ecosystems.NuGet.V3;

public sealed class NuGetUpstreamClient : INuGetUpstreamClient
{
	public const string MetadataHttpClientName = "Heimdall.NuGet.Metadata";
	public const string BinaryHttpClientName = "Heimdall.NuGet.Binary";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
	};

	private readonly IHttpClientFactory _factory;
	private readonly ILogger<NuGetUpstreamClient> _logger;
	private readonly IUpstreamUrlResolver _urls;

	public NuGetUpstreamClient(
		IHttpClientFactory factory,
		IUpstreamUrlResolver urls,
		ILogger<NuGetUpstreamClient> logger)
	{
		ArgumentNullException.ThrowIfNull(factory);
		ArgumentNullException.ThrowIfNull(urls);
		ArgumentNullException.ThrowIfNull(logger);
		_factory = factory;
		_urls = urls;
		_logger = logger;
	}

	public async Task<RegistrationIndex?> GetRegistrationAsync(
		Uri serviceIndex, string packageId, CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(serviceIndex);
		ArgumentException.ThrowIfNullOrEmpty(packageId);

		var url = await _urls.GetRegistrationBaseUrlAsync(serviceIndex, ct).ConfigureAwait(false);
		var fullUrl = new Uri(url, $"{packageId.ToLowerInvariant()}/index.json");

		var http = _factory.CreateClient(MetadataHttpClientName);
		using var resp = await http.GetAsync(fullUrl, ct).ConfigureAwait(false);
		if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return null;
		}

		resp.EnsureSuccessStatusCode();

		await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
		return await JsonSerializer.DeserializeAsync<RegistrationIndex>(stream, JsonOptions, ct)
			.ConfigureAwait(false);
	}

	public async Task<SearchResult?> SearchAsync(
		Uri serviceIndex, string query, int skip, int take, bool includePrerelease, CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(serviceIndex);

		var baseUrl = await _urls.GetSearchQueryServiceAsync(serviceIndex, ct).ConfigureAwait(false);
		var qs = $"?q={Uri.EscapeDataString(query ?? "")}"
			+ $"&skip={skip}&take={take}&prerelease={(includePrerelease ? "true" : "false")}";
		var fullUrl = new Uri(baseUrl + qs);

		var http = _factory.CreateClient(MetadataHttpClientName);
		using var resp = await http.GetAsync(fullUrl, ct).ConfigureAwait(false);
		if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return null;
		}

		resp.EnsureSuccessStatusCode();

		return await resp.Content.ReadFromJsonAsync<SearchResult>(JsonOptions, ct).ConfigureAwait(false);
	}

	public Task<HttpResponseMessage> SendBinaryAsync(HttpRequestMessage request, CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(request);

		var http = _factory.CreateClient(BinaryHttpClientName);
		return http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
	}
}
