using Heimdall.Core.Filtering;
using Heimdall.Core.Filtering.Rules;
using Heimdall.Core.Configuration;
using Heimdall.Core.Packages;
using Semver;

namespace Heimdall.UnitTests.Filtering;

public class MinAgeDaysRuleTests
{
	private static readonly DateTimeOffset Now = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);
	private static readonly RuleContext Ctx = new("nuget", "strict", Now);

	private static PackageVersionMetadata MetaWithPublishedUtc(DateTimeOffset? published) =>
		new(
			new PackageCoordinates("nuget", "Foo", SemVersion.Parse("1.0.0")),
			published,
			new Dictionary<string, string>());

	[Fact]
	public void Allow_when_published_exactly_min_age()
	{
		var rule = new MinAgeDaysRule(days: 14);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddDays(-14)), Ctx);

		verdict.IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Allow_when_published_long_ago()
	{
		var rule = new MinAgeDaysRule(days: 14);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddYears(-1)), Ctx);

		verdict.IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Deny_when_published_a_second_before_min_age()
	{
		var rule = new MinAgeDaysRule(days: 14);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddDays(-14).AddSeconds(1)), Ctx);

		verdict.IsDeny.Should().BeTrue();
		verdict.Reason!.RuleName.Should().Be("minAgeDays");
	}

	[Fact]
	public void Deny_when_published_in_future()
	{
		var rule = new MinAgeDaysRule(days: 14);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddDays(1)), Ctx);

		verdict.IsDeny.Should().BeTrue();
	}

	[Fact]
	public void Deny_when_published_is_null()
	{
		var rule = new MinAgeDaysRule(days: 14);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(null), Ctx);

		verdict.IsDeny.Should().BeTrue();
		verdict.Reason!.Message.Should().Contain("published date is missing");
	}

	[Fact]
	public void Builder_extracts_days_from_config()
	{
		var builder = new MinAgeDaysRuleBuilder();
		var cfg = new RuleConfig("minAgeDays", new Dictionary<string, string?> { ["days"] = "21" });

		var rule = builder.Build(cfg);

		rule.Should().BeOfType<MinAgeDaysRule>();
	}

	[Fact]
	public void Builder_throws_when_days_missing()
	{
		var builder = new MinAgeDaysRuleBuilder();
		var cfg = new RuleConfig("minAgeDays", new Dictionary<string, string?>());

		var act = () => builder.Build(cfg);

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void Builder_throws_when_days_negative()
	{
		var builder = new MinAgeDaysRuleBuilder();
		var cfg = new RuleConfig("minAgeDays", new Dictionary<string, string?> { ["days"] = "-1" });

		var act = () => builder.Build(cfg);

		act.Should().Throw<ArgumentException>();
	}
}
