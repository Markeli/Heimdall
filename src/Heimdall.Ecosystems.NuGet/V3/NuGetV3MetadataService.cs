using System.Collections.Concurrent;
using System.Text.Json;
using Heimdall.Core.Configuration;
using Heimdall.Core.Packages;
using Heimdall.Ecosystems.NuGet.V3.Models;
using Heimdall.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Default <see cref="INuGetV3MetadataService"/>: orchestrates upstream fetches, runs configured filters,
/// rewrites URLs through Heimdall, and memoizes registration documents via <see cref="HybridCache"/>.
/// </summary>
public sealed class NuGetV3MetadataService : INuGetV3MetadataService
{
	private const string Ecosystem = "nuget";

	private readonly IFeedConfigLookup _lookup;
	private readonly IConfigSnapshotProvider _snapshots;
	private readonly INuGetV3UpstreamClient _upstream;
	private readonly HybridCache _cache;
	private readonly NuGetV3MetadataTransformer _transformer;
	private readonly NuGetV3UrlRewriter _urls;
	private readonly IOptionsMonitor<HeimdallOptions> _options;
	private readonly ILogger<NuGetV3MetadataService> _logger;

	/// <summary>
	/// Creates a new <see cref="NuGetV3MetadataService"/>.
	/// </summary>
	/// <param name="lookup">Lookup for feed configuration by ecosystem and feed name.</param>
	/// <param name="snapshots">Provider of monotonic configuration snapshots used to key the cache.</param>
	/// <param name="upstream">Typed upstream HTTP client.</param>
	/// <param name="cache">Hybrid (L1 + optional L2) cache for registration documents. Provides stampede protection.</param>
	/// <param name="transformer">Filter-and-rewrite transformer.</param>
	/// <param name="urls">Heimdall-facing URL rewriter.</param>
	/// <param name="options">Live options monitor used to read the search enrichment concurrency cap.</param>
	/// <param name="logger">Logger used to surface registration-enrichment failures during search.</param>
	/// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
	public NuGetV3MetadataService(
		IFeedConfigLookup lookup,
		IConfigSnapshotProvider snapshots,
		INuGetV3UpstreamClient upstream,
		HybridCache cache,
		NuGetV3MetadataTransformer transformer,
		NuGetV3UrlRewriter urls,
		IOptionsMonitor<HeimdallOptions> options,
		ILogger<NuGetV3MetadataService> logger)
	{
		ArgumentNullException.ThrowIfNull(lookup);
		ArgumentNullException.ThrowIfNull(snapshots);
		ArgumentNullException.ThrowIfNull(upstream);
		ArgumentNullException.ThrowIfNull(cache);
		ArgumentNullException.ThrowIfNull(transformer);
		ArgumentNullException.ThrowIfNull(urls);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_lookup = lookup;
		_snapshots = snapshots;
		_upstream = upstream;
		_cache = cache;
		_transformer = transformer;
		_urls = urls;
		_options = options;
		_logger = logger;
	}

	/// <inheritdoc />
	public bool TryGetFeed(string feedName, out FeedConfig? feed) =>
		_lookup.TryGet(Ecosystem, feedName, out feed);

	/// <inheritdoc />
	public string BuildServiceIndexV3Json(string feedName)
	{
		// Clients must talk to Heimdall, never to the upstream directly, so every resource URL
		// advertised here is a Heimdall URL produced by NuGetV3UrlRewriter.
		var index = new ServiceIndexV3
		{
			Version = "3.0.0",
			Resources =
			[
				new ServiceResourceV3
				{
					Id = _urls.RegistrationsBase(feedName).ToString(),
					Type = "RegistrationsBaseUrl/3.6.0",
					Comment = "Heimdall registration base",
				},
				new ServiceResourceV3
				{
					Id = _urls.FlatContainerBase(feedName).ToString(),
					Type = "PackageBaseAddress/3.0.0",
					Comment = "Heimdall flat container",
				},
				new ServiceResourceV3
				{
					Id = _urls.SearchQuery(feedName).ToString(),
					Type = "SearchQueryService",
					Comment = "Heimdall search proxy",
				},
			],
		};

		return JsonSerializer.Serialize(index, JsonOptions);
	}

	/// <inheritdoc />
	public async Task<string?> GetVersionsListJsonAsync(string feedName, string packageId, CancellationToken ct)
	{
		var registration = await GetRegistrationFromCacheOrUpstreamAsync(feedName, packageId, ct);
		if (registration is null)
		{
			return null;
		}

		var feed = RequireFeed(feedName);
		return _transformer.BuildVersionsListJson(registration, feed);
	}

	/// <inheritdoc />
	public async Task<string?> GetRegistrationJsonAsync(string feedName, string packageId, CancellationToken ct)
	{
		var registration = await GetRegistrationFromCacheOrUpstreamAsync(feedName, packageId, ct);
		if (registration is null)
		{
			return null;
		}

		var feed = RequireFeed(feedName);
		return _transformer.RewriteRegistration(registration, feed);
	}

