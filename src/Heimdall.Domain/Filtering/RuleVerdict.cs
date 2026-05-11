namespace Heimdall.Domain.Filtering;

public sealed record RuleVerdict(FilterDecision Decision, FilterReason? Reason)
{
	public static RuleVerdict Allow { get; } = new(FilterDecision.Allow, null);

	public static RuleVerdict Deny(string ruleName, string message) =>
		new(FilterDecision.Deny, new FilterReason(ruleName, message));

	public bool IsAllow => Decision == FilterDecision.Allow;
	public bool IsDeny => Decision == FilterDecision.Deny;
}
