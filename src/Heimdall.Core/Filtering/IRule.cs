using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

/// <summary>
/// Filtering rule that evaluates a single package version and returns an allow/deny verdict.
/// </summary>
public interface IRule
{
	/// <summary>Stable identifier of the rule, used in deny reasons and logs.</summary>
	string Name { get; }

	/// <summary>
	/// Evaluates the rule against a single package version.
	/// </summary>
	/// <param name="meta">Metadata of the version under evaluation.</param>
	/// <param name="ctx">Evaluation context (feed, ecosystem, current time).</param>
	/// <returns>The rule verdict.</returns>
	RuleVerdict Evaluate(PackageVersionMetadata meta, RuleContext ctx);
}
