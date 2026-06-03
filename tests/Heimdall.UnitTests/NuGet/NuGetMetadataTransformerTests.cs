using System.Text.Json;
using Heimdall.Core.DependencyInjection;
using Heimdall.Core.Filtering;
using Heimdall.Core.Configuration;
using Heimdall.Core.Packages;
using Heimdall.Ecosystems.NuGet.V3;
using Heimdall.Ecosystems.NuGet.V3.Models;
using Microsoft.Extensions.DependencyInjection;
using Semver;

namespace Heimdall.UnitTests.NuGet;

public class NuGetV3MetadataTransformerTests
{
	private static readonly DateTimeOffset Now = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

	private static (NuGetV3MetadataTransformer, FeedConfig) Build(int minAgeDays)
	{
		var sp = new ServiceCollection().AddHeimdallCore().BuildServiceProvider();
		var transformer = new NuGetV3MetadataTransformer(
			new NuGetV3UrlRewriter(new Uri("https://heimdall.local/")),
			sp.GetRequiredService<IVersionListFilter>(),
			new FixedTimeProvider(Now));

		var feed = new FeedConfig(
			"nuget", "strict",
			new Uri("https://api.nuget.org/v3/index.json"),
			[
				new RuleConfig("minAgeDays", new Dictionary<string, string?> { ["days"] = minAgeDays.ToString() }),
			],
			null);

		return (transformer, feed);
	}

	private static IReadOnlyList<IRule> RulesFor(FeedConfig feed)
	{
		var sp = new ServiceCollection().AddHeimdallCore().BuildServiceProvider();
		return sp.GetRequiredService<IRuleFactory>().BuildRules(feed.Rules);
	}

	private static RegistrationIndexV3 SampleRegistration() => new()
	{
		Id = "https://upstream/registration/newtonsoft.json/index.json",
		Count = 1,
		Items =
		[
			new RegistrationPageV3
			{
				Count = 3,
				Items =
				[
					Leaf("Newtonsoft.Json", "12.0.3", Now.AddDays(-30)),
					Leaf("Newtonsoft.Json", "13.0.0", Now.AddDays(-7)),
					Leaf("Newtonsoft.Json", "13.0.3", Now.AddDays(-3)),
				],
			},
		],
	};

	private static RegistrationLeafV3 Leaf(string id, string version, DateTimeOffset published) => new()
	{
		Id = $"https://upstream/registration/{id.ToLowerInvariant()}/{version}.json",
		CatalogEntryV3 = new CatalogEntryV3
		{
			PackageId = id,
			Version = version,
			PublishedUtc = published,
			Listed = true,
			PackageContent = $"https://upstream/p/{id.ToLowerInvariant()}/{version}/{id.ToLowerInvariant()}.{version}.nupkg",
		},
		PackageContent = $"https://upstream/p/{id.ToLowerInvariant()}/{version}/{id.ToLowerInvariant()}.{version}.nupkg",
	};

	[Fact]
	public void Versions_list_filters_by_min_age()
	{
		var (transformer, feed) = Build(minAgeDays: 14);

		var json = transformer.BuildVersionsListJson(SampleRegistration(), feed);

		json.Should().NotBeNull();
		using var doc = JsonDocument.Parse(json!);
		var versions = doc.RootElement.GetProperty("versions").EnumerateArray()
			.Select(v => v.GetString()).ToList();

		versions.Should().BeEquivalentTo(["12.0.3"]);
	}

	[Fact]
	public void Registration_rewrite_drops_filtered_versions_and_rewrites_urls()
	{
		var (transformer, feed) = Build(minAgeDays: 14);

		var json = transformer.RewriteRegistration(SampleRegistration(), feed);

		json.Should().NotBeNull();
		using var doc = JsonDocument.Parse(json!);
		doc.RootElement.GetProperty("@id").GetString()
			.Should().Be("https://heimdall.local/nuget/strict/v3/registration5-gz-semver2/newtonsoft.json/index.json");
		doc.RootElement.GetProperty("count").GetInt32().Should().Be(1);

		var leaves = doc.RootElement.GetProperty("items")[0].GetProperty("items").EnumerateArray().ToList();
		leaves.Should().HaveCount(1);
		var leaf = leaves[0];
		leaf.GetProperty("@id").GetString().Should().StartWith("https://heimdall.local/nuget/strict/");
		leaf.GetProperty("packageContent").GetString()
			.Should().Be("https://heimdall.local/nuget/strict/v3/flatcontainer/newtonsoft.json/12.0.3/newtonsoft.json.12.0.3.nupkg");
		leaf.GetProperty("catalogEntry").GetProperty("packageContent").GetString()
			.Should().StartWith("https://heimdall.local/nuget/strict/v3/flatcontainer/");
	}

