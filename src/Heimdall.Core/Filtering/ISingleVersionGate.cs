using Heimdall.Core.Configuration;
using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

/// <summary>
/// Gate that evaluates a single requested version against a feed's rules. Used at download
/// time to decide whether to serve or reject a specific package version.
/// </summary>
public interface ISingleVersionGate
{
	/// <summary>
	/// Checks whether the given version is allowed under the feed's configured rules.
	/// </summary>
	/// <param name="meta">Metadata of the requested version.</param>
	/// <param name="feed">Feed configuration whose rules govern the decision.</param>
	/// <param name="nowUtc">Reference time (UTC) used for time-sensitive rules.</param>
	/// <returns>The verdict for this version.</returns>
	RuleVerdict Check(PackageVersionMetadata meta, FeedConfig feed, DateTimeOffset nowUtc);
}
