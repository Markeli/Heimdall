using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Heimdall.SmokeTests;

/// <summary>
/// End-to-end smoke tests that exercise Heimdall against the real <c>api.nuget.org</c>
/// upstream through the production <c>config.yml</c>'s <c>relaxed</c> feed (minAgeDays=1).
/// All assertions are deliberately written as containment checks rather than exact-equals,
/// because the visible version set drifts as new releases ship.
/// </summary>
public sealed class NuGetSmokeTests : IClassFixture<NuGetSmokeTests.PingFixture>
{
	private const string Feed = "relaxed";
	private const string KnownPackage = "Newtonsoft.Json";
	private const string KnownPackageLower = "newtonsoft.json";
	private const string KnownStableVersion = "13.0.3";

	[Fact]
	public async Task Healthz_returns_200()
	{
		using var http = SmokeEnvironment.CreateClient();
		using var resp = await Retry.GetAsync(http, "/healthz");
		resp.StatusCode.Should().Be(HttpStatusCode.OK);
		(await resp.Content.ReadAsStringAsync()).Should().Be("ok");
	}

	[Fact]
	public async Task Readyz_returns_200_when_upstream_reachable()
	{
		using var http = SmokeEnvironment.CreateClient();
		using var resp = await Retry.GetAsync(http, "/readyz");
		resp.StatusCode.Should().Be(HttpStatusCode.OK,
			"smoke CI runs only after `wait-for-readyz` so readiness must already be green");
	}

	[Fact]
	public async Task Service_index_rewrites_urls_to_heimdall()
	{
		using var http = SmokeEnvironment.CreateClient();
		using var resp = await Retry.GetAsync(http, $"/nuget/{Feed}/v3/index.json");
		resp.StatusCode.Should().Be(HttpStatusCode.OK);

		using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
		var resources = doc.RootElement.GetProperty("resources").EnumerateArray().ToList();
		resources.Should().NotBeEmpty();

		// Every rewritten resource @id must point at the proxy, not at api.nuget.org.
		foreach (var r in resources)
		{
			var id = r.GetProperty("@id").GetString();
			id.Should().StartWith(SmokeEnvironment.BaseUrl, "Heimdall must rewrite upstream URLs");
		}
	}

	[Fact]
	public async Task Versions_list_contains_known_stable_version()
	{
		using var http = SmokeEnvironment.CreateClient();
		using var resp = await Retry.GetAsync(
			http, $"/nuget/{Feed}/v3/flatcontainer/{KnownPackageLower}/index.json");

		resp.StatusCode.Should().Be(HttpStatusCode.OK);

		using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
		var versions = doc.RootElement.GetProperty("versions").EnumerateArray()
			.Select(v => v.GetString()).ToList();

		versions.Should().Contain(KnownStableVersion,
			$"{KnownPackage} {KnownStableVersion} shipped in 2023 and must survive minAgeDays=1");
	}

	[Fact]
	public async Task Registration_index_returns_200_for_known_package()
	{
		using var http = SmokeEnvironment.CreateClient();
		using var resp = await Retry.GetAsync(
			http, $"/nuget/{Feed}/v3/registration5-gz-semver2/{KnownPackageLower}/index.json");

		resp.StatusCode.Should().Be(HttpStatusCode.OK);

		using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
		doc.RootElement.GetProperty("count").GetInt32().Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task Search_returns_known_package()
	{
		using var http = SmokeEnvironment.CreateClient();
		using var resp = await Retry.GetAsync(
			http, $"/nuget/{Feed}/v3/query?q={KnownPackage}&take=10");

		resp.StatusCode.Should().Be(HttpStatusCode.OK);

		using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
		var ids = doc.RootElement.GetProperty("data").EnumerateArray()
			.Select(e => e.GetProperty("id").GetString())
			.ToList();

		ids.Should().Contain(id => string.Equals(id, KnownPackage, StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Download_streams_a_real_nupkg()
	{
		using var http = SmokeEnvironment.CreateClient();
		var path = $"/nuget/{Feed}/v3/flatcontainer/{KnownPackageLower}/{KnownStableVersion}"
			+ $"/{KnownPackageLower}.{KnownStableVersion}.nupkg";
		using var resp = await Retry.GetAsync(http, path);

		resp.StatusCode.Should().Be(HttpStatusCode.OK);
		var bytes = await resp.Content.ReadAsByteArrayAsync();
		bytes.Length.Should().BeGreaterThan(1024, "a real .nupkg is far bigger than 1 KiB");
		// ZIP local-file-header magic: 50 4B 03 04
		bytes.Take(4).Should().BeEquivalentTo(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
	}

	[Fact]
	public async Task Head_a_real_nupkg_returns_200()
	{
		using var http = SmokeEnvironment.CreateClient();
		var path = $"/nuget/{Feed}/v3/flatcontainer/{KnownPackageLower}/{KnownStableVersion}"
			+ $"/{KnownPackageLower}.{KnownStableVersion}.nupkg";
		using var req = new HttpRequestMessage(HttpMethod.Head, path);
		using var resp = await http.SendAsync(req);
		resp.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Unknown_feed_returns_404()
	{
		using var http = SmokeEnvironment.CreateClient();
		using var resp = await http.GetAsync("/nuget/no-such-feed/v3/index.json");
		resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	/// <summary>
	/// Sanity ping run once before any test, gives a clearer failure message than a per-test
	/// connection refused. The CI job already waits on /readyz, so this is belt-and-braces.
	/// </summary>
	public sealed class PingFixture
	{
		public PingFixture()
		{
			using var http = SmokeEnvironment.CreateClient();
			using var resp = http.GetAsync("/healthz").GetAwaiter().GetResult();
			if (resp.StatusCode != HttpStatusCode.OK)
			{
				throw new InvalidOperationException(
					$"Heimdall at {SmokeEnvironment.BaseUrl} returned {(int)resp.StatusCode} on /healthz. "
					+ "Smoke tests require a live container.");
			}
		}
	}
}
