namespace Heimdall.Core.Filtering;

/// <summary>
/// Ambient information passed to every rule evaluation: which feed is being served and the
/// reference time used by time-based rules.
/// </summary>
/// <param name="EcosystemName">Ecosystem identifier (e.g. <c>nuget</c>).</param>
/// <param name="FeedName">Logical name of the feed being served.</param>
/// <param name="NowUtc">Reference time (UTC) used by time-sensitive rules.</param>
public sealed record RuleContext(string EcosystemName, string FeedName, DateTimeOffset NowUtc);
