namespace Heimdall.Core.Filtering;

/// <summary>
/// Result of evaluating one or more rules against a single version: the allow/deny decision
/// and, when denied, the reason supplied by the rule that rejected it.
/// </summary>
/// <param name="Decision">Allow/deny outcome.</param>
/// <param name="Reason">Reason attached to a deny outcome; <c>null</c> when allowed.</param>
public sealed record RuleVerdict(FilterDecision Decision, FilterReason? Reason)
{
	/// <summary>Singleton allow verdict with no reason attached.</summary>
	public static RuleVerdict Allow { get; } = new(FilterDecision.Allow, null);

	/// <summary>
	/// Creates a deny verdict carrying the supplied reason.
	/// </summary>
	/// <param name="ruleName">Name of the rule producing the deny.</param>
	/// <param name="message">Human-readable explanation.</param>
	/// <returns>A deny verdict.</returns>
	public static RuleVerdict Deny(string ruleName, string message) =>
		new(FilterDecision.Deny, new FilterReason(ruleName, message));

	/// <summary><c>true</c> when this verdict allows the version.</summary>
	public bool IsAllow => Decision == FilterDecision.Allow;

	/// <summary><c>true</c> when this verdict denies the version.</summary>
	public bool IsDeny => Decision == FilterDecision.Deny;
}
