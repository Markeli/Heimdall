using Heimdall.Ecosystems.NuGet.V3.Models;

namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Typed HTTP client abstraction around upstream NuGet V3 endpoints (service index, registration, search,
/// and binary content). Implementations are expected to use named <see cref="HttpClient"/> instances with
/// resilience policies attached via DI.
/// </summary>
public interface INuGetV3UpstreamClient
{
	/// <summary>
	/// Fetches the registration index for a package from the upstream feed.
	/// </summary>
	/// <param name="serviceIndex">Absolute URL of the upstream feed's service index (<c>index.json</c>).</param>
	/// <param name="packageId">Package identifier (case-insensitive; normalized to lowercase in the request URL).</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The deserialized registration index, or null when the upstream returned 404.</returns>
	/// <exception cref="HttpRequestException">Propagated when the upstream returns a non-success, non-404 status.</exception>
	Task<RegistrationIndexV3?> GetRegistrationAsync(
		Uri serviceIndex, string packageId, CancellationToken ct);

	/// <summary>
	/// Queries the upstream <c>SearchQueryService</c>.
	/// </summary>
	/// <param name="serviceIndex">Absolute URL of the upstream feed's service index.</param>
	/// <param name="query">Free-text query (URL-escaped by the implementation).</param>
	/// <param name="skip">Number of hits to skip.</param>
	/// <param name="take">Page size.</param>
	/// <param name="includePrerelease">Whether to include prerelease versions.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The deserialized search result, or null when the upstream returned 404.</returns>
	/// <exception cref="HttpRequestException">Propagated when the upstream returns a non-success, non-404 status.</exception>
	Task<SearchResultV3?> SearchAsync(
		Uri serviceIndex, string query, int skip, int take, bool includePrerelease, CancellationToken ct);

	/// <summary>
	/// Sends a binary (.nupkg) request to the upstream using the binary-tuned HTTP client.
	/// The response is returned with headers only so callers can stream the body.
	/// </summary>
	/// <param name="request">Pre-built request message addressed to the upstream binary endpoint.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The raw upstream response. The caller owns and must dispose it.</returns>
	Task<HttpResponseMessage> SendBinaryAsync(
		HttpRequestMessage request, CancellationToken ct);
}
