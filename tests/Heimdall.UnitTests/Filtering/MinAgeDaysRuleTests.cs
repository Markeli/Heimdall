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

	private static PackageVersionMetadata MetaWithPublishedUtc(DateTimeOffset? published, string id = "Foo") =>
		new(
			new PackageCoordinates("nuget", id, SemVersion.Parse("1.0.0")),
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
	public void Allow_when_package_matches_exclude_pattern_even_if_too_young()
	{
		var rule = new MinAgeDaysRule(days: 14, excludePatterns: ["Mindbox.*"]);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-5), id: "Mindbox.Logging"), Ctx);

		verdict.IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Allow_when_package_matches_exclude_pattern_and_published_is_null()
	{
		// Exclusion is checked before the missing-date safeguard: an excluded ID is implicitly trusted.
		var rule = new MinAgeDaysRule(days: 14, excludePatterns: ["Mindbox.*"]);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(null, id: "Mindbox.Logging"), Ctx);

		verdict.IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Exclude_matches_case_insensitively()
	{
		var rule = new MinAgeDaysRule(days: 14, excludePatterns: ["mindbox.*"]);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-5), id: "Mindbox.Logging"), Ctx);

		verdict.IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Exclude_supports_exact_match()
	{
		var rule = new MinAgeDaysRule(days: 14, excludePatterns: ["Quoka"]);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-5), id: "Quoka"), Ctx);

		verdict.IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Deny_when_package_does_not_match_any_exclude_pattern()
	{
		var rule = new MinAgeDaysRule(days: 14, excludePatterns: ["Mindbox.*", "Quoka", "Abc"]);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-5), id: "Newtonsoft.Json"), Ctx);

		verdict.IsDeny.Should().BeTrue();
		verdict.Reason!.RuleName.Should().Be("minAgeDays");
	}

	[Fact]
	public void Empty_exclude_list_preserves_age_check()
	{
		var rule = new MinAgeDaysRule(days: 14, excludePatterns: []);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-5), id: "Mindbox.Logging"), Ctx);

		verdict.IsDeny.Should().BeTrue();
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

	[Fact]
	public void Builder_parses_semicolon_separated_exclude_patterns()
	{
		var builder = new MinAgeDaysRuleBuilder();
		var cfg = new RuleConfig("minAgeDays", new Dictionary<string, string?>
		{
			["days"] = "14",
			["exclude"] = "Mindbox.*; Quoka ;Abc"
		});

		var rule = builder.Build(cfg);

		var allowedYoungMindbox = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-1), id: "Mindbox.Logging"), Ctx);
		var allowedYoungQuoka = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-1), id: "Quoka"), Ctx);
		var allowedYoungAbc = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-1), id: "Abc"), Ctx);
		var deniedYoungOther = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-1), id: "Newtonsoft.Json"), Ctx);

		allowedYoungMindbox.IsAllow.Should().BeTrue();
		allowedYoungQuoka.IsAllow.Should().BeTrue();
		allowedYoungAbc.IsAllow.Should().BeTrue();
		deniedYoungOther.IsDeny.Should().BeTrue();
	}

	[Fact]
	public void Builder_parses_newline_separated_exclude_patterns()
	{
		var builder = new MinAgeDaysRuleBuilder();
		var cfg = new RuleConfig("minAgeDays", new Dictionary<string, string?>
		{
			["days"] = "14",
			["exclude"] = "Mindbox.*\nQuoka\n\nAbc"
		});

		var rule = builder.Build(cfg);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-1), id: "Abc"), Ctx);

		verdict.IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Builder_ignores_blank_exclude_entries()
	{
		var builder = new MinAgeDaysRuleBuilder();
		var cfg = new RuleConfig("minAgeDays", new Dictionary<string, string?>
		{
			["days"] = "14",
			["exclude"] = " ; ;\n;  "
		});

		var act = () => builder.Build(cfg);

		act.Should().NotThrow();
	}

	[Fact]
	public void Builder_treats_missing_exclude_as_no_exclusions()
	{
		var builder = new MinAgeDaysRuleBuilder();
		var cfg = new RuleConfig("minAgeDays", new Dictionary<string, string?> { ["days"] = "14" });

		var rule = builder.Build(cfg);

		var verdict = rule.Evaluate(MetaWithPublishedUtc(Now.AddMinutes(-1), id: "Mindbox.Logging"), Ctx);

		verdict.IsDeny.Should().BeTrue();
	}
}
