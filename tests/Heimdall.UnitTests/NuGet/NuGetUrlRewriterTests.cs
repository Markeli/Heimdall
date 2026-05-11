using Heimdall.Ecosystems.NuGet.V3;

namespace Heimdall.UnitTests.NuGet;

public class NuGetUrlRewriterTests
{
	private readonly NuGetUrlRewriter _urls = new(new Uri("https://heimdall.local/"));

	[Fact]
	public void ServiceIndex_url()
	{
		_urls.ServiceIndex("strict").ToString()
			.Should().Be("https://heimdall.local/nuget/strict/v3/index.json");
	}

	[Fact]
	public void RegistrationIndex_url_lowercases_id()
	{
		_urls.RegistrationIndex("strict", "Newtonsoft.Json").ToString()
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
		var u = new NuGetUrlRewriter(new Uri("https://heimdall.local"));
		u.ServiceIndex("strict").ToString()
			.Should().Be("https://heimdall.local/nuget/strict/v3/index.json");
	}
}
