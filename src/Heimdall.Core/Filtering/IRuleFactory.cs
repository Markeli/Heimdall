using Heimdall.Core.Configuration;

namespace Heimdall.Core.Filtering;

/// <summary>
/// Materialises a list of declarative <see cref="RuleConfig"/> entries into ready-to-evaluate
/// <see cref="IRule"/> instances, dispatching each by its <see cref="RuleConfig.Type"/>.
/// </summary>
public interface IRuleFactory
{
	/// <summary>
	/// Builds rule instances for the given configurations, preserving order.
	/// </summary>
	/// <param name="configs">Declarative rule configurations.</param>
	/// <returns>Concrete rule instances in the same order as the input.</returns>
	IReadOnlyList<IRule> BuildRules(IReadOnlyList<RuleConfig> configs);
}
