using System.Diagnostics.Metrics;

namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Metrics for the NuGet v3 search path. Uses the BCL <see cref="Meter"/> API (no metrics-library
/// dependency in this layer); the host bridges it to its metrics endpoint.
/// </summary>
internal static class NuGetSearchMetrics
{
	/// <summary>Meter name; the host registers this with its metrics exporter.</summary>
	public const string MeterName = "Heimdall.Ecosystems.NuGet";

	private static readonly Meter Meter = new(MeterName);

	/// <summary>
	/// Counts search hits whose registration enrichment failed and fell back to date-less metadata.
	/// Under date-based rules such hits are dropped from results, so a rising count signals that
	/// servable packages may be silently missing from search due to upstream/cache trouble.
	/// </summary>
	public static readonly Counter<long> EnrichmentFailures = Meter.CreateCounter<long>(
		"heimdall_search_enrichment_failures",
		unit: "{hit}",
		description: "Search hits whose metadata enrichment failed and fell back to date-less filtering.");
}
