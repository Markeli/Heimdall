using Heimdall.Domain.Configuration;

namespace Heimdall.Application.Filtering;

public interface IRuleFactory
{
	IReadOnlyList<IRule> BuildRules(IReadOnlyList<RuleConfig> configs);
}
