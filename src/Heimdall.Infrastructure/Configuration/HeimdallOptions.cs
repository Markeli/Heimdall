namespace Heimdall.Infrastructure.Configuration;

public sealed class HeimdallOptions
{
	public const string SectionName = "heimdall";

	public ServerOptions Server { get; set; } = new();
	public CacheOptions Cache { get; set; } = new();
	public EcosystemsOptions Ecosystems { get; set; } = new();
	public ObservabilityOptions Observability { get; set; } = new();
}

public sealed class ServerOptions
{
	public string Listen { get; set; } = "http://0.0.0.0:8080";
	public string PublicBaseUrl { get; set; } = "";
}

public sealed class CacheOptions
{
	public CacheLayerOptions L1 { get; set; } = new();
	public CacheLayerOptions L2 { get; set; } = new() { Provider = "none" };
}

public sealed class CacheLayerOptions
{
	public int MaxEntries { get; set; } = 50_000;
	public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);
	public string Provider { get; set; } = "memory";
}

public sealed class EcosystemsOptions
{
	public NuGetEcosystemOptions NuGet { get; set; } = new();
}

public sealed class NuGetEcosystemOptions
{
	public List<FeedDefinition> Feeds { get; set; } = [];
}

public sealed class FeedDefinition
{
	public string Name { get; set; } = "";
	public string Upstream { get; set; } = "";
	public TimeSpan? CacheTtl { get; set; }
	public List<Dictionary<string, string?>> Rules { get; set; } = [];
}

public sealed class ObservabilityOptions
{
	public MetricsOptions Metrics { get; set; } = new();
	public AuditOptions Audit { get; set; } = new();
}

public sealed class MetricsOptions
{
	public string Path { get; set; } = "/metrics";
}

public sealed class AuditOptions
{
	public bool Enabled { get; set; } = true;
}
