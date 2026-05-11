using Heimdall.Core.Filtering;
using Heimdall.Core.Filtering.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Heimdall.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
	public static IServiceCollection AddHeimdallCore(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IRuleEvaluator, RuleEvaluator>();
		services.TryAddSingleton<IRuleFactory, RuleFactory>();
		services.TryAddSingleton<IVersionListFilter, VersionListFilter>();
		services.TryAddSingleton<ISingleVersionGate, SingleVersionGate>();

		services.AddKeyedSingleton<IRuleBuilder, MinAgeDaysRuleBuilder>(MinAgeDaysRule.RuleName);
		services.AddKeyedSingleton<IRuleBuilder, AllowDenyRuleBuilder>(AllowDenyRule.RuleName);

		return services;
	}
}
