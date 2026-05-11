using Heimdall.Domain.Filtering;
using Heimdall.Domain.Packages;

namespace Heimdall.Application.Filtering;

public sealed class RuleEvaluator : IRuleEvaluator
{
	public RuleVerdict Evaluate(PackageVersionMetadata meta, IReadOnlyList<IRule> rules, RuleContext ctx)
	{
		ArgumentNullException.ThrowIfNull(meta);
		ArgumentNullException.ThrowIfNull(rules);

		foreach (var rule in rules)
		{
			var verdict = rule.Evaluate(meta, ctx);
			if (verdict.IsDeny)
			{
				return verdict;
			}
		}

		return RuleVerdict.Allow;
	}

	public IReadOnlyList<FilteredVersion> Filter(
		IEnumerable<PackageVersionMetadata> metas,
		IReadOnlyList<IRule> rules,
		RuleContext ctx)
	{
		ArgumentNullException.ThrowIfNull(metas);
		ArgumentNullException.ThrowIfNull(rules);

		var result = new List<FilteredVersion>();
		foreach (var meta in metas)
		{
			result.Add(new FilteredVersion(meta, Evaluate(meta, rules, ctx)));
		}

		return result;
	}
}
