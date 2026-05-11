using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Heimdall.Api.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
	private readonly HealthCheckService _healthChecks;

	public HealthController(HealthCheckService healthChecks)
	{
		ArgumentNullException.ThrowIfNull(healthChecks);
		_healthChecks = healthChecks;
	}

	[HttpGet("/healthz")]
	public IActionResult Liveness() => Ok("ok");

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
