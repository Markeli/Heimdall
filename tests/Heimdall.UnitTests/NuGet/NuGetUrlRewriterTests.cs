using Heimdall.Ecosystems.NuGet.V3;

namespace Heimdall.UnitTests.NuGet;

public class NuGetV3UrlRewriterTests
{
	private readonly NuGetV3UrlRewriter _urls = new(new Uri("https://heimdall.local/"));

	[Fact]
	public void ServiceIndexV3_url()
	{
		_urls.ServiceIndexV3("strict").ToString()
			.Should().Be("https://heimdall.local/nuget/strict/v3/index.json");
	}

	[Fact]
	public void RegistrationIndexV3_url_lowercases_id()
	{
		_urls.RegistrationIndexV3("strict", "Newtonsoft.Json").ToString()
			.Should().Be("https://heimdall.local/nuget/strict/v3/registration5-gz-semver2/newtonsoft.json/index.json");
	}

	[Fact]
	public void PackageContent_url_lowercases()
	{
		_urls.PackageContent("strict", "Newtonsoft.Json", "13.0.3").ToString()
			.Should().Be(
				"https://heimdall.local/nuget/strict/v3/flatcontainer/newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg");
	}

	[Fact]
	public void Public_base_without_trailing_slash_works()
	{
		var u = new NuGetV3UrlRewriter(new Uri("https://heimdall.local"));
		u.ServiceIndexV3("strict").ToString()
			.Should().Be("https://heimdall.local/nuget/strict/v3/index.json");
	}
}
