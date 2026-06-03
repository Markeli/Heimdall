using System.Text.Json;
using System.Text.Json.Nodes;
using Heimdall.Core.Filtering;
using Heimdall.Core.Configuration;
using Heimdall.Core.Packages;
using Heimdall.Ecosystems.NuGet.V3.Models;
using Semver;

namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Applies version filters to projected NuGet metadata and rewrites the surviving upstream URLs so they
/// point at Heimdall's public endpoints rather than the upstream feed.
/// </summary>
public sealed class NuGetV3MetadataTransformer
{
	private readonly NuGetV3UrlRewriter _urls;
	private readonly IVersionListFilter _filter;
	private readonly TimeProvider _time;

	/// <summary>
	/// Creates a new <see cref="NuGetV3MetadataTransformer"/>.
	/// </summary>
	/// <param name="urls">Heimdall-facing URL rewriter.</param>
	/// <param name="filter">Filter applied to projected version metadata.</param>
	/// <param name="time">Time provider used for age-based filter evaluation.</param>
	/// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
	public NuGetV3MetadataTransformer(NuGetV3UrlRewriter urls, IVersionListFilter filter, TimeProvider time)
	{
		ArgumentNullException.ThrowIfNull(urls);
		ArgumentNullException.ThrowIfNull(filter);
		ArgumentNullException.ThrowIfNull(time);
		_urls = urls;
		_filter = filter;
		_time = time;
	}

	/// <summary>
	/// Builds the flat-container <c>index.json</c> body: an object with a single <c>versions</c> array
	/// containing only versions that pass the filter, ordered ascending by semantic version.
	/// </summary>
	/// <param name="registration">Upstream registration index for the package.</param>
	/// <param name="feed">Feed configuration whose filter rules apply.</param>
	/// <returns>Serialized JSON for the versions list, or <c>null</c> when every version is filtered out.</returns>
	/// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
	public string? BuildVersionsListJson(RegistrationIndexV3 registration, FeedConfig feed)
	{
		ArgumentNullException.ThrowIfNull(registration);
		ArgumentNullException.ThrowIfNull(feed);

		var metas = NuGetV3MetadataProjection.ToVersionMetadata(registration);
		var passed = _filter.Apply(metas, feed, _time.GetUtcNow());
		if (passed.Count == 0)
		{
			// All versions filtered out — signal "not found" so the controller returns 404.
			return null;
		}

		var versions = VersionOrdering.OrderAscending(passed)
			.Select(m => m.Coords.Version.ToString())
			.ToArray();

		var doc = new JsonObject
		{
			["versions"] = new JsonArray(versions.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()),
		};

		return doc.ToJsonString();
	}

