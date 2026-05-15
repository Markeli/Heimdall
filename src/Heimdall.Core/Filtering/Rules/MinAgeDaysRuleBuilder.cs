using System.Globalization;
using Heimdall.Core.Configuration;

namespace Heimdall.Core.Filtering.Rules;

/// <summary>
/// Builder for <see cref="MinAgeDaysRule"/>. Reads the required <c>days</c> parameter and
/// parses it as a non-negative invariant-culture integer. Optionally reads <c>exclude</c>:
/// a <c>;</c>- or newline-separated list of glob patterns whose matching package IDs bypass
/// the age check.
/// </summary>
public sealed class MinAgeDaysRuleBuilder : IRuleBuilder
{
	/// <inheritdoc />
	public string Type => MinAgeDaysRule.RuleName;

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
	/// <exception cref="ArgumentException">The <c>days</c> parameter is missing, not an integer, or negative.</exception>
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

		string[]? exclude = null;
		if (config.Parameters.TryGetValue("exclude", out var rawExclude) && rawExclude is not null)
		{
			exclude = rawExclude.Split(
				[';', '\n'],
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		}

		return new MinAgeDaysRule(days, exclude);
	}
}
