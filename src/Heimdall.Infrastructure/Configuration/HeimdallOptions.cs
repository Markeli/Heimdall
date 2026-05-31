namespace Heimdall.Infrastructure.Configuration;

/// <summary>
/// Root configuration POCO bound from the <c>heimdall:</c> section of the YAML
/// configuration file. Validated by <see cref="HeimdallOptionsValidator"/>.
/// </summary>
public sealed class HeimdallOptions
{
	/// <summary>Configuration section name (<c>heimdall</c>).</summary>
	public const string SectionName = "heimdall";

	/// <summary>HTTP listener and public URL settings.</summary>
	public ServerOptions Server { get; set; } = new();

	/// <summary>L1/L2 cache strand configuration.</summary>
	public CacheOptions Cache { get; set; } = new();

	/// <summary>Per-ecosystem feed configuration (NuGet today, npm/Maven later).</summary>
	public EcosystemsOptions Ecosystems { get; set; } = new();

	/// <summary>Metrics and audit logging settings.</summary>
	public ObservabilityOptions Observability { get; set; } = new();
}

/// <summary>Server-level settings: bind address, public URL, proxy trust, and search defaults.</summary>
public sealed class ServerOptions
{
	/// <summary>Kestrel listen URL (scheme + host + port).</summary>
	public string Listen { get; set; } = "http://0.0.0.0:8080";

	/// <summary>External base URL used when rewriting <c>@id</c> values in NuGet registration responses.</summary>
	public string PublicBaseUrl { get; set; } = "";

	/// <summary>Reverse-proxy trust configuration. When both lists are empty the middleware is not registered.</summary>
	public ForwardedHeadersOptions ForwardedHeaders { get; set; } = new();

	/// <summary>Search endpoint defaults.</summary>
	public SearchOptions Search { get; set; } = new();
}

/// <summary>
/// Trust list for the ASP.NET Core forwarded-headers middleware. Bound as strings to allow YAML
/// configuration; parsed into <see cref="System.Net.IPAddress"/> / <see cref="System.Net.IPNetwork"/>
/// at startup. When both lists are empty the middleware is not registered and Kestrel's loopback-only
/// default applies.
/// </summary>
public sealed class ForwardedHeadersOptions
{
	/// <summary>Individual reverse-proxy IP addresses Heimdall should trust X-Forwarded-* headers from.</summary>
	public List<string> KnownProxies { get; set; } = [];

	/// <summary>CIDR networks Heimdall should trust X-Forwarded-* headers from.</summary>
	public List<string> KnownNetworks { get; set; } = [];
}

/// <summary>Tunable defaults for the NuGet v3 search endpoint.</summary>
public sealed class SearchOptions
{
	/// <summary>
	/// Default number of hits to return when the client omits or passes a non-positive <c>take</c>.
	/// Constrained to <c>1..100</c> by <see cref="HeimdallOptionsValidator"/>.
	/// </summary>
	public int DefaultTake { get; set; } = 20;

	/// <summary>
	/// Maximum number of registration documents fetched concurrently when enriching a single search
	/// response page with publish dates. Bounds the upstream/thread-pool fan-out per request so search
	/// stays scalable under load. Must be <c>&gt;= 1</c> (validated by <see cref="HeimdallOptionsValidator"/>).
	/// </summary>
	public int MaxConcurrentRegistrationFetches { get; set; } = 8;
}

/// <summary>Container for L1 and L2 cache strand settings.</summary>
public sealed class CacheOptions
{
	/// <summary>In-process L1 strand settings.</summary>
	public CacheLayerOptions L1 { get; set; } = new();

	/// <summary>Distributed L2 strand settings. Defaults to <c>none</c> until a real backend is wired up.</summary>
	public CacheLayerOptions L2 { get; set; } = new() { Provider = "none" };
}

/// <summary>Settings for a single cache strand (L1 or L2).</summary>
public sealed class CacheLayerOptions
{
	/// <summary>Soft cap on the number of entries the strand should hold.</summary>
	public int MaxEntries { get; set; } = 50_000;

	/// <summary>Default TTL applied when an entry is stored without an explicit one.</summary>
	public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>Backend identifier (e.g. <c>memory</c>, <c>none</c>, future <c>redis</c>).</summary>
	public string Provider { get; set; } = "memory";
}

/// <summary>Per-ecosystem feed configuration.</summary>
public sealed class EcosystemsOptions
{
	/// <summary>NuGet ecosystem feeds.</summary>
	public NuGetEcosystemOptions NuGet { get; set; } = new();
}

/// <summary>NuGet ecosystem feed list.</summary>
public sealed class NuGetEcosystemOptions
{
	/// <summary>Declared NuGet feeds exposed by this Heimdall instance.</summary>
	public List<FeedDefinition> Feeds { get; set; } = [];
}

/// <summary>Single feed definition: its name, upstream, optional cache TTL and rule list.</summary>
public sealed class FeedDefinition
{
	/// <summary>Feed identifier used in the URL path; must be unique within an ecosystem.</summary>
	public string Name { get; set; } = "";

	/// <summary>Absolute http(s) URL of the upstream registry to proxy.</summary>
	public string Upstream { get; set; } = "";

	/// <summary>Optional per-feed override for the cache TTL. <c>null</c> falls back to the strand default.</summary>
	public TimeSpan? CacheTtl { get; set; }

	/// <summary>
	/// Loosely-typed filter rules; each entry must contain a <c>type</c> key plus arbitrary rule-specific
	/// parameters. Parsed and validated by the filtering layer.
	/// </summary>
	public List<Dictionary<string, string?>> Rules { get; set; } = [];
}

/// <summary>Metrics and audit logging settings.</summary>
public sealed class ObservabilityOptions
{
	/// <summary>Prometheus metrics endpoint settings.</summary>
	public MetricsOptions Metrics { get; set; } = new();

	/// <summary>Audit logging settings.</summary>
	public AuditOptions Audit { get; set; } = new();
}

/// <summary>Settings for the Prometheus metrics endpoint.</summary>
public sealed class MetricsOptions
{
	/// <summary>Path on which the metrics endpoint is exposed.</summary>
	public string Path { get; set; } = "/metrics";
}

/// <summary>Audit logging toggle.</summary>
public sealed class AuditOptions
{
	/// <summary>Whether audit log emission is enabled.</summary>
	public bool Enabled { get; set; } = true;
}
