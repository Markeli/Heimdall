using Heimdall.Domain.Configuration;

namespace Heimdall.Application.Filtering.Rules;

public sealed class AllowDenyRuleBuilder : IRuleBuilder
{
	public string Type => AllowDenyRule.RuleName;

	public IRule Build(RuleConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);

		if (!config.Parameters.TryGetValue("patterns", out var raw) || raw is null)
		{
			return new AllowDenyRule([]);
		}

		var patterns = raw.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return new AllowDenyRule(patterns);
	}
}
