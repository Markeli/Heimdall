namespace Heimdall.Core.Configuration;

/// <summary>
/// Configuration of a single proxied feed: which ecosystem and upstream it targets, what
/// filtering rules apply, and how long metadata should be cached.
/// </summary>
/// <param name="Ecosystem">Ecosystem identifier (e.g. <c>nuget</c>).</param>
/// <param name="Name">Logical feed name used in routing.</param>
/// <param name="Upstream">Upstream feed base URI.</param>
/// <param name="Rules">Ordered list of filtering rules applied to this feed.</param>
/// <param name="CacheTtl">Optional override for metadata cache time-to-live.</param>
public sealed record FeedConfig(
	string Ecosystem,
	string Name,
	Uri Upstream,
	IReadOnlyList<RuleConfig> Rules,
	TimeSpan? CacheTtl);