	[Fact]
	public void All_versions_filtered_yields_null_versions_list()
	{
		var (transformer, feed) = Build(minAgeDays: 999_999);

		// Every version filtered out → null so the controller returns 404 instead of an empty list.
		transformer.BuildVersionsListJson(SampleRegistration(), feed).Should().BeNull();
	}

	[Fact]
	public void All_versions_filtered_yields_null_registration()
	{
		var (transformer, feed) = Build(minAgeDays: 999_999);

		transformer.RewriteRegistration(SampleRegistration(), feed).Should().BeNull();
	}

	[Fact]
	public void Versions_list_is_ordered_by_semver_not_lexicographically()
	{
		var (transformer, feed) = Build(minAgeDays: 0);
		var registration = RegistrationOf(
			"Pkg", ("10.0.0", 30), ("2.0.0", 30), ("1.0.0", 30));

		var json = transformer.BuildVersionsListJson(registration, feed);

		json.Should().NotBeNull();
		using var doc = JsonDocument.Parse(json!);
		var versions = doc.RootElement.GetProperty("versions").EnumerateArray()
			.Select(v => v.GetString()).ToList();

		versions.Should().ContainInOrder("1.0.0", "2.0.0", "10.0.0");
	}

	[Fact]
	public void Registration_lower_and_upper_are_semver_min_and_max()
	{
		var (transformer, feed) = Build(minAgeDays: 0);
		var registration = RegistrationOf(
			"Pkg", ("10.0.0", 30), ("2.0.0", 30), ("1.0.0", 30));

		var json = transformer.RewriteRegistration(registration, feed);

		json.Should().NotBeNull();
		using var doc = JsonDocument.Parse(json!);
		var page = doc.RootElement.GetProperty("items")[0];
		page.GetProperty("lower").GetString().Should().Be("1.0.0");
		page.GetProperty("upper").GetString().Should().Be("10.0.0");
	}

	[Fact]
	public void Search_recomputes_primary_as_latest_stable_among_survivors()
	{
		var (transformer, feed) = Build(minAgeDays: 14);
		var hit = new SearchHitV3
		{
			PackageId = "Foo",
			Version = "3.0.0",
			Versions =
			[
				new SearchVersionV3 { Version = "1.0.0", Downloads = 10 },
				new SearchVersionV3 { Version = "2.0.0", Downloads = 20 },
				new SearchVersionV3 { Version = "2.1.0-rc", Downloads = 5 },
				new SearchVersionV3 { Version = "3.0.0", Downloads = 1 },
			],
		};
		var result = new SearchResultV3 { TotalHits = 1, Data = [hit] };

		// Enriched dates: 3.0.0 is too new (2 days) and is filtered; the prerelease is older but stays.
		var enriched = EnrichedFor(
			"Foo", ("1.0.0", 100), ("2.0.0", 50), ("2.1.0-rc", 40), ("3.0.0", 2));

		var json = transformer.RewriteSearch(result, feed, RulesFor(feed), includePrerelease: false, enriched);

		using var doc = JsonDocument.Parse(json);
		var data = doc.RootElement.GetProperty("data");
		data.GetArrayLength().Should().Be(1);
		var entry = data[0];

		// Latest is the highest stable survivor (2.0.0), not the prerelease and not the filtered 3.0.0.
		entry.GetProperty("version").GetString().Should().Be("2.0.0");
		var versions = entry.GetProperty("versions").EnumerateArray()
			.Select(v => v.GetProperty("version").GetString()).ToList();
		versions.Should().ContainInOrder("1.0.0", "2.0.0", "2.1.0-rc");
		versions.Should().NotContain("3.0.0");
	}

	[Fact]
	public void Search_without_enrichment_drops_hit_under_date_rule()
	{
		var (transformer, feed) = Build(minAgeDays: 14);
		var hit = new SearchHitV3
		{
			PackageId = "Foo",
			Version = "1.0.0",
			Versions = [new SearchVersionV3 { Version = "1.0.0", Downloads = 10 }],
		};
		var result = new SearchResultV3 { TotalHits = 1, Data = [hit] };

		// No enrichment → date-less metadata → minAgeDays denies everything → hit dropped.
		var json = transformer.RewriteSearch(result, feed, RulesFor(feed));

		using var doc = JsonDocument.Parse(json);
		doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
	}

	[Fact]
	public void Search_with_prerelease_allows_prerelease_as_primary()
	{
		var (transformer, feed) = Build(minAgeDays: 14);
		var hit = new SearchHitV3
		{
			PackageId = "Foo",
			Version = "2.1.0-rc",
			Versions =
			[
				new SearchVersionV3 { Version = "1.0.0", Downloads = 1 },
				new SearchVersionV3 { Version = "2.0.0", Downloads = 1 },
				new SearchVersionV3 { Version = "2.1.0-rc", Downloads = 1 },
			],
		};
		var result = new SearchResultV3 { TotalHits = 1, Data = [hit] };
		var enriched = EnrichedFor("Foo", ("1.0.0", 100), ("2.0.0", 50), ("2.1.0-rc", 40));

		var json = transformer.RewriteSearch(result, feed, RulesFor(feed), includePrerelease: true, enriched);

		using var doc = JsonDocument.Parse(json);
		// With prerelease requested, the newest survivor (the prerelease) is the primary.
		doc.RootElement.GetProperty("data")[0].GetProperty("version").GetString().Should().Be("2.1.0-rc");
	}

