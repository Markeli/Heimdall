namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Resolves typed resource URLs from an upstream NuGet V3 service index
/// (<c>RegistrationsBaseUrl</c>, <c>PackageBaseAddress/3.0.0</c>, <c>SearchQueryService</c>).
/// Implementations are expected to cache parsed service indexes to avoid repeated round trips.
/// </summary>
public interface INuGetV3UpstreamUrlResolver
{
	/// <summary>
	/// Returns the base URL of the registration resource, with a trailing slash, ready for concatenation.
	/// </summary>
	/// <param name="serviceIndex">Absolute URL of the upstream feed's service index.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Registration base URL.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the service index has no registration resource.</exception>
	Task<Uri> GetRegistrationBaseUrlAsync(Uri serviceIndex, CancellationToken ct);

	/// <summary>
	/// Returns the absolute URL of the upstream <c>SearchQueryService</c> resource.
	/// </summary>
	/// <param name="serviceIndex">Absolute URL of the upstream feed's service index.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Search query service URL as a string (callers append a query string).</returns>
	/// <exception cref="InvalidOperationException">Thrown when the service index has no search resource.</exception>
	Task<string> GetSearchQueryServiceAsync(Uri serviceIndex, CancellationToken ct);

	/// <summary>
	/// Returns the base URL of the <c>PackageBaseAddress/3.0.0</c> resource (flat container), with a trailing slash.
	/// </summary>
	/// <param name="serviceIndex">Absolute URL of the upstream feed's service index.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Flat container base URL.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the service index has no flat container resource.</exception>
	Task<Uri> GetPackageBaseAddressAsync(Uri serviceIndex, CancellationToken ct);
}
