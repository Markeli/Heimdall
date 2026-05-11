using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering.Rules;

/// <summary>
/// Rejects versions younger than a configured number of days. A version is allowed when
/// <c>now - catalogEntry.published &gt;= days</c>. A missing <c>published</c> timestamp is
/// treated as deny.
/// </summary>
public sealed class MinAgeDaysRule : IRule
{
	/// <summary>Stable rule discriminator used in configuration and deny reasons.</summary>
	public const string RuleName = "minAgeDays";

	private readonly int _days;

	/// <summary>
	/// Creates a new <see cref="MinAgeDaysRule"/>.
	/// </summary>
	/// <param name="days">Minimum required age in days; must be non-negative.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="days"/> is negative.</exception>
	public MinAgeDaysRule(int days)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(days);
		_days = days;
	}

	/// <inheritdoc />
	public string Name => RuleName;

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="meta"/> is <c>null</c>.</exception>
	public RuleVerdict Evaluate(PackageVersionMetadata meta, RuleContext ctx)
	{
		ArgumentNullException.ThrowIfNull(meta);

		// Safeguard: missing publication date is treated as deny — we cannot prove the age requirement.
		if (!meta.PublishedUtc.HasValue)
		{
			return RuleVerdict.Deny(RuleName, "published date is missing");
		}

		var age = ctx.NowUtc - meta.PublishedUtc.Value;
		if (age >= TimeSpan.FromDays(_days))
		{
			return RuleVerdict.Allow;
		}

		var message = $"version published {age.TotalDays:F1} days ago, requires {_days}";
		return RuleVerdict.Deny(RuleName, message);
	}
}