	/// <inheritdoc />
	public async Task<string?> SearchJsonAsync(
		string feedName, string? query, int skip, int take, bool includePrerelease, CancellationToken ct)
	{
		var feed = RequireFeed(feedName);
		var result = await _upstream.SearchAsync(
			feed.Upstream, query ?? "", skip, take, includePrerelease, ct);
		if (result is null)
		{
			return null;
		}

		// Search hits carry no publish dates, so date-based rules (e.g. minAgeDays) cannot be applied
		// to them directly. Enrich each hit with the registration metadata Heimdall already caches so
		// search filters consistently with the specific-package endpoints.
		var enriched = await EnrichSearchHitsAsync(feedName, result, ct);
		return _transformer.RewriteSearch(result, feed, enriched);
	}

	private async Task<IReadOnlyDictionary<string, IReadOnlyList<PackageVersionMetadata>>> EnrichSearchHitsAsync(
		string feedName, SearchResultV3 result, CancellationToken ct)
	{
		var packageIds = result.Data
			.Select(hit => hit.PackageId)
			.Where(id => !string.IsNullOrEmpty(id))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var enriched = new ConcurrentDictionary<string, IReadOnlyList<PackageVersionMetadata>>(
			StringComparer.OrdinalIgnoreCase);
		if (packageIds.Count == 0)
		{
			return enriched;
		}

		// Bound the fan-out: a search page must not spawn one unbounded registration fetch per hit.
		// The cap is configurable so it can be tuned to the deployment's upstream/thread-pool budget.
		var parallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = Math.Max(1, _options.CurrentValue.Server.Search.MaxConcurrentRegistrationFetches),
			CancellationToken = ct,
		};

		await Parallel.ForEachAsync(packageIds, parallelOptions, async (packageId, token) =>
		{
			try
			{
				var registration = await GetRegistrationFromCacheOrUpstreamAsync(feedName, packageId, token);
				if (registration is not null)
				{
					enriched[packageId] = NuGetV3MetadataProjection.ToVersionMetadata(registration);
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// On failure the transformer falls back to date-less hit metadata, which drops the hit
				// under date-based rules. Log so the silent omission is observable rather than mysterious.
				_logger.LogWarning(
					ex,
					"Failed to enrich search hit {PackageId} on feed {Feed} from registration; "
						+ "it will be filtered using date-less metadata",
					packageId,
					feedName);
			}
		});

		return enriched;
	}

	/// <inheritdoc />
	public async Task<RegistrationLeafV3?> GetVersionLeafAsync(
		string feedName, string packageId, string version, CancellationToken ct)
	{
		var registration = await GetRegistrationFromCacheOrUpstreamAsync(feedName, packageId, ct);
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
				if (string.Equals(leaf.CatalogEntryV3?.Version, version, StringComparison.OrdinalIgnoreCase))
				{
					return leaf;
				}
			}
		}

		return null;
	}

	private async Task<RegistrationIndexV3?> GetRegistrationFromCacheOrUpstreamAsync(
		string feedName, string packageId, CancellationToken ct)
	{
		var feed = RequireFeed(feedName);
		var snapshot = _snapshots.Capture();

		// Including the snapshot generation in the cache key invalidates entries automatically
		// whenever feed configuration changes. Package IDs are lowercased to match NuGet's normalization.
		var key = $"g{snapshot.Generation}:{Ecosystem}:{feedName}:reg:{packageId.ToLowerInvariant()}";

		var entryOptions = new HybridCacheEntryOptions
		{
			Expiration = feed.CacheTtl ?? TimeSpan.FromMinutes(5),
		};

		// HybridCache provides stampede protection: only one concurrent miss runs the factory.
		return await _cache.GetOrCreateAsync<RegistrationIndexV3?>(
			key,
			async cancellationToken => await _upstream.GetRegistrationAsync(feed.Upstream, packageId, cancellationToken),
			entryOptions,
			cancellationToken: ct);
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

/// <summary>
/// Thrown by <see cref="NuGetV3MetadataService"/> when a feed name is not registered in the configured ecosystem.
/// </summary>
public sealed class FeedNotFoundException : Exception
{
	/// <summary>
	/// Creates a new <see cref="FeedNotFoundException"/>.
	/// </summary>
	/// <param name="ecosystem">Ecosystem identifier (e.g. <c>nuget</c>).</param>
	/// <param name="feedName">Feed name that could not be resolved.</param>
	public FeedNotFoundException(string ecosystem, string feedName)
		: base($"feed '{feedName}' not found in ecosystem '{ecosystem}'")
	{
		Ecosystem = ecosystem;
		FeedName = feedName;
	}

	/// <summary>Ecosystem identifier the lookup was performed in.</summary>
	public string Ecosystem { get; }

	/// <summary>Feed name that was not found.</summary>
	public string FeedName { get; }
}
