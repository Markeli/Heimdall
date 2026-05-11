using System.Text.Json;
using Heimdall.Core.DependencyInjection;
using Heimdall.Core.Filtering;
using Heimdall.Core.Configuration;
using Heimdall.Ecosystems.NuGet.V3;
using Heimdall.Ecosystems.NuGet.V3.Models;
using Microsoft.Extensions.DependencyInjection;

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

		using var doc = JsonDocument.Parse(json);
		var versions = doc.RootElement.GetProperty("versions").EnumerateArray()
			.Select(v => v.GetString()).ToList();

		versions.Should().BeEquivalentTo(["12.0.3"]);
	}

	[Fact]
	public void Registration_rewrite_drops_filtered_versions_and_rewrites_urls()
	{
		var (transformer, feed) = Build(minAgeDays: 14);

		var json = transformer.RewriteRegistration(SampleRegistration(), feed);

		using var doc = JsonDocument.Parse(json);
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
	public void All_versions_filtered_yields_empty_versions()
	{
		var (transformer, feed) = Build(minAgeDays: 999_999);

		var json = transformer.BuildVersionsListJson(SampleRegistration(), feed);

		using var doc = JsonDocument.Parse(json);
		doc.RootElement.GetProperty("versions").GetArrayLength().Should().Be(0);
	}

	private sealed class FixedTimeProvider : TimeProvider
	{
		private readonly DateTimeOffset _now;
		public FixedTimeProvider(DateTimeOffset now) => _now = now;
		public override DateTimeOffset GetUtcNow() => _now;
	}
}
