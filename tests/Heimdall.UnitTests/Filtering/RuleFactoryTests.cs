using Heimdall.Application.DependencyInjection;
using Heimdall.Application.Filtering;
using Heimdall.Application.Filtering.Rules;
using Heimdall.Domain.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.UnitTests.Filtering;

public class RuleFactoryTests
{
	[Fact]
	public void Builds_min_age_days_and_allow_deny()
	{
		var sp = new ServiceCollection().AddHeimdallApplication().BuildServiceProvider();
		var factory = sp.GetRequiredService<IRuleFactory>();

		var rules = factory.BuildRules(
		[
			new RuleConfig("minAgeDays", new Dictionary<string, string?> { ["days"] = "7" }),
			new RuleConfig("allowDeny", new Dictionary<string, string?> { ["patterns"] = "Foo.*" }),
		]);

		rules.Should().HaveCount(2);
		rules[0].Should().BeOfType<MinAgeDaysRule>();
		rules[1].Should().BeOfType<AllowDenyRule>();
	}

	[Fact]
	public void Throws_on_unknown_rule_type()
	{
		var sp = new ServiceCollection().AddHeimdallApplication().BuildServiceProvider();
		var factory = sp.GetRequiredService<IRuleFactory>();

		var act = () => factory.BuildRules(
		[
			new RuleConfig("unknownRule", new Dictionary<string, string?>()),
		]);

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*unknownRule*");
	}
}
