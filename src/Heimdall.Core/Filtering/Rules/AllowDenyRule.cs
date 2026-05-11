using System.Text.RegularExpressions;
using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering.Rules;

/// <summary>
/// Glob-based allow/deny rule on package identifier. Patterns use <c>*</c> and <c>?</c> and
/// are matched case-insensitively. A leading <c>!</c> marks a deny-pattern.
/// </summary>
/// <remarks>
/// Semantics (see README): any deny match rejects the package. If at least one allow
/// pattern is present, the package must match one of them, otherwise it is denied.
/// Deny-only configurations allow everything not explicitly denied.
/// </remarks>
public sealed class AllowDenyRule : IRule
{
	/// <summary>Stable rule discriminator used in configuration and deny reasons.</summary>
	public const string RuleName = "allowDeny";

	private readonly List<Regex> _allow;
	private readonly List<Regex> _deny;
	private readonly List<string> _allowSources;
	private readonly List<string> _denySources;

	/// <summary>
	/// Creates a new <see cref="AllowDenyRule"/> from raw glob patterns. Blank entries are
	/// ignored. Patterns starting with <c>!</c> are treated as deny.
	/// </summary>
	/// <param name="patterns">Raw glob patterns; allow by default, <c>!</c>-prefixed for deny.</param>
	/// <exception cref="ArgumentNullException"><paramref name="patterns"/> is <c>null</c>.</exception>
	/// <exception cref="ArgumentException">A pattern is just <c>!</c> with no body.</exception>
	public AllowDenyRule(IReadOnlyList<string> patterns)
	{
		ArgumentNullException.ThrowIfNull(patterns);

		var allow = new List<Regex>();
		var deny = new List<Regex>();
		var allowSrc = new List<string>();
		var denySrc = new List<string>();

		foreach (var raw in patterns)
		{
			if (string.IsNullOrWhiteSpace(raw))
			{
				continue;
			}

			var trimmed = raw.Trim();
			if (trimmed.StartsWith('!'))
			{
				var body = trimmed[1..];
				if (string.IsNullOrEmpty(body))
				{
					throw new ArgumentException("deny pattern cannot be empty after '!'", nameof(patterns));
				}
				deny.Add(GlobMatcher.Compile(body));
				denySrc.Add(body);
			}
			else
			{
				allow.Add(GlobMatcher.Compile(trimmed));
				allowSrc.Add(trimmed);
			}
		}

		_allow = allow;
		_deny = deny;
		_allowSources = allowSrc;
		_denySources = denySrc;
	}

	/// <inheritdoc />
	public string Name => RuleName;

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="meta"/> is <c>null</c>.</exception>
	public RuleVerdict Evaluate(PackageVersionMetadata meta, RuleContext ctx)
	{
		ArgumentNullException.ThrowIfNull(meta);

		var id = meta.Coords.Id;

		// Deny wins: a single deny match is enough to reject before any allow check.
		for (var i = 0; i < _deny.Count; i++)
		{
			if (_deny[i].IsMatch(id))
			{
				return RuleVerdict.Deny(RuleName, $"package matches deny pattern '!{_denySources[i]}'");
			}
		}

		// Deny-only configuration: anything not explicitly denied is allowed.
		if (_allow.Count == 0)
		{
			return RuleVerdict.Allow;
		}

		for (var i = 0; i < _allow.Count; i++)
		{
			if (_allow[i].IsMatch(id))
			{
				return RuleVerdict.Allow;
			}
		}

		return RuleVerdict.Deny(RuleName, "package does not match any allow pattern");
	}
}
