using Heimdall.Core.Filtering;
using Heimdall.Core.Filtering.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Heimdall.Core.DependencyInjection;

/// <summary>
/// Dependency injection extensions for the Heimdall core layer.
/// </summary>
public static class CoreServiceCollectionExtensions
{
	/// <summary>
	/// Registers the core filtering services and built-in rule builders.
	/// </summary>
	/// <param name="services">The service collection to populate.</param>
	/// <returns>The same service collection, to allow call chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
	public static IServiceCollection AddHeimdallCore(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IRuleEvaluator, RuleEvaluator>();
		services.TryAddSingleton<IRuleFactory, RuleFactory>();
		services.TryAddSingleton<IVersionListFilter, VersionListFilter>();
		services.TryAddSingleton<ISingleVersionGate, SingleVersionGate>();

		// Rule builders are keyed by rule discriminator so RuleFactory can resolve them by RuleConfig.Type.
		services.AddKeyedSingleton<IRuleBuilder, MinAgeDaysRuleBuilder>(MinAgeDaysRule.RuleName);
		services.AddKeyedSingleton<IRuleBuilder, AllowDenyRuleBuilder>(AllowDenyRule.RuleName);

		return services;
	}
}
