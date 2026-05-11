using Heimdall.Core.Configuration;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

/// <summary>
/// Filters a list of package versions against a feed's rules, retaining only those that
/// pass. Used when serving listing/registration responses.
/// </summary>
public interface IVersionListFilter
{
	/// <summary>
	/// Applies the feed's rules to each version and returns only the allowed ones.
	/// </summary>
	/// <param name="metas">Candidate versions.</param>
	/// <param name="feed">Feed configuration whose rules govern the decision.</param>
	/// <param name="nowUtc">Reference time (UTC) used for time-sensitive rules.</param>
	/// <returns>The subset of versions that all rules allow, in the original order.</returns>
	IReadOnlyList<PackageVersionMetadata> Apply(
		IEnumerable<PackageVersionMetadata> metas,
		FeedConfig feed,
		DateTimeOffset nowUtc);
}
