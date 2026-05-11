using System.Net;
using Heimdall.Api.Audit;
using Heimdall.Api.BinaryProxy;
using Heimdall.Api.Health;
using Heimdall.Core.DependencyInjection;
using Heimdall.Ecosystems.NuGet.DependencyInjection;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Prometheus;
using Serilog;
using AspNetForwardedHeadersOptions = Microsoft.AspNetCore.Builder.ForwardedHeadersOptions;
using IPNetwork = System.Net.IPNetwork;

var builder = WebApplication.CreateBuilder(args);

// Order matters: base config loads first, the per-environment file overrides it, the optional
// untracked secret file overrides those, and HEIMDALL_* env vars override everything so deployments
// can tune settings without rebuilding the image.
builder.Configuration
	.SetBasePath(builder.Environment.ContentRootPath)
	.AddYamlFile("config.yml", optional: true, reloadOnChange: true)
	.AddYamlFile($"config.{builder.Environment.EnvironmentName}.yml", optional: true, reloadOnChange: true)
	.AddYamlFile("config.secret.yml", optional: true, reloadOnChange: true)
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
builder.Services.AddNuGetV3Ecosystem();
builder.Services.AddSingleton<AuditLogger>();
builder.Services.AddSingleton<NuGetV3BinaryProxyService>();

var app = builder.Build();

// Honour X-Forwarded-* when at least one proxy/network is configured. Without explicit trust Kestrel
// falls back to its loopback-only default, which is safer than blindly accepting forwarded values.
var forwarded = app.Services.GetRequiredService<IOptions<HeimdallOptions>>().Value.Server.ForwardedHeaders;
if (forwarded.KnownProxies.Count > 0 || forwarded.KnownNetworks.Count > 0)
{
	var aspOptions = new AspNetForwardedHeadersOptions
	{
		ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
	};
	aspOptions.KnownProxies.Clear();
	aspOptions.KnownIPNetworks.Clear();
	foreach (var raw in forwarded.KnownProxies)
	{
		aspOptions.KnownProxies.Add(IPAddress.Parse(raw));
	}
	foreach (var raw in forwarded.KnownNetworks)
	{
		aspOptions.KnownIPNetworks.Add(IPNetwork.Parse(raw));
	}
	app.UseForwardedHeaders(aspOptions);
}

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