	[Fact]
	public void Search_preserves_upstream_total_hits_when_a_hit_is_filtered_out()
	{
		var (transformer, feed) = Build(minAgeDays: 14);
		var foo = new SearchHitV3
		{
			PackageId = "Foo", Version = "1.0.0",
			Versions = [new SearchVersionV3 { Version = "1.0.0", Downloads = 1 }],
		};
		var bar = new SearchHitV3
		{
			PackageId = "Bar", Version = "9.0.0",
			Versions = [new SearchVersionV3 { Version = "9.0.0", Downloads = 1 }],
		};
		var result = new SearchResultV3 { TotalHits = 5000, Data = [foo, bar] };
		var enriched = new Dictionary<string, IReadOnlyList<PackageVersionMetadata>>(StringComparer.OrdinalIgnoreCase)
		{
			["Foo"] = MetasFor("Foo", ("1.0.0", 100)),
			["Bar"] = MetasFor("Bar", ("9.0.0", 1)),   // too new → fully filtered
		};

		var json = transformer.RewriteSearch(result, feed, RulesFor(feed), includePrerelease: false, enriched);

		using var doc = JsonDocument.Parse(json);
		// Bar dropped from the page, but the global total is preserved so paging still works.
		doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(1);
		doc.RootElement.GetProperty("totalHits").GetInt32().Should().Be(5000);
	}

	[Fact]
	public void Search_keeps_non_canonical_version_strings()
	{
		var (transformer, feed) = Build(minAgeDays: 14);
		var hit = new SearchHitV3
		{
			PackageId = "Foo", Version = "1.0",
			Versions = [new SearchVersionV3 { Version = "1.0", Downloads = 1 }],
		};
		var result = new SearchResultV3 { TotalHits = 1, Data = [hit] };
		// Registration spells it "1.0.0"; the hit spells it "1.0" — they must still match.
		var enriched = EnrichedFor("Foo", ("1.0.0", 100));

		var json = transformer.RewriteSearch(result, feed, RulesFor(feed), includePrerelease: false, enriched);

		using var doc = JsonDocument.Parse(json);
		var entry = doc.RootElement.GetProperty("data")[0];
		entry.GetProperty("version").GetString().Should().Be("1.0");
		entry.GetProperty("versions").EnumerateArray()
			.Select(v => v.GetProperty("version").GetString()).Should().ContainInOrder("1.0");
	}

	[Fact]
	public void Registration_keeps_non_canonical_version_strings()
	{
		var (transformer, feed) = Build(minAgeDays: 0);
		// Upstream leaf carries a non-canonical "1.0"; it must survive (not be dropped to a 404).
		var registration = RegistrationOf("Pkg", ("1.0", 30));

		var json = transformer.RewriteRegistration(registration, feed);

		json.Should().NotBeNull();
		using var doc = JsonDocument.Parse(json!);
		var leaves = doc.RootElement.GetProperty("items")[0].GetProperty("items").EnumerateArray().ToList();
		leaves.Should().HaveCount(1);
		leaves[0].GetProperty("catalogEntry").GetProperty("version").GetString().Should().Be("1.0");
	}

	private static RegistrationIndexV3 RegistrationOf(string id, params (string Version, int AgeDays)[] versions) => new()
	{
		Count = 1,
		Items =
		[
			new RegistrationPageV3
			{
				Count = versions.Length,
				Items = versions.Select(v => Leaf(id, v.Version, Now.AddDays(-v.AgeDays))).ToList(),
			},
		],
	};

	private static IReadOnlyList<PackageVersionMetadata> MetasFor(
		string id, params (string Version, int AgeDays)[] versions) => versions
			.Select(v => PackageVersionMetadata.Create(
				new PackageCoordinates("nuget", id, SemVersion.Parse(v.Version, SemVersionStyles.Any)),
				Now.AddDays(-v.AgeDays)))
			.ToList();

	private static IReadOnlyDictionary<string, IReadOnlyList<PackageVersionMetadata>> EnrichedFor(
		string id, params (string Version, int AgeDays)[] versions) =>
		new Dictionary<string, IReadOnlyList<PackageVersionMetadata>>(StringComparer.OrdinalIgnoreCase)
		{
			[id] = MetasFor(id, versions),
		};

	private sealed class FixedTimeProvider : TimeProvider
	{
		private readonly DateTimeOffset _now;
		public FixedTimeProvider(DateTimeOffset now) => _now = now;
		public override DateTimeOffset GetUtcNow() => _now;
	}
}
