using Heimdall.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Core.Filtering;

public sealed class RuleFactory : IRuleFactory
{
	private readonly IServiceProvider _services;

	public RuleFactory(IServiceProvider services)
	{
		ArgumentNullException.ThrowIfNull(services);
		_services = services;
	}

	public IReadOnlyList<IRule> BuildRules(IReadOnlyList<RuleConfig> configs)
	{
		ArgumentNullException.ThrowIfNull(configs);

		var rules = new List<IRule>(configs.Count);
		foreach (var cfg in configs)
		{
			var builder = _services.GetKeyedService<IRuleBuilder>(cfg.Type)
				?? throw new InvalidOperationException(
					$"No rule builder registered for type '{cfg.Type}'");

			rules.Add(builder.Build(cfg));
		}

		return rules;
	}
}
