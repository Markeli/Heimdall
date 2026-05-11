using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

/// <summary>
/// Default <see cref="IRuleEvaluator"/>. Iterates the rules in order and short-circuits on
/// the first deny verdict.
/// </summary>
public sealed class RuleEvaluator : IRuleEvaluator
{
	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="meta"/> or <paramref name="rules"/> is <c>null</c>.</exception>
	public RuleVerdict Evaluate(PackageVersionMetadata meta, IReadOnlyList<IRule> rules, RuleContext ctx)
	{
		ArgumentNullException.ThrowIfNull(meta);
		ArgumentNullException.ThrowIfNull(rules);

		foreach (var rule in rules)
		{
			var verdict = rule.Evaluate(meta, ctx);
			// Short-circuit: deny wins, no need to evaluate further rules for this version.
			if (verdict.IsDeny)
			{
				return verdict;
			}
		}

		return RuleVerdict.Allow;
	}

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="metas"/> or <paramref name="rules"/> is <c>null</c>.</exception>
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
