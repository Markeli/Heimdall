using System.Net.Http.Json;
using System.Text.Json;
using Heimdall.Ecosystems.NuGet.V3.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Default <see cref="INuGetV3UpstreamClient"/> implementation. Uses two named <see cref="HttpClient"/> instances:
/// one tuned for metadata (gzip/deflate, JSON) and one for binary .nupkg streaming.
/// </summary>
public sealed class NuGetV3UpstreamClient : INuGetV3UpstreamClient
{
	/// <summary>Named HttpClient used for JSON metadata requests (service index, registration, search).</summary>
	public const string MetadataHttpClientName = "Heimdall.NuGet.Metadata";

	/// <summary>Named HttpClient used for binary (.nupkg) requests; no automatic decompression.</summary>
	public const string BinaryHttpClientName = "Heimdall.NuGet.Binary";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
	};

	private readonly IHttpClientFactory _factory;
	private readonly ILogger<NuGetV3UpstreamClient> _logger;
	private readonly INuGetV3UpstreamUrlResolver _urls;

	/// <summary>
	/// Creates a new <see cref="NuGetV3UpstreamClient"/>.
	/// </summary>
	/// <param name="factory">Factory used to resolve the named HTTP clients.</param>
	/// <param name="urls">Upstream URL resolver used to discover resource endpoints.</param>
	/// <param name="logger">Logger.</param>
	/// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
	public NuGetV3UpstreamClient(
		IHttpClientFactory factory,
		INuGetV3UpstreamUrlResolver urls,
		ILogger<NuGetV3UpstreamClient> logger)
	{
		ArgumentNullException.ThrowIfNull(factory);
		ArgumentNullException.ThrowIfNull(urls);
		ArgumentNullException.ThrowIfNull(logger);
		_factory = factory;
		_urls = urls;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<RegistrationIndexV3?> GetRegistrationAsync(
		Uri serviceIndex, string packageId, CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(serviceIndex);
		ArgumentException.ThrowIfNullOrEmpty(packageId);

		// NuGet flat-container and registration paths require the lowercased package id.
		var url = await _urls.GetRegistrationBaseUrlAsync(serviceIndex, ct);
		var fullUrl = new Uri(url, $"{packageId.ToLowerInvariant()}/index.json");

		var http = _factory.CreateClient(MetadataHttpClientName);
		using var resp = await http.GetAsync(fullUrl, ct);
		if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return null;
		}

		resp.EnsureSuccessStatusCode();

		await using var stream = await resp.Content.ReadAsStreamAsync(ct);
		return await JsonSerializer.DeserializeAsync<RegistrationIndexV3>(stream, JsonOptions, ct)
			;
	}

	/// <inheritdoc />
	public async Task<SearchResultV3?> SearchAsync(
		Uri serviceIndex, string query, int skip, int take, bool includePrerelease, CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(serviceIndex);

		var baseUrl = await _urls.GetSearchQueryServiceAsync(serviceIndex, ct);
		var qs = $"?q={Uri.EscapeDataString(query ?? "")}"
			+ $"&skip={skip}&take={take}&prerelease={(includePrerelease ? "true" : "false")}";
		var fullUrl = new Uri(baseUrl + qs);

		var http = _factory.CreateClient(MetadataHttpClientName);
		using var resp = await http.GetAsync(fullUrl, ct);
		if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return null;
		}

		resp.EnsureSuccessStatusCode();

		return await resp.Content.ReadFromJsonAsync<SearchResultV3>(JsonOptions, ct);
	}

	/// <inheritdoc />
	public Task<HttpResponseMessage> SendBinaryAsync(HttpRequestMessage request, CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(request);

		// ResponseHeadersRead lets callers stream the .nupkg body without buffering it in memory first.
		var http = _factory.CreateClient(BinaryHttpClientName);
		return http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
	}
}
