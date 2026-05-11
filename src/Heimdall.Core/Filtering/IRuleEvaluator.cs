using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

/// <summary>
/// Executes a list of rules against package versions. The evaluator is rule-agnostic and
/// implements the short-circuit semantics: the first deny wins.
/// </summary>
public interface IRuleEvaluator
{
	/// <summary>
	/// Evaluates the supplied rules against a single version. Returns the first deny verdict
	/// encountered, otherwise <see cref="RuleVerdict.Allow"/>.
	/// </summary>
	/// <param name="meta">Metadata of the version under evaluation.</param>
	/// <param name="rules">Rules to apply, in order.</param>
	/// <param name="ctx">Evaluation context.</param>
	/// <returns>The resulting verdict for this version.</returns>
	RuleVerdict Evaluate(PackageVersionMetadata meta, IReadOnlyList<IRule> rules, RuleContext ctx);

	/// <summary>
	/// Evaluates the supplied rules against each version of the input and returns the verdict
	/// per version, preserving the input order.
	/// </summary>
	/// <param name="metas">Versions to evaluate.</param>
	/// <param name="rules">Rules to apply, in order.</param>
	/// <param name="ctx">Evaluation context.</param>
	/// <returns>One <see cref="FilteredVersion"/> per input version, including denied ones.</returns>
	IReadOnlyList<FilteredVersion> Filter(
		IEnumerable<PackageVersionMetadata> metas,
		IReadOnlyList<IRule> rules,
		RuleContext ctx);
}

/// <summary>
/// Pairs a package version with the verdict produced for it.
/// </summary>
/// <param name="Meta">The evaluated version metadata.</param>
/// <param name="Verdict">The verdict produced by the rule pipeline.</param>
public sealed record FilteredVersion(PackageVersionMetadata Meta, RuleVerdict Verdict);
