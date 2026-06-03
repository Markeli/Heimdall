using Semver;

namespace Heimdall.Core.Packages;

/// <summary>
/// Semantic-version ordering helpers used when serving package metadata: ordering a version set
/// ascending and selecting the version that should be presented as the "latest".
/// </summary>
/// <remarks>
/// Heimdall must never rely on the upstream's array order or on lexicographic string comparison
/// when deciding the latest version — filtering can remove the upstream's newest entry, and
/// ordinal comparison sorts <c>10.0.0</c> before <c>2.0.0</c>. All ordering here goes through
/// <see cref="SemVersion.SortOrderComparer"/>, which gives a total semantic-version order.
/// </remarks>
public static class VersionOrdering
{
	/// <summary>
	/// Total-order ascending comparer over <see cref="SemVersion"/> using semantic-version sort order
	/// (so <c>2.0.0</c> precedes <c>10.0.0</c>, unlike lexicographic comparison). <see cref="SemVersion"/>
	/// does not implement <see cref="IComparable{T}"/>, so an explicit comparer is required for sorting.
	/// </summary>
	public static IComparer<SemVersion> Ascending { get; } = SemVersion.SortOrderComparer;

	// Precedence ignores build metadata (per SemVer it is not significant for ordering), which is the
	// correct notion when deciding which version is "greater" for latest selection. SortOrderComparer
	// is kept for OrderAscending so the emitted list has a deterministic total order.
	private static readonly IComparer<SemVersion> Precedence = SemVersion.PrecedenceComparer;

	/// <summary>
	/// Orders the supplied metadata ascending by semantic version. The input order is not relied upon.
	/// </summary>
	/// <param name="versions">Versions to order.</param>
	/// <returns>A new list ordered ascending by semantic version.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="versions"/> is <c>null</c>.</exception>
	public static IReadOnlyList<PackageVersionMetadata> OrderAscending(IEnumerable<PackageVersionMetadata> versions)
	{
		ArgumentNullException.ThrowIfNull(versions);
		return versions.OrderBy(m => m.Coords.Version, Ascending).ToList();
	}

	/// <summary>
	/// Selects the version to present as "latest". When <paramref name="includePrerelease"/> is
	/// <c>false</c> (the default), this is the highest stable version by semantic version, falling back
	/// to the highest prerelease when no stable version is present (NuGet's default notion of latest).
	/// When <c>true</c>, it is simply the highest version overall — matching a search performed with
	/// <c>prerelease=true</c>, where the latest may legitimately be a prerelease.
	/// </summary>
	/// <param name="versions">Candidate versions.</param>
	/// <param name="includePrerelease">When <c>true</c>, a prerelease may be selected as the latest.</param>
	/// <returns>The latest version, or <c>null</c> when the set is empty.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="versions"/> is <c>null</c>.</exception>
	public static PackageVersionMetadata? SelectLatest(
		IEnumerable<PackageVersionMetadata> versions,
		bool includePrerelease = false)
	{
		ArgumentNullException.ThrowIfNull(versions);

		PackageVersionMetadata? latestStable = null;
		PackageVersionMetadata? latestAny = null;
		foreach (var candidate in versions)
		{
			var version = candidate.Coords.Version;
			if (latestAny is null || Precedence.Compare(version, latestAny.Coords.Version) > 0)
			{
				latestAny = candidate;
			}

			if (!version.IsPrerelease &&
				(latestStable is null || Precedence.Compare(version, latestStable.Coords.Version) > 0))
			{
				latestStable = candidate;
			}
		}

		return includePrerelease ? latestAny : latestStable ?? latestAny;
	}
}
