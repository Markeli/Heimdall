using Heimdall.Ecosystems.NuGet.DependencyInjection;
using Heimdall.Ecosystems.NuGet.V3;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Heimdall.IntegrationTests.NuGet;

public class NuGetV3UpstreamClientTests : IDisposable
{
	private readonly WireMockServer _server = WireMockServer.Start();

	public void Dispose() => _server.Dispose();

	[Fact]
	public async Task Resolves_registration_url_from_service_index_and_parses_registration()
	{
		var serviceIndexUrl = $"{_server.Url}/v3/index.json";
		var registrationBaseUrl = $"{_server.Url}/v3/registrations/";

		_server.Given(Request.Create().WithPath("/v3/index.json").UsingGet())
			.RespondWith(Response.Create()
				.WithStatusCode(200)
				.WithHeader("Content-Type", "application/json")
				.WithBody($$"""
				{
				  "version": "3.0.0",
				  "resources": [
					{ "@id": "{{registrationBaseUrl}}", "@type": "RegistrationsBaseUrl/3.6.0" }
				  ]
				}
				"""));

		_server.Given(Request.Create().WithPath("/v3/registrations/newtonsoft.json/index.json").UsingGet())
			.RespondWith(Response.Create()
				.WithStatusCode(200)
				.WithHeader("Content-Type", "application/json")
				.WithBody("""
				{
				  "@id": "https://upstream/registration/newtonsoft.json",
				  "count": 1,
				  "items": [
					{
					  "count": 2,
					  "items": [
						{
						  "catalogEntry": {
							"id": "Newtonsoft.Json",
							"version": "12.0.3",
							"published": "2020-01-01T00:00:00+00:00",
							"listed": true
						  },
						  "packageContent": "https://upstream/p/newtonsoft.json/12.0.3/newtonsoft.json.12.0.3.nupkg"
						},
						{
						  "catalogEntry": {
							"id": "Newtonsoft.Json",
							"version": "13.0.3",
							"published": "2023-03-08T00:00:00+00:00",
							"listed": true
						  },
						  "packageContent": "https://upstream/p/newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg"
						}
					  ]
					}
				  ]
				}
				"""));

		await using var sp = BuildServices();
		var client = sp.GetRequiredService<INuGetV3UpstreamClient>();

		var index = await client.GetRegistrationAsync(new Uri(serviceIndexUrl), "Newtonsoft.Json", default);

		index.Should().NotBeNull();
		index!.Items.Should().HaveCount(1);
		var leaves = index.Items[0].Items!;
		leaves.Should().HaveCount(2);
		leaves[0].CatalogEntryV3!.PackageId.Should().Be("Newtonsoft.Json");
		leaves[0].CatalogEntryV3!.Version.Should().Be("12.0.3");
		leaves[0].CatalogEntryV3!.PublishedUtc.Should().NotBeNull();
	}

	[Fact]
	public async Task Inlines_external_registration_pages()
	{
		var serviceIndexUrl = $"{_server.Url}/v3/index.json";
		var registrationBaseUrl = $"{_server.Url}/v3/registrations/";
		var pageUrl = $"{_server.Url}/v3/registrations/big/page/1.0.0/2.0.0.json";

		_server.Given(Request.Create().WithPath("/v3/index.json").UsingGet())
			.RespondWith(Response.Create()
				.WithStatusCode(200)
				.WithHeader("Content-Type", "application/json")
				.WithBody($$"""
				{ "version": "3.0.0", "resources": [
					{ "@id": "{{registrationBaseUrl}}", "@type": "RegistrationsBaseUrl/3.6.0" } ] }
				"""));

		// Index page carries only an @id and omits its leaves (the large-package shape).
		_server.Given(Request.Create().WithPath("/v3/registrations/big/index.json").UsingGet())
			.RespondWith(Response.Create()
				.WithStatusCode(200)
				.WithHeader("Content-Type", "application/json")
				.WithBody($$"""
				{
				  "@id": "https://upstream/registration/big",
				  "count": 1,
				  "items": [ { "@id": "{{pageUrl}}", "count": 2, "lower": "1.0.0", "upper": "2.0.0" } ]
				}
				"""));

		_server.Given(Request.Create().WithPath("/v3/registrations/big/page/1.0.0/2.0.0.json").UsingGet())
			.RespondWith(Response.Create()
				.WithStatusCode(200)
				.WithHeader("Content-Type", "application/json")
				.WithBody("""
				{
				  "@id": "https://upstream/registration/big/page/1.0.0/2.0.0.json",
				  "count": 2,
				  "items": [
					{ "catalogEntry": { "id": "Big", "version": "1.0.0", "listed": true } },
					{ "catalogEntry": { "id": "Big", "version": "2.0.0", "listed": true } }
				  ]
				}
				"""));

		await using var sp = BuildServices();
		var client = sp.GetRequiredService<INuGetV3UpstreamClient>();

		var index = await client.GetRegistrationAsync(new Uri(serviceIndexUrl), "Big", default);

		index.Should().NotBeNull();
		// The external page's leaves were fetched and inlined rather than left null.
		var leaves = index!.Items[0].Items;
		leaves.Should().NotBeNull();
		leaves!.Select(l => l.CatalogEntryV3!.Version).Should().BeEquivalentTo(["1.0.0", "2.0.0"]);
	}

	[Fact]
	public async Task Returns_null_on_404()
	{
		var serviceIndexUrl = $"{_server.Url}/v3/index.json";
		var registrationBaseUrl = $"{_server.Url}/v3/registrations/";

		_server.Given(Request.Create().WithPath("/v3/index.json").UsingGet())
			.RespondWith(Response.Create()
				.WithStatusCode(200)
				.WithHeader("Content-Type", "application/json")
				.WithBody($$"""
				{ "version": "3.0.0", "resources": [
					{ "@id": "{{registrationBaseUrl}}", "@type": "RegistrationsBaseUrl/3.6.0" } ] }
				"""));

		_server.Given(Request.Create().WithPath("/v3/registrations/missing/index.json").UsingGet())
			.RespondWith(Response.Create().WithStatusCode(404));

		await using var sp = BuildServices();
		var client = sp.GetRequiredService<INuGetV3UpstreamClient>();

		var result = await client.GetRegistrationAsync(new Uri(serviceIndexUrl), "Missing", default);

		result.Should().BeNull();
	}

	private static ServiceProvider BuildServices()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddNuGetV3Ecosystem();
		return services.BuildServiceProvider();
	}
}
