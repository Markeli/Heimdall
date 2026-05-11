using Heimdall.Domain.Filtering;
using Heimdall.Domain.Packages;

namespace Heimdall.Application.Filtering;

public interface IRuleEvaluator
{
	RuleVerdict Evaluate(PackageVersionMetadata meta, IReadOnlyList<IRule> rules, RuleContext ctx);

	IReadOnlyList<FilteredVersion> Filter(
		IEnumerable<PackageVersionMetadata> metas,
		IReadOnlyList<IRule> rules,
		RuleContext ctx);
}

public sealed record FilteredVersion(PackageVersionMetadata Meta, RuleVerdict Verdict);
