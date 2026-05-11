using System.Globalization;
using Heimdall.Core.Configuration;

namespace Heimdall.Core.Filtering.Rules;

public sealed class MinAgeDaysRuleBuilder : IRuleBuilder
{
	public string Type => MinAgeDaysRule.RuleName;

	public IRule Build(RuleConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);

		if (!config.Parameters.TryGetValue("days", out var raw) || raw is null)
		{
			throw new ArgumentException("minAgeDays rule requires 'days' parameter", nameof(config));
		}

		if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
		{
			throw new ArgumentException($"minAgeDays 'days' must be an integer, got '{raw}'", nameof(config));
		}

		if (days < 0)
		{
			throw new ArgumentException($"minAgeDays 'days' must be >= 0, got {days}", nameof(config));
		}

		return new MinAgeDaysRule(days);
	}
}
