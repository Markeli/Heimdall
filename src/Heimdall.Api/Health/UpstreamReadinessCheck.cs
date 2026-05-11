using Heimdall.Core.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Heimdall.Api.Health;

/// <summary>
/// Readiness health check that probes the configured upstream service indexes. Registered under the
/// <c>"ready"</c> tag so it participates in the <c>/readyz</c> endpoint only.
/// </summary>
public sealed class UpstreamReadinessCheck : IHealthCheck
{
	private readonly IConfigSnapshotProvider _snapshots;
	private readonly IHttpClientFactory _factory;

	/// <summary>
	/// Initializes a new instance of the <see cref="UpstreamReadinessCheck"/> class.
	/// </summary>
	/// <param name="snapshots">Provider that exposes the current feed configuration snapshot.</param>
	/// <param name="factory">HTTP client factory used to obtain a short-timeout client for the probes.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="snapshots"/> or <paramref name="factory"/> is null.
	/// </exception>
	public UpstreamReadinessCheck(IConfigSnapshotProvider snapshots, IHttpClientFactory factory)
	{
		ArgumentNullException.ThrowIfNull(snapshots);
		ArgumentNullException.ThrowIfNull(factory);
		_snapshots = snapshots;
		_factory = factory;
	}

	/// <summary>
	/// Issues an HTTP GET (reading headers only) to every configured upstream and aggregates the outcome.
	/// </summary>
	/// <param name="context">Standard health check context provided by the framework.</param>
	/// <param name="cancellationToken">Token used to cancel the probes.</param>
	/// <returns>
	/// <see cref="HealthCheckResult.Healthy(string,System.Collections.Generic.IReadOnlyDictionary{string,object})"/>
	/// when every upstream is reachable; otherwise an unhealthy result describing the failures.
	/// </returns>
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		var snapshot = _snapshots.Capture();
		if (snapshot.Feeds.Count == 0)
		{
			return HealthCheckResult.Unhealthy("no feeds configured");
		}

		var http = _factory.CreateClient("Heimdall.Health");
		http.Timeout = TimeSpan.FromSeconds(5);

		var failures = new List<string>();
		foreach (var feed in snapshot.Feeds)
		{
			try
			{
				using var resp = await http.GetAsync(
					feed.Upstream, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
				if (!resp.IsSuccessStatusCode)
				{
					failures.Add($"{feed.Ecosystem}/{feed.Name}: HTTP {(int)resp.StatusCode}");
				}
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
			{
				failures.Add($"{feed.Ecosystem}/{feed.Name}: {ex.Message}");
			}
		}

		return failures.Count == 0
			? HealthCheckResult.Healthy($"all {snapshot.Feeds.Count} feeds reachable")
			: HealthCheckResult.Unhealthy($"upstream check failed: {string.Join("; ", failures)}");
	}
}
