using System.Text.RegularExpressions;
using Heimdall.Domain.Filtering;
using Heimdall.Domain.Packages;

namespace Heimdall.Application.Filtering.Rules;

public sealed class AllowDenyRule : IRule
{
	public const string RuleName = "allowDeny";

	private readonly List<Regex> _allow;
	private readonly List<Regex> _deny;
	private readonly List<string> _allowSources;
	private readonly List<string> _denySources;

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

	public string Name => RuleName;

	public RuleVerdict Evaluate(PackageVersionMetadata meta, RuleContext ctx)
	{
		ArgumentNullException.ThrowIfNull(meta);

		var id = meta.Coords.Id;

		for (var i = 0; i < _deny.Count; i++)
		{
			if (_deny[i].IsMatch(id))
			{
				return RuleVerdict.Deny(RuleName, $"package matches deny pattern '!{_denySources[i]}'");
			}
		}

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
