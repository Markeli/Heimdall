using Heimdall.Api.Audit;
using Heimdall.Api.BinaryProxy;
using Heimdall.Api.Health;
using Heimdall.Core.DependencyInjection;
using Heimdall.Ecosystems.NuGet.DependencyInjection;
using Heimdall.Infrastructure.DependencyInjection;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Order matters: base YAML loads first, the per-environment file overrides it, and HEIMDALL_* env
// vars override both so deployments can tune settings without rebuilding the image.
builder.Configuration
	.SetBasePath(builder.Environment.ContentRootPath)
	.AddYamlFile("heimdall.yaml", optional: true, reloadOnChange: true)
	.AddYamlFile($"heimdall.{builder.Environment.EnvironmentName}.yaml", optional: true, reloadOnChange: true)
	.AddEnvironmentVariables(prefix: "HEIMDALL_");

builder.Host.UseSerilog((ctx, lc) => lc
	.ReadFrom.Configuration(ctx.Configuration)
	.Enrich.FromLogContext()
	.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient("Heimdall.Health");

builder.Services.AddHealthChecks()
	.AddCheck<UpstreamReadinessCheck>("upstream", tags: ["ready"]);

builder.Services.AddHeimdallCore();
builder.Services.AddHeimdallInfrastructure(builder.Configuration);
builder.Services.AddNuGetEcosystem();
builder.Services.AddSingleton<AuditLogger>();
builder.Services.AddSingleton<BinaryProxyService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics();

app.Run();

/// <summary>
/// Program entry partial declaration that exists solely to expose the top-level <c>Program</c> type
/// to <c>WebApplicationFactory&lt;Program&gt;</c> used by integration tests.
/// </summary>
public partial class Program;
