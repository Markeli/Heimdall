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

public static class InfrastructureServiceCollectionExtensions
{
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
