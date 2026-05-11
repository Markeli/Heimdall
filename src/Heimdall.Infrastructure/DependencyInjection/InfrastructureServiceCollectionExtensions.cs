using Heimdall.Core.Configuration;
using Heimdall.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Heimdall.Infrastructure.DependencyInjection;

/// <summary>
/// DI registration entry point for the Heimdall.Infrastructure layer.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
	/// <summary>
	/// Registers Heimdall infrastructure services: options binding and validation, the
	/// <see cref="HybridCache"/> with an in-memory <c>IDistributedCache</c> stub L2 (replace with
	/// AddStackExchangeRedisCache when Redis lands), the config generation counter, the snapshot
	/// provider, and the feed config lookup.
	/// </summary>
	/// <param name="services">Target service collection.</param>
	/// <param name="configuration">Configuration root containing the <c>heimdall</c> section.</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	/// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
	public static IServiceCollection AddHeimdallInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddOptions<HeimdallOptions>()
			.Bind(configuration.GetSection(HeimdallOptions.SectionName))
			.ValidateOnStart();

		services.AddSingleton<IValidateOptions<HeimdallOptions>, HeimdallOptionsValidator>();

		// AddDistributedMemoryCache provides an in-memory IDistributedCache; HybridCache uses it as
		// its L2 stub. Swap to AddStackExchangeRedisCache(...) when a real shared cache is ready —
		// the HybridCache contract above (and every consumer) stays unchanged.
		services.AddDistributedMemoryCache();
		services.AddHybridCache(opts =>
		{
			opts.DefaultEntryOptions = new HybridCacheEntryOptions
			{
				LocalCacheExpiration = TimeSpan.FromMinutes(1),
				Expiration = TimeSpan.FromMinutes(5),
			};
		});

		services.TryAddSingleton<IConfigGeneration, ConfigGeneration>();
		services.TryAddSingleton<IConfigSnapshotProvider, ConfigSnapshotProvider>();
		services.TryAddSingleton<IFeedConfigLookup, FeedConfigLookup>();

		return services;
	}

}
