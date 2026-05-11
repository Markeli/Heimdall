using Heimdall.Application.Filtering;
using Heimdall.Application.Filtering.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Heimdall.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
	public static IServiceCollection AddHeimdallApplication(this IServiceCollection services)
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
