using Heimdall.Domain.Configuration;
using Heimdall.Ecosystems.NuGet.V3.Models;

namespace Heimdall.Ecosystems.NuGet.V3;

public interface INuGetMetadataService
{
	bool TryGetFeed(string feedName, out FeedConfig? feed);
	string BuildServiceIndexJson(string feedName);
	Task<string?> GetVersionsListJsonAsync(string feedName, string packageId, CancellationToken ct);
	Task<string?> GetRegistrationJsonAsync(string feedName, string packageId, CancellationToken ct);
	Task<string?> SearchJsonAsync(
		string feedName, string? query, int skip, int take, bool includePrerelease, CancellationToken ct);
	Task<RegistrationLeaf?> GetVersionLeafAsync(
		string feedName, string packageId, string version, CancellationToken ct);
}
