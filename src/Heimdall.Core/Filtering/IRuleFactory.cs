using Heimdall.Core.Configuration;

namespace Heimdall.Core.Filtering;

public interface IRuleFactory
{
	IReadOnlyList<IRule> BuildRules(IReadOnlyList<RuleConfig> configs);
}
