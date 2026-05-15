using System.Text.RegularExpressions;
using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering.Rules;

/// <summary>
/// Rejects versions younger than a configured number of days. A version is allowed when
/// <c>now - catalogEntry.published &gt;= days</c>. A missing <c>published</c> timestamp is
/// treated as deny.
/// </summary>
/// <remarks>
/// Optional <c>exclude</c> glob patterns short-circuit the rule for matching package IDs —
/// matched packages are allowed regardless of age (e.g. internal first-party packages whose
/// publishing pipeline is already trusted).
/// </remarks>
public sealed class MinAgeDaysRule : IRule
{
	/// <summary>Stable rule discriminator used in configuration and deny reasons.</summary>
	public const string RuleName = "minAgeDays";

	private readonly int _days;
	private readonly List<Regex> _exclude;
	private readonly List<string> _excludeSources;

	/// <summary>
	/// Creates a new <see cref="MinAgeDaysRule"/>.
	/// </summary>
	/// <param name="days">Minimum required age in days; must be non-negative.</param>
	/// <param name="excludePatterns">
	/// Optional glob patterns (case-insensitive) on package ID; matching packages bypass the
	/// age check entirely. Blank entries are ignored. <c>null</c> is treated as no exclusions.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="days"/> is negative.</exception>
	public MinAgeDaysRule(int days, IReadOnlyList<string>? excludePatterns = null)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(days);
		_days = days;

		var exclude = new List<Regex>();
		var excludeSrc = new List<string>();
		if (excludePatterns is not null)
		{
			foreach (var raw in excludePatterns)
			{
				if (string.IsNullOrWhiteSpace(raw))
				{
					continue;
				}

				var trimmed = raw.Trim();
				exclude.Add(GlobMatcher.Compile(trimmed));
				excludeSrc.Add(trimmed);
			}
		}

		_exclude = exclude;
		_excludeSources = excludeSrc;
	}

	/// <inheritdoc />
	public string Name => RuleName;

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="meta"/> is <c>null</c>.</exception>
	public RuleVerdict Evaluate(PackageVersionMetadata meta, RuleContext ctx)
	{
		ArgumentNullException.ThrowIfNull(meta);

		// Excluded packages bypass the age check — useful for first-party / pre-vetted IDs.
		var id = meta.Coords.Id;
		for (var i = 0; i < _exclude.Count; i++)
		{
			if (_exclude[i].IsMatch(id))
			{
				return RuleVerdict.Allow;
			}
		}

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
