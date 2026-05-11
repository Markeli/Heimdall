using Heimdall.Application.Filtering;
using Heimdall.Domain.Filtering;
using Heimdall.Domain.Packages;
using NSubstitute;
using Semver;

namespace Heimdall.UnitTests.Filtering;

public class RuleEvaluatorTests
{
	private static readonly RuleContext Ctx = new("nuget", "strict", DateTimeOffset.UtcNow);

	private static PackageVersionMetadata Meta(string id = "Foo", string ver = "1.0.0") =>
		PackageVersionMetadata.Create(
			new PackageCoordinates("nuget", id, SemVersion.Parse(ver)),
			DateTimeOffset.UtcNow.AddDays(-30));

	[Fact]
	public void Evaluate_no_rules_allows()
	{
		var evaluator = new RuleEvaluator();

		var verdict = evaluator.Evaluate(Meta(), [], Ctx);

		verdict.IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Evaluate_all_rules_allow_returns_allow()
	{
		var evaluator = new RuleEvaluator();
		var r1 = AlwaysAllow("R1");
		var r2 = AlwaysAllow("R2");

		var verdict = evaluator.Evaluate(Meta(), [r1, r2], Ctx);

		verdict.IsAllow.Should().BeTrue();
	}

	[Fact]
	public void Evaluate_first_deny_returns_that_reason_and_short_circuits()
	{
		var evaluator = new RuleEvaluator();
		var deny = AlwaysDeny("R1", "blocked");
		var laterRule = Substitute.For<IRule>();
		laterRule.Name.Returns("R2");
		laterRule.Evaluate(Arg.Any<PackageVersionMetadata>(), Arg.Any<RuleContext>())
			.Returns(RuleVerdict.Allow);

		var verdict = evaluator.Evaluate(Meta(), [deny, laterRule], Ctx);

		verdict.IsDeny.Should().BeTrue();
		verdict.Reason!.RuleName.Should().Be("R1");
		verdict.Reason.Message.Should().Be("blocked");
		laterRule.DidNotReceiveWithAnyArgs().Evaluate(default!, default!);
	}

	[Fact]
	public void Filter_returns_all_versions_with_their_verdicts()
	{
		var evaluator = new RuleEvaluator();
		var allowRule = AlwaysAllow("R1");
		var denyForBar = ConditionalDeny("R2", m => m.Coords.Id == "Bar", "no Bar");

		var metas = new[] { Meta("Foo"), Meta("Bar"), Meta("Baz") };
		var result = evaluator.Filter(metas, [allowRule, denyForBar], Ctx);

		result.Should().HaveCount(3);
		result.Single(r => r.Meta.Coords.Id == "Foo").Verdict.IsAllow.Should().BeTrue();
		result.Single(r => r.Meta.Coords.Id == "Bar").Verdict.IsDeny.Should().BeTrue();
		result.Single(r => r.Meta.Coords.Id == "Baz").Verdict.IsAllow.Should().BeTrue();
	}

	private static IRule AlwaysAllow(string name)
	{
		var r = Substitute.For<IRule>();
		r.Name.Returns(name);
		r.Evaluate(Arg.Any<PackageVersionMetadata>(), Arg.Any<RuleContext>()).Returns(RuleVerdict.Allow);
		return r;
	}

	private static IRule AlwaysDeny(string name, string message)
	{
		var r = Substitute.For<IRule>();
		r.Name.Returns(name);
		r.Evaluate(Arg.Any<PackageVersionMetadata>(), Arg.Any<RuleContext>())
			.Returns(RuleVerdict.Deny(name, message));
		return r;
	}

	private static IRule ConditionalDeny(string name, Func<PackageVersionMetadata, bool> when, string message)
	{
		var r = Substitute.For<IRule>();
		r.Name.Returns(name);
		r.Evaluate(Arg.Any<PackageVersionMetadata>(), Arg.Any<RuleContext>())
			.Returns(call =>
			{
				var m = call.ArgAt<PackageVersionMetadata>(0);
				return when(m) ? RuleVerdict.Deny(name, message) : RuleVerdict.Allow;
			});
		return r;
	}
}
