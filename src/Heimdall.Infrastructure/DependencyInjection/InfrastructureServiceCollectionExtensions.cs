using Heimdall.Core.Caching;
using Heimdall.Core.Configuration;
using Heimdall.Infrastructure.Caching;
using Heimdall.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
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
	/// Registers Heimdall infrastructure services: options binding and validation,
	/// the in-process memory cache, the L1/L2 hybrid metadata cache, the config
	/// generation counter, the snapshot provider, and the feed config lookup.
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

		services.AddMemoryCache();

		services.TryAddSingleton<ICacheLayer>(sp =>
			new MemoryCacheL1(sp.GetRequiredService<IMemoryCache>()));

		services.TryAddSingleton<IMetadataCache>(sp =>
		{
			var l1 = sp.GetRequiredService<ICacheLayer>();
			var l2 = new NullDistributedCacheL2();
			return new HybridMetadataCache(l1, l2);
		});

		services.TryAddSingleton<IConfigGeneration, ConfigGeneration>();
		services.TryAddSingleton<IConfigSnapshotProvider, ConfigSnapshotProvider>();
		services.TryAddSingleton<IFeedConfigLookup, FeedConfigLookup>();

		return services;
	}

}
