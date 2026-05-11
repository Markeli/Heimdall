namespace Heimdall.Core.Filtering;

/// <summary>
/// Human-readable explanation attached to a deny verdict, identifying the rule that produced
/// it and why.
/// </summary>
/// <param name="RuleName">Name of the rule that produced the verdict.</param>
/// <param name="Message">Description of why the rule denied the version.</param>
public sealed record FilterReason(string RuleName, string Message);
