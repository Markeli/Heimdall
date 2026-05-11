using Heimdall.Ecosystems.NuGet.V3.Models;

namespace Heimdall.Ecosystems.NuGet.V3;

public interface INuGetUpstreamClient
{
	Task<RegistrationIndex?> GetRegistrationAsync(
		Uri serviceIndex, string packageId, CancellationToken ct);

	Task<SearchResult?> SearchAsync(
		Uri serviceIndex, string query, int skip, int take, bool includePrerelease, CancellationToken ct);

	Task<HttpResponseMessage> SendBinaryAsync(
		HttpRequestMessage request, CancellationToken ct);
}
