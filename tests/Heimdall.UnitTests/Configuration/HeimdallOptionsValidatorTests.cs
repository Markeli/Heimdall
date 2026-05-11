using Heimdall.Infrastructure.Configuration;

namespace Heimdall.UnitTests.Configuration;

public class HeimdallOptionsValidatorTests
{
	private static HeimdallOptions Valid()
	{
		return new HeimdallOptions
		{
			Server = new ServerOptions
			{
				Listen = "http://0.0.0.0:8080",
				PublicBaseUrl = "https://heimdall.local",
			},
			Ecosystems = new EcosystemsOptions
			{
				NuGet = new NuGetEcosystemOptions
				{
					Feeds =
					[
						new FeedDefinition
						{
							Name = "strict",
							Upstream = "https://api.nuget.org/v3/index.json",
							Rules =
							[
								new Dictionary<string, string?> { ["type"] = "minAgeDays", ["days"] = "14" },
							],
						},
					],
				},
			},
		};
	}

	[Fact]
	public void Valid_options_pass()
	{
		var validator = new HeimdallOptionsValidator();
		var result = validator.Validate(null, Valid());
		result.Succeeded.Should().BeTrue();
	}

	[Fact]
	public void Public_base_url_required()
	{
		var validator = new HeimdallOptionsValidator();
		var opts = Valid();
		opts.Server.PublicBaseUrl = "";
		var result = validator.Validate(null, opts);
		result.Failed.Should().BeTrue();
		result.FailureMessage.Should().Contain("publicBaseUrl");
	}

	[Fact]
	public void Public_base_url_must_be_absolute()
	{
		var validator = new HeimdallOptionsValidator();
		var opts = Valid();
		opts.Server.PublicBaseUrl = "/relative/path";
		var result = validator.Validate(null, opts);
		result.Failed.Should().BeTrue();
		result.FailureMessage.Should().Contain("publicBaseUrl");
	}

	[Fact]
	public void Feed_upstream_required()
	{
		var validator = new HeimdallOptionsValidator();
		var opts = Valid();
		opts.Ecosystems.NuGet.Feeds[0].Upstream = "";
		var result = validator.Validate(null, opts);
		result.Failed.Should().BeTrue();
	}

	[Fact]
	public void Duplicate_feed_names_fail()
	{
		var validator = new HeimdallOptionsValidator();
		var opts = Valid();
		opts.Ecosystems.NuGet.Feeds.Add(new FeedDefinition
		{
			Name = "STRICT",
			Upstream = "https://api.nuget.org/v3/index.json",
		});
		var result = validator.Validate(null, opts);
		result.Failed.Should().BeTrue();
		result.FailureMessage.Should().Contain("duplicated");
	}

	[Fact]
	public void Rule_without_type_fails()
	{
		var validator = new HeimdallOptionsValidator();
		var opts = Valid();
		opts.Ecosystems.NuGet.Feeds[0].Rules.Add(new Dictionary<string, string?> { ["days"] = "5" });
		var result = validator.Validate(null, opts);
		result.Failed.Should().BeTrue();
	}
}
