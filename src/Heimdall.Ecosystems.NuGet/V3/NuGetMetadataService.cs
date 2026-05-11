using System.Text.Json;
using Heimdall.Application.Caching;
using Heimdall.Application.Configuration;
using Heimdall.Domain.Configuration;
using Heimdall.Ecosystems.NuGet.V3.Models;

namespace Heimdall.Ecosystems.NuGet.V3;

public sealed class NuGetMetadataService : INuGetMetadataService
{
	private const string Ecosystem = "nuget";

	private readonly IFeedConfigLookup _lookup;
	private readonly IConfigSnapshotProvider _snapshots;
	private readonly INuGetUpstreamClient _upstream;
	private readonly IMetadataCache _cache;
	private readonly NuGetMetadataTransformer _transformer;
	private readonly NuGetUrlRewriter _urls;

	public NuGetMetadataService(
		IFeedConfigLookup lookup,
		IConfigSnapshotProvider snapshots,
		INuGetUpstreamClient upstream,
		IMetadataCache cache,
		NuGetMetadataTransformer transformer,
		NuGetUrlRewriter urls)
	{
		ArgumentNullException.ThrowIfNull(lookup);
		ArgumentNullException.ThrowIfNull(snapshots);
		ArgumentNullException.ThrowIfNull(upstream);
		ArgumentNullException.ThrowIfNull(cache);
		ArgumentNullException.ThrowIfNull(transformer);
		ArgumentNullException.ThrowIfNull(urls);

		_lookup = lookup;
		_snapshots = snapshots;
		_upstream = upstream;
		_cache = cache;
		_transformer = transformer;
		_urls = urls;
	}

	public bool TryGetFeed(string feedName, out FeedConfig? feed) =>
		_lookup.TryGet(Ecosystem, feedName, out feed);

	public string BuildServiceIndexJson(string feedName)
	{
		var index = new ServiceIndex
		{
			Version = "3.0.0",
			Resources =
			[
				new ServiceResource
				{
					Id = _urls.RegistrationsBase(feedName).ToString(),
					Type = "RegistrationsBaseUrl/3.6.0",
					Comment = "Heimdall registration base",
				},
				new ServiceResource
				{
					Id = _urls.FlatContainerBase(feedName).ToString(),
					Type = "PackageBaseAddress/3.0.0",
					Comment = "Heimdall flat container",
				},
				new ServiceResource
				{
					Id = _urls.SearchQuery(feedName).ToString(),
					Type = "SearchQueryService",
					Comment = "Heimdall search proxy",
				},
			],
		};

		return JsonSerializer.Serialize(index, JsonOptions);
	}

	public async Task<string?> GetVersionsListJsonAsync(string feedName, string packageId, CancellationToken ct)
	{
		var registration = await GetRegistrationFromCacheOrUpstreamAsync(feedName, packageId, ct).ConfigureAwait(false);
		if (registration is null)
		{
			return null;
		}

		var feed = RequireFeed(feedName);
		return _transformer.BuildVersionsListJson(registration, feed);
	}

	public async Task<string?> GetRegistrationJsonAsync(string feedName, string packageId, CancellationToken ct)
	{
		var registration = await GetRegistrationFromCacheOrUpstreamAsync(feedName, packageId, ct).ConfigureAwait(false);
		if (registration is null)
		{
			return null;
		}

		var feed = RequireFeed(feedName);
		return _transformer.RewriteRegistration(registration, feed);
	}

	public async Task<string?> SearchJsonAsync(
		string feedName, string? query, int skip, int take, bool includePrerelease, CancellationToken ct)
	{
		var feed = RequireFeed(feedName);
		var result = await _upstream.SearchAsync(
			feed.Upstream, query ?? "", skip, take, includePrerelease, ct).ConfigureAwait(false);
		if (result is null)
		{
			return null;
		}

		return _transformer.RewriteSearch(result, feed);
	}

	public async Task<RegistrationLeaf?> GetVersionLeafAsync(
		string feedName, string packageId, string version, CancellationToken ct)
	{
		var registration = await GetRegistrationFromCacheOrUpstreamAsync(feedName, packageId, ct).ConfigureAwait(false);
		if (registration is null)
		{
			return null;
		}

		foreach (var page in registration.Items)
		{
			if (page.Items is null)
			{
				continue;
			}
			foreach (var leaf in page.Items)
			{
				if (string.Equals(leaf.CatalogEntry?.Version, version, StringComparison.OrdinalIgnoreCase))
				{
					return leaf;
				}
			}
		}

		return null;
	}

	private async Task<RegistrationIndex?> GetRegistrationFromCacheOrUpstreamAsync(
		string feedName, string packageId, CancellationToken ct)
	{
		var feed = RequireFeed(feedName);
		var snapshot = _snapshots.Capture();

		var key = $"g{snapshot.Generation}:{Ecosystem}:{feedName}:reg:{packageId.ToLowerInvariant()}";
		var cached = await _cache.GetAsync<RegistrationIndex>(key, ct).ConfigureAwait(false);
		if (cached is not null)
		{
			return cached;
		}

		var fetched = await _upstream.GetRegistrationAsync(feed.Upstream, packageId, ct).ConfigureAwait(false);
		if (fetched is null)
		{
			return null;
		}

		var ttl = feed.CacheTtl ?? TimeSpan.FromMinutes(5);
		await _cache.SetAsync(key, fetched, ttl, ct).ConfigureAwait(false);
		return fetched;
	}

	private FeedConfig RequireFeed(string feedName)
	{
		if (!_lookup.TryGet(Ecosystem, feedName, out var feed))
		{
			throw new FeedNotFoundException(Ecosystem, feedName);
		}
		return feed;
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
	};
}

public sealed class FeedNotFoundException : Exception
{
	public FeedNotFoundException(string ecosystem, string feedName)
		: base($"feed '{feedName}' not found in ecosystem '{ecosystem}'")
	{
		Ecosystem = ecosystem;
		FeedName = feedName;
	}

	public string Ecosystem { get; }
	public string FeedName { get; }
}