	/// <summary>
	/// Rewrites a registration index so that surviving leaves point at Heimdall URLs. Filtered-out
	/// versions are dropped and the index is collapsed into a single page covering the kept range.
	/// </summary>
	/// <param name="registration">Upstream registration index.</param>
	/// <param name="feed">Feed configuration whose filter rules and name apply.</param>
	/// <returns>
	/// Serialized JSON of the rewritten registration index, or <c>null</c> when every version is
	/// filtered out (so the controller returns 404).
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the upstream registration index contains no leaves to identify the package by.
	/// </exception>
	public string? RewriteRegistration(RegistrationIndexV3 registration, FeedConfig feed)
	{
		ArgumentNullException.ThrowIfNull(registration);
		ArgumentNullException.ThrowIfNull(feed);

		var metas = NuGetV3MetadataProjection.ToVersionMetadata(registration);
		// Match on the parsed SemVersion, not its string form: the filter's survivor set and the
		// upstream leaf may spell the same version differently ("1.0" vs "1.0.0"), so a string
		// comparison would silently drop versions that actually passed the filter.
		var passed = new HashSet<SemVersion>(_filter.Apply(metas, feed, _time.GetUtcNow()).Select(m => m.Coords.Version));

		var packageId = registration.Items
			.FirstOrDefault()?.Items?
			.FirstOrDefault()?.CatalogEntryV3?.PackageId
			?? throw new InvalidOperationException("registration has no leaves to rewrite");

		var rewritten = new RegistrationIndexV3
		{
			Id = _urls.RegistrationIndexV3(feed.Name, packageId).ToString(),
			Items = [],
		};

		// Parse each surviving leaf's version once so we can both match the filter set and order by
		// semantic version without re-parsing.
		var kept = new List<(RegistrationLeafV3 Leaf, SemVersion Version)>();
		foreach (var page in registration.Items)
		{
			if (page.Items is null)
			{
				continue;
			}

			foreach (var leaf in page.Items)
			{
				var entry = leaf.CatalogEntryV3;
				if (entry is null ||
					!SemVersion.TryParse(entry.Version, SemVersionStyles.Any, out var version) ||
					!passed.Contains(version))
				{
					continue;
				}

				// Every @id and packageContent URL is rewritten so that clients fetch through Heimdall
				// rather than following links back to nuget.org directly.
				var rewrittenLeaf = new RegistrationLeafV3
				{
					Id = _urls.RegistrationLeafV3(feed.Name, entry.PackageId, entry.Version).ToString(),
					CatalogEntryV3 = new CatalogEntryV3
					{
						Id = _urls.RegistrationLeafV3(feed.Name, entry.PackageId, entry.Version).ToString(),
						PackageId = entry.PackageId,
						Version = entry.Version,
						PublishedUtc = entry.PublishedUtc,
						Listed = entry.Listed,
						PackageContent = _urls.PackageContent(feed.Name, entry.PackageId, entry.Version).ToString(),
					},
					PackageContent = _urls.PackageContent(feed.Name, entry.PackageId, entry.Version).ToString(),
				};
				kept.Add((rewrittenLeaf, version));
			}
		}

		if (kept.Count == 0)
		{
			// Every version was filtered out — signal "not found" so the controller returns 404.
			return null;
		}

		// Order by semantic version rather than trusting upstream order, so lower/upper are the
		// true min/max of the surviving set.
		var keptLeaves = kept
			.OrderBy(x => x.Version, VersionOrdering.Ascending)
			.Select(x => x.Leaf)
			.ToList();

		var first = keptLeaves[0].CatalogEntryV3!.Version;
		var last = keptLeaves[^1].CatalogEntryV3!.Version;
		rewritten.Items.Add(new RegistrationPageV3
		{
			Id = _urls.RegistrationPageV3(feed.Name, packageId, first, last).ToString(),
			Count = keptLeaves.Count,
			Lower = first,
			Upper = last,
			Items = keptLeaves,
		});

		rewritten.Count = keptLeaves.Count;

		return JsonSerializer.Serialize(rewritten, JsonOptions);
	}

