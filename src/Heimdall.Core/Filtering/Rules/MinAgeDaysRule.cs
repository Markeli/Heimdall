using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering.Rules;

public sealed class MinAgeDaysRule : IRule
{
	public const string RuleName = "minAgeDays";

	private readonly int _days;

	public MinAgeDaysRule(int days)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(days);
		_days = days;
	}

	public string Name => RuleName;

	public RuleVerdict Evaluate(PackageVersionMetadata meta, RuleContext ctx)
	{
		ArgumentNullException.ThrowIfNull(meta);

		if (!meta.Published.HasValue)
		{
			return RuleVerdict.Deny(RuleName, "published date is missing");
		}

		var age = ctx.NowUtc - meta.Published.Value;
		if (age >= TimeSpan.FromDays(_days))
		{
			return RuleVerdict.Allow;
		}

		var message = $"version published {age.TotalDays:F1} days ago, requires {_days}";
		return RuleVerdict.Deny(RuleName, message);
	}
}
