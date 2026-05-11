using System.Net;
using Heimdall.Ecosystems.NuGet.V3;
using Heimdall.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Heimdall.Ecosystems.NuGet.DependencyInjection;

/// <summary>
/// DI registration helpers for the NuGet ecosystem: URL resolver, upstream client, transformer,
/// metadata service, and the two named HTTP clients (metadata and binary) with resilience policies.
/// </summary>
public static class NuGetEcosystemServiceCollectionExtensions
{
	/// <summary>
	/// Registers the NuGet ecosystem services and configures the metadata and binary HTTP clients with
	/// retries, timeouts, and circuit breakers via <c>AddStandardResilienceHandler</c>.
	/// </summary>
	/// <param name="services">Service collection to extend.</param>
	/// <returns>The same <paramref name="services"/> instance for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddNuGetEcosystem(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IUpstreamUrlResolver, UpstreamUrlResolver>();
		services.TryAddSingleton<INuGetUpstreamClient, NuGetUpstreamClient>();
		services.TryAddSingleton(TimeProvider.System);
		services.TryAddSingleton(sp =>
		{
			var monitor = sp.GetRequiredService<IOptionsMonitor<HeimdallOptions>>();
			return new NuGetUrlRewriter(new Uri(monitor.CurrentValue.Server.PublicBaseUrl));
		});
		services.TryAddSingleton<NuGetMetadataTransformer>();
		services.TryAddSingleton<INuGetMetadataService, NuGetMetadataService>();

		// Metadata client: small JSON payloads, gzip/deflate negotiated, aggressive retries are safe.
		services.AddHttpClient(NuGetUpstreamClient.MetadataHttpClientName)
			.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
			{
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
				PooledConnectionLifetime = TimeSpan.FromMinutes(5),
			})
			.AddStandardResilienceHandler(opts =>
			{
				opts.Retry.MaxRetryAttempts = 3;
				opts.Retry.Delay = TimeSpan.FromMilliseconds(500);
				opts.Retry.UseJitter = true;
				opts.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
				opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
				opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
				opts.CircuitBreaker.FailureRatio = 0.5;
				opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
				opts.CircuitBreaker.MinimumThroughput = 10;
				opts.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
			});

		// Binary client: streams .nupkg payloads which may be large; decompression is off because the
		// content must be passed through to the caller byte-for-byte, and timeouts are much higher.
		services.AddHttpClient(NuGetUpstreamClient.BinaryHttpClientName)
			.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
			{
				AutomaticDecompression = DecompressionMethods.None,
				PooledConnectionLifetime = TimeSpan.FromMinutes(5),
				AllowAutoRedirect = true,
			})
			.AddStandardResilienceHandler(opts =>
			{
				opts.Retry.MaxRetryAttempts = 2;
				opts.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
				opts.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
				opts.CircuitBreaker.FailureRatio = 0.5;
				opts.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
				opts.CircuitBreaker.MinimumThroughput = 10;
				opts.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
			});

		return services;
	}
}
