using System.Net;
using System.Text.Json;

namespace Heimdall.SmokeTests;

/// <summary>
/// Smoke coverage for the policy filter rules (<c>allowDeny</c>, <c>minAgeDays</c>) against the
/// real <c>api.nuget.org</c> upstream. The feeds exercised here are defined in
/// <c>tests/Heimdall.SmokeTests/config.smoke.yml</c>, which release CI bind-mounts over
/// <c>/app/config.yml</c> in the container under test. For every rule we assert *both*
/// channels through which the filter must enforce its verdict: the version listing must
/// drop the blocked versions, and a direct <c>.nupkg</c> download for those versions must
/// return <c>403 Forbidden</c>.
/// </summary>
public sealed class FilteringSmokeTests : IClassFixture<NuGetSmokeTests.PingFixture>
{
	// Newtonsoft.Json 13.0.3 was published 2023-03-08. As of the foreseeable future of this
	// repo it is older than every minAgeDays threshold short of `99999` and survives any
	// `Newtonsoft.*` allow pattern, which makes it the canonical "real but easily targeted"
	// package for these tests.
	private const string TargetPackage = "Newtonsoft.Json";
	private const string TargetPackageLower = "newtonsoft.json";
	private const string TargetVersion = "13.0.3";

	// Dapper is widely mirrored, has many shipped versions, and crucially does not match the
	// `Newtonsoft.*` allow pattern — so it is the right "control" package for the allow-list
	// blocking case.
	private const string ControlPackageLower = "dapper";

	[Fact]
	public async Task AllowList_passes_matching_package()
	{
		using var http = SmokeEnvironment.CreateClient();
		var versions = await GetVersionsAsync(http, "allow-newtonsoft", TargetPackageLower);
		versions.Should().Contain(TargetVersion,
			"Newtonsoft.Json matches the `Newtonsoft.*` allow pattern, so its versions must flow");
	}

	[Fact]
	public async Task AllowList_blocks_nonmatching_listing()
	{
		using var http = SmokeEnvironment.CreateClient();
		var versions = await GetVersionsAsync(http, "allow-newtonsoft", ControlPackageLower);
		versions.Should().BeEmpty(
			"Dapper does not match the `Newtonsoft.*` allow pattern, so listing must be empty");
	}

	[Fact]
	public async Task AllowList_blocks_nonmatching_download()
	{
		using var http = SmokeEnvironment.CreateClient();

		// Discover a real Dapper version through the unrestricted `relaxed` feed instead of
		// hardcoding one — Dapper publishes regularly and any pinned version risks rotting.
		var dapperVersions = await GetVersionsAsync(http, "relaxed", ControlPackageLower);
		dapperVersions.Should().NotBeEmpty("smoke depends on relaxed surfacing real Dapper versions");
		var v = dapperVersions[0];

		var path = $"/nuget/allow-newtonsoft/v3/flatcontainer/{ControlPackageLower}/{v}"
			+ $"/{ControlPackageLower}.{v}.nupkg";
		using var resp = await Retry.GetAsync(http, path);
		resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
			"Dapper does not match the `Newtonsoft.*` allow pattern, so download must be denied");
	}

	[Fact]
	public async Task DenyPattern_allows_nonmatching_package()
	{
		using var http = SmokeEnvironment.CreateClient();
		var versions = await GetVersionsAsync(http, "deny-newtonsoft", ControlPackageLower);
		versions.Should().NotBeEmpty(
			"Dapper does not match `!Newtonsoft.*`, so deny-only configuration must let it through");
	}

	[Fact]
	public async Task DenyPattern_blocks_matching_listing()
	{
		using var http = SmokeEnvironment.CreateClient();
		var versions = await GetVersionsAsync(http, "deny-newtonsoft", TargetPackageLower);
		versions.Should().BeEmpty(
			"Newtonsoft.Json matches `!Newtonsoft.*`, so every version must be filtered from listing");
	}

	[Fact]
	public async Task DenyPattern_blocks_matching_download()
	{
		using var http = SmokeEnvironment.CreateClient();
		var path = $"/nuget/deny-newtonsoft/v3/flatcontainer/{TargetPackageLower}/{TargetVersion}"
			+ $"/{TargetPackageLower}.{TargetVersion}.nupkg";
		using var resp = await Retry.GetAsync(http, path);
		resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
			"Newtonsoft.Json matches `!Newtonsoft.*`, so direct download must be denied");
	}

	[Fact]
	public async Task MinAgeDays_blocks_listing_when_threshold_unreachable()
	{
		using var http = SmokeEnvironment.CreateClient();
		var versions = await GetVersionsAsync(http, "age-locked", TargetPackageLower);
		versions.Should().BeEmpty(
			"age-locked requires minAgeDays=99999; no real package is that old, listing must be empty");
	}

	[Fact]
	public async Task MinAgeDays_blocks_download_when_threshold_unreachable()
	{
		using var http = SmokeEnvironment.CreateClient();
		var path = $"/nuget/age-locked/v3/flatcontainer/{TargetPackageLower}/{TargetVersion}"
			+ $"/{TargetPackageLower}.{TargetVersion}.nupkg";
		using var resp = await Retry.GetAsync(http, path);
		resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
			"Newtonsoft.Json 13.0.3 (2023) is far younger than 99999 days, download must be denied");
	}

	private static async Task<List<string>> GetVersionsAsync(HttpClient http, string feed, string pkgLower)
	{
		using var resp = await Retry.GetAsync(http, $"/nuget/{feed}/v3/flatcontainer/{pkgLower}/index.json");
		// When upstream genuinely has no such package, controller returns 404. When the package
		// exists but every version is filtered, the controller returns 200 with versions:[].
		// Both states satisfy "the feed is not serving this package", so the tests collapse them.
		if (resp.StatusCode == HttpStatusCode.NotFound)
		{
			return [];
		}
		resp.StatusCode.Should().Be(HttpStatusCode.OK,
			$"versions listing for {pkgLower} on '{feed}' should be 200 or 404, got {(int)resp.StatusCode}");
		using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
		return doc.RootElement.GetProperty("versions").EnumerateArray()
			.Select(v => v.GetString() ?? string.Empty)
			.Where(v => v.Length > 0)
			.ToList();
	}
}