	/// <summary>
	/// Rewrites a <c>SearchQueryService</c> response: hits whose versions are entirely filtered out are
	/// removed, surviving hits have their @id, registration, and per-version URLs pointed at Heimdall,
	/// the surviving versions are ordered ascending by semantic version, and the primary version is
	/// recomputed as the latest survivor (highest stable, else highest prerelease).
	/// </summary>
	/// <param name="upstream">Raw upstream search result.</param>
	/// <param name="feed">Feed configuration whose filter rules and name apply.</param>
	/// <param name="includePrerelease">
	/// Whether the originating query requested prereleases; when <c>true</c> the recomputed primary
	/// version may be a prerelease, matching NuGet's contract that the hit version is the latest
	/// respecting the prerelease parameter.
	/// </param>
	/// <param name="enrichedByPackageId">
	/// Optional per-package version metadata (with publish dates) sourced from the registration index,
	/// keyed by package id (case-insensitive). Used so date-based rules filter search results the same
	/// way they filter a specific package. When a hit is absent, the date-less hit metadata is used as a
	/// fallback — under date-based rules that drops the hit entirely.
	/// </param>
	/// <returns>Serialized JSON of the rewritten search result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="upstream"/> or <paramref name="feed"/> is null.</exception>
	public string RewriteSearch(
		SearchResultV3 upstream,
		FeedConfig feed,
		IReadOnlyList<IRule> rules,
		bool includePrerelease = false,
		IReadOnlyDictionary<string, IReadOnlyList<PackageVersionMetadata>>? enrichedByPackageId = null)
	{
		ArgumentNullException.ThrowIfNull(upstream);
		ArgumentNullException.ThrowIfNull(feed);
		ArgumentNullException.ThrowIfNull(rules);

		// Build the evaluation context once and reuse the pre-built rules across every hit, so glob
		// regexes are not recompiled per hit.
		var ctx = new RuleContext(feed.Ecosystem, feed.Name, _time.GetUtcNow());

		var passedHits = new List<SearchHitV3>();
		foreach (var hit in upstream.Data)
		{
			var metas = enrichedByPackageId is not null
				&& enrichedByPackageId.TryGetValue(hit.PackageId, out var enriched)
					? enriched
					: HitToMetadata(hit);

			// Match on the parsed SemVersion rather than its string form so non-canonical upstream
			// version strings ("1.0" vs "1.0.0") are not silently dropped.
			var passed = new HashSet<SemVersion>(_filter.Apply(metas, rules, ctx).Select(m => m.Coords.Version));

			// Keep the hit's own version entries (they carry the download counts) that survived the
			// filter. Pair each with the parsed version for semantic-version ordering and latest selection.
			var kept = new List<(SearchVersionV3 Entry, SemVersion Version)>();
			foreach (var v in hit.Versions)
			{
				if (!SemVersion.TryParse(v.Version, SemVersionStyles.Any, out var version) ||
					!passed.Contains(version))
				{
					continue;
				}
				kept.Add((v, version));
			}

			if (kept.Count == 0)
			{
				continue;
			}

			// Recompute the primary "version" over the survivors instead of trusting the upstream's
			// pick, which may have been filtered out. Map back to the entry's original version string.
			var latest = VersionOrdering.SelectLatest(
				kept.Select(k => PackageVersionMetadata.Create(new PackageCoordinates("nuget", hit.PackageId, k.Version), null)),
				includePrerelease)!;
			var primary = kept.First(k => k.Version.Equals(latest.Coords.Version)).Entry.Version;

			var orderedVersions = kept
				.OrderBy(k => k.Version, VersionOrdering.Ascending)
				.Select(k => new SearchVersionV3
				{
					Version = k.Entry.Version,
					Downloads = k.Entry.Downloads,
					Id = _urls.RegistrationLeafV3(feed.Name, hit.PackageId, k.Entry.Version).ToString(),
				})
				.ToList();

			passedHits.Add(new SearchHitV3
			{
				Id = _urls.RegistrationIndexV3(feed.Name, hit.PackageId).ToString(),
				PackageId = hit.PackageId,
				Version = primary,
				Registration = _urls.RegistrationIndexV3(feed.Name, hit.PackageId).ToString(),
				Description = hit.Description,
				Versions = orderedVersions,
			});
		}

		var rewritten = new SearchResultV3
		{
			// Preserve the upstream's global total so clients can still paginate; collapsing it to the
			// surviving-hits-on-this-page count would truncate search to a single page.
			TotalHits = upstream.TotalHits,
			Data = passedHits,
		};

		return JsonSerializer.Serialize(rewritten, JsonOptions);
	}

	private static List<PackageVersionMetadata> HitToMetadata(SearchHitV3 hit)
	{
		var result = new List<PackageVersionMetadata>();
		foreach (var v in hit.Versions)
		{
			if (!SemVersion.TryParse(v.Version, SemVersionStyles.Any, out var sv))
			{
				continue;
			}
			// Search hits do not carry per-version publish timestamps; age-based filters that
			// require PublishedUtc will treat these as unknown (and therefore deny).
			result.Add(new PackageVersionMetadata(
				new PackageCoordinates("nuget", hit.PackageId, sv),
				PublishedUtc: null,
				Extra: new Dictionary<string, string>()));
		}
		return result;
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = null,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false,
	};
}
