using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Heimdall.Api.Controllers;

/// <summary>
/// Exposes liveness and readiness probe endpoints used by orchestrators and load balancers.
/// </summary>
[ApiController]
public sealed class HealthController : ControllerBase
{
	private readonly HealthCheckService _healthChecks;

	/// <summary>
	/// Initializes a new instance of the <see cref="HealthController"/> class.
	/// </summary>
	/// <param name="healthChecks">The health check service used to evaluate registered checks for readiness.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="healthChecks"/> is null.</exception>
	public HealthController(HealthCheckService healthChecks)
	{
		ArgumentNullException.ThrowIfNull(healthChecks);
		_healthChecks = healthChecks;
	}

	/// <summary>
	/// Liveness probe. Returns success as long as the process is able to handle HTTP requests.
	/// </summary>
	/// <returns>A 200 OK response with a constant body.</returns>
	/// <response code="200">The process is alive.</response>
	[HttpGet("/healthz")]
	public IActionResult Liveness() => Ok("ok");

	/// <summary>
	/// Readiness probe. Runs all registered health checks and reports their aggregated status.
	/// </summary>
	/// <param name="ct">Token used to cancel the probe evaluation.</param>
	/// <returns>
	/// A response describing the overall status and the per-check entries. The HTTP status code is 200 when
	/// every check reports healthy and 503 otherwise.
	/// </returns>
	/// <response code="200">All health checks reported healthy.</response>
	[HttpGet("/readyz")]
	public async Task<IActionResult> Readiness(CancellationToken ct)
	{
		var report = await _healthChecks.CheckHealthAsync(ct);
		var status = report.Status == HealthStatus.Healthy ? 200 : 503;
		return StatusCode(status, new
		{
			status = report.Status.ToString(),
			entries = report.Entries.ToDictionary(
				kv => kv.Key,
				kv => new { status = kv.Value.Status.ToString(), description = kv.Value.Description }),
		});
	}
}
