namespace Heimdall.Core.Filtering;

public sealed record RuleContext(string EcosystemName, string FeedName, DateTimeOffset NowUtc);
