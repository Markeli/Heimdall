using Heimdall.Core.Filtering;
using Heimdall.Core.Filtering.Rules;
using Heimdall.Core.Configuration;
using Heimdall.Core.Packages;
using Semver;

namespace Heimdall.UnitTests.Filtering;

public class AllowDenyRuleTests
{
	private static readonly RuleContext Ctx = new("nuget", "strict", DateTimeOffset.UtcNow);

	private static PackageVersionMetadata Meta(string id) =>
		PackageVersionMetadata.Create(
			new PackageCoordinates("nuget", id, SemVersion.Parse("1.0.0")),
			DateTimeOffset.UtcNow.AddDays(-30));

	[Fact]
	public void Empty_patterns_allow_all()
	{
		var rule = new AllowDenyRule([]);

		rule.Evaluate(Meta("Anything"), Ctx).IsAllow.Should().BeTrue();
		rule.Evaluate(Meta("Microsoft.X"), Ctx).IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Deny_only_blocks_matched_allows_others()
	{
		var rule = new AllowDenyRule(["!Internal.*"]);

		rule.Evaluate(Meta("Internal.Foo"), Ctx).IsDeny.Should().BeTrue();
		rule.Evaluate(Meta("Public.Bar"), Ctx).IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Allow_only_with_match_allows()
	{
		var rule = new AllowDenyRule(["Microsoft.*"]);

		rule.Evaluate(Meta("Microsoft.AspNetCore"), Ctx).IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Allow_only_no_match_denies()
	{
		var rule = new AllowDenyRule(["Microsoft.*"]);

		rule.Evaluate(Meta("Newtonsoft.Json"), Ctx).IsDeny.Should().BeTrue();
	}

	[Fact]
	public void Mixed_deny_wins_over_allow()
	{
		var rule = new AllowDenyRule(["Microsoft.*", "!Microsoft.Internal.*"]);

		rule.Evaluate(Meta("Microsoft.AspNetCore"), Ctx).IsAllow.Should().BeTrue();
		rule.Evaluate(Meta("Microsoft.Internal.Tools"), Ctx).IsDeny.Should().BeTrue();
	}

	[Fact]
	public void Mixed_allow_required_when_any_allow_present()
	{
		var rule = new AllowDenyRule(["Microsoft.*", "!Microsoft.Internal.*"]);

		rule.Evaluate(Meta("Newtonsoft.Json"), Ctx).IsDeny.Should().BeTrue();
	}

	[Fact]
	public void Case_insensitive_matching()
	{
		var rule = new AllowDenyRule(["microsoft.*"]);

		rule.Evaluate(Meta("Microsoft.AspNetCore"), Ctx).IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Question_mark_matches_single_char()
	{
		var rule = new AllowDenyRule(["Foo.?"]);

		rule.Evaluate(Meta("Foo.A"), Ctx).IsAllow.Should().BeTrue();
		rule.Evaluate(Meta("Foo.AB"), Ctx).IsDeny.Should().BeTrue();
	}

	[Fact]
	public void Builder_parses_patterns()
	{
		var builder = new AllowDenyRuleBuilder();
		var cfg = new RuleConfig(
			"allowDeny",
			new Dictionary<string, string?> { ["patterns"] = "Microsoft.*;!Internal.*" });

		var rule = builder.Build(cfg);

		rule.Evaluate(Meta("Microsoft.AspNetCore"), Ctx).IsAllow.Should().BeTrue();
		rule.Evaluate(Meta("Internal.Tool"), Ctx).IsDeny.Should().BeTrue();
		rule.Evaluate(Meta("Other"), Ctx).IsDeny.Should().BeTrue();
	}
}
