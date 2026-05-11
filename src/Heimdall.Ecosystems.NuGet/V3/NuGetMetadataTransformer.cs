using System.Text.Json;
using System.Text.Json.Nodes;
using Heimdall.Application.Filtering;
using Heimdall.Domain.Configuration;
using Heimdall.Domain.Packages;
using Heimdall.Ecosystems.NuGet.V3.Models;

namespace Heimdall.Ecosystems.NuGet.V3;

public sealed class NuGetMetadataTransformer
{
	private readonly NuGetUrlRewriter _urls;
	private readonly IVersionListFilter _filter;
	private readonly TimeProvider _time;

	public NuGetMetadataTransformer(NuGetUrlRewriter urls, IVersionListFilter filter, TimeProvider time)
	{
		ArgumentNullException.ThrowIfNull(urls);
		ArgumentNullException.ThrowIfNull(filter);
		ArgumentNullException.ThrowIfNull(time);
		_urls = urls;
		_filter = filter;
		_time = time;
	}

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
