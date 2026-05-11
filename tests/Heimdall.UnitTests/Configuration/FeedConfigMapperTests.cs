using Heimdall.Infrastructure.Configuration;

namespace Heimdall.UnitTests.Configuration;

public class FeedConfigMapperTests
{
	[Fact]
	public void Maps_nuget_feeds_with_rules()
	{
		var opts = new HeimdallOptions
		{
			Server = new ServerOptions { PublicBaseUrl = "https://h.local" },
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
							CacheTtl = TimeSpan.FromMinutes(10),
							Rules =
							[
								new Dictionary<string, string?>
								{
									["type"] = "minAgeDays",
									["days"] = "14",
								},
								new Dictionary<string, string?>
								{
									["type"] = "allowDeny",
									["patterns"] = "Microsoft.*;!Internal.*",
								},
							],
						},
					],
				},
			},
		};

		var feeds = FeedConfigMapper.Map(opts);

		feeds.Should().HaveCount(1);
		feeds[0].Ecosystem.Should().Be("nuget");
		feeds[0].Name.Should().Be("strict");
		feeds[0].Upstream.Should().Be(new Uri("https://api.nuget.org/v3/index.json"));
		feeds[0].CacheTtl.Should().Be(TimeSpan.FromMinutes(10));
		feeds[0].Rules.Should().HaveCount(2);
		feeds[0].Rules[0].Type.Should().Be("minAgeDays");
		feeds[0].Rules[0].Parameters.Should().ContainKey("days").WhoseValue.Should().Be("14");
		feeds[0].Rules[0].Parameters.Should().NotContainKey("type");
		feeds[0].Rules[1].Type.Should().Be("allowDeny");
		feeds[0].Rules[1].Parameters.Should().ContainKey("patterns");
	}
}
