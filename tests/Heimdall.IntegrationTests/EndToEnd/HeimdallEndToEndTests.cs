using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Heimdall.IntegrationTests.EndToEnd;

public class HeimdallEndToEndTests : IDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

	private readonly WireMockServer _upstream = WireMockServer.Start();
	private readonly string _tempDir;
	private readonly string _yamlPath;

	public HeimdallEndToEndTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "heimdall-e2e-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
		_yamlPath = Path.Combine(_tempDir, "heimdall.yaml");

		File.WriteAllText(_yamlPath, $$"""
			heimdall:
			  server:
			    publicBaseUrl: "https://heimdall.local"
			  ecosystems:
			    nuget:
			      feeds:
			        - name: strict
			          upstream: "{{_upstream.Url}}/v3/index.json"
			          rules:
			            - type: minAgeDays
			              days: "14"
			""");

		ConfigureUpstream();
	}

	public void Dispose()
	{
		_upstream.Dispose();
		try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
	}

	private void ConfigureUpstream()
	{
		_upstream.Given(Request.Create().WithPath("/v3/index.json").UsingGet())
			.RespondWith(Response.Create()
				.WithStatusCode(200)
				.WithHeader("Content-Type", "application/json")
				.WithBody($$"""
				{
				  "version": "3.0.0",
				  "resources": [
					{ "@id": "{{_upstream.Url}}/v3/registrations/", "@type": "RegistrationsBaseUrl/3.6.0" },
					{ "@id": "{{_upstream.Url}}/v3/flatcontainer/", "@type": "PackageBaseAddress/3.0.0" }
				  ]
				}
				"""));

		_upstream.Given(Request.Create().WithPath("/v3/registrations/foo.bar/index.json").UsingGet())
			.RespondWith(Response.Create()
				.WithStatusCode(200)
				.WithHeader("Content-Type", "application/json")
				.WithBody($$"""
				{
				  "@id": "{{_upstream.Url}}/v3/registrations/foo.bar/index.json",
				  "count": 1,
				  "items": [
					{
					  "count": 2,
					  "items": [
						{
						  "catalogEntry": {
							"id": "Foo.Bar",
							"version": "1.0.0",
							"published": "2026-01-01T00:00:00+00:00",
							"listed": true
						  },
						  "packageContent": "{{_upstream.Url}}/v3/flatcontainer/foo.bar/1.0.0/foo.bar.1.0.0.nupkg"
						},
						{
						  "catalogEntry": {
							"id": "Foo.Bar",
							"version": "2.0.0",
							"published": "2026-05-04T00:00:00+00:00",
							"listed": true
						  },
						  "packageContent": "{{_upstream.Url}}/v3/flatcontainer/foo.bar/2.0.0/foo.bar.2.0.0.nupkg"
						}
					  ]
					}
				  ]
				}
				"""));

		_upstream.Given(Request.Create()
			.WithPath("/v3/flatcontainer/foo.bar/1.0.0/foo.bar.1.0.0.nupkg").UsingGet())
			.RespondWith(Response.Create()
				.WithStatusCode(200)
				.WithHeader("Content-Type", "application/octet-stream")
				.WithBody(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xAA, 0xBB, 0xCC, 0xDD }));

		_upstream.Given(Request.Create()
			.WithPath("/v3/flatcontainer/foo.bar/2.0.0/foo.bar.2.0.0.nupkg").UsingGet())
			.RespondWith(Response.Create()
				.WithStatusCode(200)
				.WithHeader("Content-Type", "application/octet-stream")
				.WithBody(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x11, 0x22, 0x33, 0x44 }));
	}

	private WebApplicationFactory<Program> CreateFactory() =>
		new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
		{
			builder.UseContentRoot(_tempDir);
			builder.ConfigureAppConfiguration((_, cfg) =>
			{
				cfg.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["heimdall:server:publicBaseUrl"] = "https://heimdall.local",
					["heimdall:ecosystems:nuget:feeds:0:name"] = "strict",
					["heimdall:ecosystems:nuget:feeds:0:upstream"] = $"{_upstream.Url}/v3/index.json",
					["heimdall:ecosystems:nuget:feeds:0:rules:0:type"] = "minAgeDays",
					["heimdall:ecosystems:nuget:feeds:0:rules:0:days"] = "14",
				});
			});
			builder.ConfigureLogging(l =>
			{
				l.ClearProviders();
				l.AddSimpleConsole(o => o.SingleLine = true);
				l.SetMinimumLevel(LogLevel.Debug);
			});
			builder.ConfigureServices(services =>
			{
				services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
			});
		});

	[Fact]
	public async Task Service_index_returns_heimdall_urls()
	{
		using var factory = CreateFactory();
		using var client = factory.CreateClient();

		var resp = await client.GetAsync("/nuget/strict/v3/index.json");
		resp.StatusCode.Should().Be(HttpStatusCode.OK);

		var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
		var resources = doc!.RootElement.GetProperty("resources").EnumerateArray().ToList();
		resources.Should().Contain(r =>
			r.GetProperty("@type").GetString() == "RegistrationsBaseUrl/3.6.0"
			&& r.GetProperty("@id").GetString() == "https://heimdall.local/nuget/strict/v3/registration5-gz-semver2/");
	}

	[Fact]
	public async Task Versions_list_filters_recent_version()
	{
		using var factory = CreateFactory();
		using var client = factory.CreateClient();

		var resp = await client.GetAsync("/nuget/strict/v3/flatcontainer/foo.bar/index.json");
		resp.StatusCode.Should().Be(HttpStatusCode.OK);

		var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
		var versions = doc!.RootElement.GetProperty("versions").EnumerateArray()
			.Select(v => v.GetString()).ToList();

		versions.Should().BeEquivalentTo(["1.0.0"]);
	}

	[Fact]
	public async Task Download_blocked_version_returns_403_problem_details()
	{
		using var factory = CreateFactory();
		using var client = factory.CreateClient();

		var resp = await client.GetAsync(
			"/nuget/strict/v3/flatcontainer/foo.bar/2.0.0/foo.bar.2.0.0.nupkg");

		resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		var body = await resp.Content.ReadAsStringAsync();
		body.Should().Contain("minAgeDays");
	}

	[Fact]
	public async Task Download_allowed_version_streams_bytes()
	{
		using var factory = CreateFactory();
		using var client = factory.CreateClient();

		var resp = await client.GetAsync(
			"/nuget/strict/v3/flatcontainer/foo.bar/1.0.0/foo.bar.1.0.0.nupkg");

		resp.StatusCode.Should().Be(HttpStatusCode.OK);
		var bytes = await resp.Content.ReadAsByteArrayAsync();
		bytes.Should().BeEquivalentTo(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xAA, 0xBB, 0xCC, 0xDD });
	}

	[Fact]
	public async Task Healthz_returns_200()
	{
		using var factory = CreateFactory();
		using var client = factory.CreateClient();

		var resp = await client.GetAsync("/healthz");
		resp.StatusCode.Should().Be(HttpStatusCode.OK);
	}


	[Fact]
	public async Task Unknown_feed_returns_404()
	{
		using var factory = CreateFactory();
		using var client = factory.CreateClient();

		var resp = await client.GetAsync("/nuget/no-such-feed/v3/index.json");
		resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	private sealed class FixedTimeProvider : TimeProvider
	{
		private readonly DateTimeOffset _now;
		public FixedTimeProvider(DateTimeOffset now) => _now = now;
		public override DateTimeOffset GetUtcNow() => _now;
	}
}
