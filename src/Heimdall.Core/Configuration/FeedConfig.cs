namespace Heimdall.Core.Configuration;

public sealed record FeedConfig(
	string Ecosystem,
	string Name,
	Uri Upstream,
	IReadOnlyList<RuleConfig> Rules,
	TimeSpan? CacheTtl);
