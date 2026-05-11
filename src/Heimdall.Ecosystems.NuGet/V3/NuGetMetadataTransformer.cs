using System.Text.Json;
using System.Text.Json.Nodes;
using Heimdall.Core.Filtering;
using Heimdall.Core.Configuration;
using Heimdall.Core.Packages;
using Heimdall.Ecosystems.NuGet.V3.Models;

namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Applies version filters to projected NuGet metadata and rewrites the surviving upstream URLs so they
/// point at Heimdall's public endpoints rather than the upstream feed.
/// </summary>
public sealed class NuGetMetadataTransformer
{
	private readonly NuGetUrlRewriter _urls;
	private readonly IVersionListFilter _filter;
	private readonly TimeProvider _time;

	/// <summary>
	/// Creates a new <see cref="NuGetMetadataTransformer"/>.
	/// </summary>
	/// <param name="urls">Heimdall-facing URL rewriter.</param>
	/// <param name="filter">Filter applied to projected version metadata.</param>
	/// <param name="time">Time provider used for age-based filter evaluation.</param>
	/// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
	public NuGetMetadataTransformer(NuGetUrlRewriter urls, IVersionListFilter filter, TimeProvider time)
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
	/// containing only versions that pass the filter.
	/// </summary>
	/// <param name="registration">Upstream registration index for the package.</param>
	/// <param name="feed">Feed configuration whose filter rules apply.</param>
	/// <returns>Serialized JSON for the versions list.</returns>
	/// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
	public string BuildVersionsListJson(RegistrationIndex registration, FeedConfig feed)
	{
		ArgumentNullException.ThrowIfNull(registration);
		ArgumentNullException.ThrowIfNull(feed);

		var metas = NuGetMetadataProjection.ToVersionMetadata(registration);
		var passed = _filter.Apply(metas, feed, _time.GetUtcNow());

		var versions = passed
			.Select(m => m.Coords.Version.ToString())
			.OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
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
	/// <returns>Serialized JSON of the rewritten registration index.</returns>
	/// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the upstream registration index contains no leaves to identify the package by.
	/// </exception>
	public string RewriteRegistration(RegistrationIndex registration, FeedConfig feed)
	{
		ArgumentNullException.ThrowIfNull(registration);
		ArgumentNullException.ThrowIfNull(feed);

		var metas = NuGetMetadataProjection.ToVersionMetadata(registration);
		var passed = new HashSet<string>(
			_filter.Apply(metas, feed, _time.GetUtcNow()).Select(m => m.Coords.Version.ToString()),
			StringComparer.OrdinalIgnoreCase);

		var packageId = registration.Items
			.FirstOrDefault()?.Items?
			.FirstOrDefault()?.CatalogEntry?.PackageId
			?? throw new InvalidOperationException("registration has no leaves to rewrite");

		var rewritten = new RegistrationIndex
		{
			Id = _urls.RegistrationIndex(feed.Name, packageId).ToString(),
			Items = [],
		};

		var keptLeaves = new List<RegistrationLeaf>();
		foreach (var page in registration.Items)
		{
			if (page.Items is null)
			{
				continue;
			}

			foreach (var leaf in page.Items)
			{
				var entry = leaf.CatalogEntry;
				if (entry is null || !passed.Contains(entry.Version))
				{
					continue;
				}

				// Every @id and packageContent URL is rewritten so that clients fetch through Heimdall
				// rather than following links back to nuget.org directly.
				keptLeaves.Add(new RegistrationLeaf
				{
					Id = _urls.RegistrationLeaf(feed.Name, entry.PackageId, entry.Version).ToString(),
					CatalogEntry = new CatalogEntry
					{
						Id = _urls.RegistrationLeaf(feed.Name, entry.PackageId, entry.Version).ToString(),
						PackageId = entry.PackageId,
						Version = entry.Version,
						Published = entry.Published,
						Listed = entry.Listed,
						PackageContent = _urls.PackageContent(feed.Name, entry.PackageId, entry.Version).ToString(),
					},
					PackageContent = _urls.PackageContent(feed.Name, entry.PackageId, entry.Version).ToString(),
				});
			}
		}

		if (keptLeaves.Count > 0)
		{
			var first = keptLeaves[0].CatalogEntry!.Version;
			var last = keptLeaves[^1].CatalogEntry!.Version;
			rewritten.Items.Add(new RegistrationPage
			{
				Id = _urls.RegistrationPage(feed.Name, packageId, first, last).ToString(),
				Count = keptLeaves.Count,
				Lower = first,
				Upper = last,
				Items = keptLeaves,
			});
		}

		rewritten.Count = keptLeaves.Count;

		return JsonSerializer.Serialize(rewritten, JsonOptions);
	}

	/// <summary>
	/// Rewrites a <c>SearchQueryService</c> response: hits whose versions are entirely filtered out
	/// are removed, surviving hits have their @id, registration, and per-version URLs pointed at Heimdall,
	/// and the primary version is set to the last surviving entry.
	/// </summary>
	/// <param name="upstream">Raw upstream search result.</param>
	/// <param name="feed">Feed configuration whose filter rules and name apply.</param>
	/// <returns>Serialized JSON of the rewritten search result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
	public string RewriteSearch(SearchResult upstream, FeedConfig feed)
	{
		ArgumentNullException.ThrowIfNull(upstream);
		ArgumentNullException.ThrowIfNull(feed);

		var passedHits = new List<SearchHit>();
		foreach (var hit in upstream.Data)
		{
			var hitMetas = HitToMetadata(hit);
			var passed = new HashSet<string>(
				_filter.Apply(hitMetas, feed, _time.GetUtcNow()).Select(m => m.Coords.Version.ToString()),
				StringComparer.OrdinalIgnoreCase);

			var keptVersions = hit.Versions.Where(v => passed.Contains(v.Version)).ToList();
			if (keptVersions.Count == 0)
			{
				continue;
			}

			// Search hits arrive ordered with the newest version last, so the last surviving entry
			// is the best candidate for the hit's primary "version" field.
			var primary = keptVersions[^1].Version;
			passedHits.Add(new SearchHit
			{
				Id = _urls.RegistrationIndex(feed.Name, hit.PackageId).ToString(),
				PackageId = hit.PackageId,
				Version = primary,
				Registration = _urls.RegistrationIndex(feed.Name, hit.PackageId).ToString(),
				Description = hit.Description,
				Versions = keptVersions.Select(v => new SearchVersion
				{
					Version = v.Version,
					Downloads = v.Downloads,
					Id = _urls.RegistrationLeaf(feed.Name, hit.PackageId, v.Version).ToString(),
				}).ToList(),
			});
		}

		var rewritten = new SearchResult
		{
			TotalHits = passedHits.Count,
			Data = passedHits,
		};

		return JsonSerializer.Serialize(rewritten, JsonOptions);
	}

	private static List<PackageVersionMetadata> HitToMetadata(SearchHit hit)
	{
		var result = new List<PackageVersionMetadata>();
		foreach (var v in hit.Versions)
		{
			if (!Semver.SemVersion.TryParse(v.Version, Semver.SemVersionStyles.Any, out var sv))
			{
				continue;
			}
			// Search hits do not carry per-version publish timestamps; age-based filters that
			// require Published will treat these as unknown.
			result.Add(new PackageVersionMetadata(
				new PackageCoordinates("nuget", hit.PackageId, sv),
				Published: null,
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
