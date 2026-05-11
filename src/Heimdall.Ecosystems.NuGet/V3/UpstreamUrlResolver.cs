using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Heimdall.Ecosystems.NuGet.V3.Models;

namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Default <see cref="IUpstreamUrlResolver"/>: fetches and caches an upstream's service index by URL,
/// then locates known resource types within it.
/// </summary>
public sealed class UpstreamUrlResolver : IUpstreamUrlResolver
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	// 3.6.0 is the modern semver2 + gzip variant exposed by nuget.org. The "/Versioned" alias is
	// preferred as a fallback because some private feeds publish it without the explicit 3.6.0 suffix.
	private const string RegistrationsBaseUrlSemver2 = "RegistrationsBaseUrl/3.6.0";
	private const string RegistrationsBaseUrlGzSemver2 = "RegistrationsBaseUrl/Versioned";
	private const string PackageBaseAddress = "PackageBaseAddress/3.0.0";
	private const string SearchQueryService = "SearchQueryService";

	private readonly IHttpClientFactory _factory;
	private readonly ConcurrentDictionary<string, ServiceIndex> _cache = new();

	/// <summary>
	/// Creates a new <see cref="UpstreamUrlResolver"/>.
	/// </summary>
	/// <param name="factory">Factory used to resolve the metadata HTTP client.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
	public UpstreamUrlResolver(IHttpClientFactory factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		_factory = factory;
	}

	/// <inheritdoc />
	public async Task<Uri> GetRegistrationBaseUrlAsync(Uri serviceIndex, CancellationToken ct)
	{
		var index = await GetServiceIndexAsync(serviceIndex, ct).ConfigureAwait(false);
		var url = FindResource(index, RegistrationsBaseUrlSemver2)
			?? FindResource(index, RegistrationsBaseUrlGzSemver2)
			?? throw new InvalidOperationException(
				$"upstream service index '{serviceIndex}' has no RegistrationsBaseUrl");
		return new Uri(EnsureTrailingSlash(url));
	}

	/// <inheritdoc />
	public async Task<Uri> GetPackageBaseAddressAsync(Uri serviceIndex, CancellationToken ct)
	{
		var index = await GetServiceIndexAsync(serviceIndex, ct).ConfigureAwait(false);
		var url = FindResource(index, PackageBaseAddress)
			?? throw new InvalidOperationException(
				$"upstream service index '{serviceIndex}' has no PackageBaseAddress/3.0.0");
		return new Uri(EnsureTrailingSlash(url));
	}

	/// <inheritdoc />
	public async Task<string> GetSearchQueryServiceAsync(Uri serviceIndex, CancellationToken ct)
	{
		var index = await GetServiceIndexAsync(serviceIndex, ct).ConfigureAwait(false);
		return FindResource(index, SearchQueryService)
			?? throw new InvalidOperationException(
				$"upstream service index '{serviceIndex}' has no SearchQueryService");
	}

	private async Task<ServiceIndex> GetServiceIndexAsync(Uri uri, CancellationToken ct)
	{
		var key = uri.ToString();
		if (_cache.TryGetValue(key, out var existing))
		{
			return existing;
		}

		var http = _factory.CreateClient(NuGetUpstreamClient.MetadataHttpClientName);
		var index = await http.GetFromJsonAsync<ServiceIndex>(uri, JsonOptions, ct)
			.ConfigureAwait(false)
			?? throw new InvalidOperationException($"upstream service index '{uri}' returned null");

		_cache[key] = index;
		return index;
	}

	private static string? FindResource(ServiceIndex index, string type)
	{
		foreach (var r in index.Resources)
		{
			if (string.Equals(r.Type, type, StringComparison.Ordinal))
			{
				return r.Id;
			}
		}
		return null;
	}

	private static string EnsureTrailingSlash(string url) =>
		url.EndsWith('/') ? url : url + "/";
}
