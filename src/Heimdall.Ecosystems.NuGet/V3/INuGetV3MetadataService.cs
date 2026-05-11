using Heimdall.Core.Configuration;
using Heimdall.Ecosystems.NuGet.V3.Models;

namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Orchestrates NuGet V3 metadata responses for Heimdall controllers: fetches upstream data,
/// applies configured filters, rewrites URLs back through Heimdall, and memoizes results per feed.
/// </summary>
public interface INuGetV3MetadataService
{
	/// <summary>
	/// Resolves the configured feed by name within the NuGet ecosystem.
	/// </summary>
	/// <param name="feedName">Configured feed name.</param>
	/// <param name="feed">When successful, the resolved feed configuration; otherwise null.</param>
	/// <returns>True if the feed exists; false otherwise.</returns>
	bool TryGetFeed(string feedName, out FeedConfig? feed);

	/// <summary>
	/// Builds the Heimdall-facing V3 service index for the feed, advertising Heimdall URLs (not the upstream's).
	/// </summary>
	/// <param name="feedName">Configured feed name.</param>
	/// <returns>Serialized JSON of the Heimdall service index.</returns>
	string BuildServiceIndexV3Json(string feedName);

	/// <summary>
	/// Returns the filtered flat-container <c>index.json</c> (versions list) for the package.
	/// </summary>
	/// <param name="feedName">Configured feed name.</param>
	/// <param name="packageId">NuGet package identifier.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Serialized JSON, or null if the upstream returned 404.</returns>
	Task<string?> GetVersionsListJsonAsync(string feedName, string packageId, CancellationToken ct);

	/// <summary>
	/// Returns the filtered registration index for the package, with leaf and content URLs rewritten through Heimdall.
	/// </summary>
	/// <param name="feedName">Configured feed name.</param>
	/// <param name="packageId">NuGet package identifier.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Serialized JSON, or null if the upstream returned 404.</returns>
	Task<string?> GetRegistrationJsonAsync(string feedName, string packageId, CancellationToken ct);

	/// <summary>
	/// Returns the filtered <c>SearchQueryService</c> response with URLs rewritten through Heimdall.
	/// </summary>
	/// <param name="feedName">Configured feed name.</param>
	/// <param name="query">Free-text search query; null is treated as empty.</param>
	/// <param name="skip">Number of hits to skip (paging).</param>
	/// <param name="take">Page size requested from the upstream.</param>
	/// <param name="includePrerelease">Whether to include prerelease versions.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Serialized JSON, or null if the upstream returned 404.</returns>
	Task<string?> SearchJsonAsync(
		string feedName, string? query, int skip, int take, bool includePrerelease, CancellationToken ct);

	/// <summary>
	/// Looks up a single registration leaf for an exact package version, after applying caching.
	/// </summary>
	/// <param name="feedName">Configured feed name.</param>
	/// <param name="packageId">NuGet package identifier.</param>
	/// <param name="version">Exact version string to match (case-insensitive).</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The matching leaf, or null when the package or version is not found.</returns>
	Task<RegistrationLeafV3?> GetVersionLeafAsync(
		string feedName, string packageId, string version, CancellationToken ct);
}
