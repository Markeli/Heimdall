using Heimdall.Core.Configuration;

namespace Heimdall.Core.Filtering.Rules;

/// <summary>
/// Builder for <see cref="AllowDenyRule"/>. Reads the <c>patterns</c> parameter and splits
/// it on <c>;</c> or newline, trimming entries.
/// </summary>
public sealed class AllowDenyRuleBuilder : IRuleBuilder
{
	/// <inheritdoc />
	public string Type => AllowDenyRule.RuleName;

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
	public IRule Build(RuleConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);

		// Missing or null 'patterns' yields a rule that allows everything (no filters configured).
		if (!config.Parameters.TryGetValue("patterns", out var raw) || raw is null)
		{
			return new AllowDenyRule([]);
		}

		var patterns = raw.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return new AllowDenyRule(patterns);
	}
}
