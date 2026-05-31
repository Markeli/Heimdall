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

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(101)]
	public void Search_default_take_out_of_range_fails(int take)
	{
		var validator = new HeimdallOptionsValidator();
		var opts = Valid();
		opts.Server.Search.DefaultTake = take;
		var result = validator.Validate(null, opts);
		result.Failed.Should().BeTrue();
		result.FailureMessage.Should().Contain("defaultTake");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Search_max_concurrent_registration_fetches_below_one_fails(int value)
	{
		var validator = new HeimdallOptionsValidator();
		var opts = Valid();
		opts.Server.Search.MaxConcurrentRegistrationFetches = value;
		var result = validator.Validate(null, opts);
		result.Failed.Should().BeTrue();
		result.FailureMessage.Should().Contain("maxConcurrentRegistrationFetches");
	}

	[Fact]
	public void Forwarded_known_proxy_must_parse_as_ip()
	{
		var validator = new HeimdallOptionsValidator();
		var opts = Valid();
		opts.Server.ForwardedHeaders.KnownProxies.Add("not-an-ip");
		var result = validator.Validate(null, opts);
		result.Failed.Should().BeTrue();
		result.FailureMessage.Should().Contain("knownProxies");
	}

	[Fact]
	public void Forwarded_known_network_must_parse_as_cidr()
	{
		var validator = new HeimdallOptionsValidator();
		var opts = Valid();
		opts.Server.ForwardedHeaders.KnownNetworks.Add("10.0.0.0/notacidr");
		var result = validator.Validate(null, opts);
		result.Failed.Should().BeTrue();
		result.FailureMessage.Should().Contain("knownNetworks");
	}

	[Fact]
	public void Forwarded_headers_valid_entries_pass()
	{
		var validator = new HeimdallOptionsValidator();
		var opts = Valid();
		opts.Server.ForwardedHeaders.KnownProxies.Add("10.0.0.5");
		opts.Server.ForwardedHeaders.KnownNetworks.Add("10.0.0.0/8");
		var result = validator.Validate(null, opts);
		result.Succeeded.Should().BeTrue();
	}
}
