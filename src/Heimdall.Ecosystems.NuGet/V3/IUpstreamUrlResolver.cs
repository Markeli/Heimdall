namespace Heimdall.Ecosystems.NuGet.V3;

public interface IUpstreamUrlResolver
{
	Task<Uri> GetRegistrationBaseUrlAsync(Uri serviceIndex, CancellationToken ct);
	Task<string> GetSearchQueryServiceAsync(Uri serviceIndex, CancellationToken ct);
	Task<Uri> GetPackageBaseAddressAsync(Uri serviceIndex, CancellationToken ct);
}
