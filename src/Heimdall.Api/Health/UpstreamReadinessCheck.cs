using Heimdall.Core.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Heimdall.Api.Health;

public sealed class UpstreamReadinessCheck : IHealthCheck
{
	private readonly IConfigSnapshotProvider _snapshots;
	private readonly IHttpClientFactory _factory;

	public UpstreamReadinessCheck(IConfigSnapshotProvider snapshots, IHttpClientFactory factory)
	{
		ArgumentNullException.ThrowIfNull(snapshots);
		ArgumentNullException.ThrowIfNull(factory);
		_snapshots = snapshots;
		_factory = factory;
	}

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
