using Heimdall.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Core.Filtering;

/// <summary>
/// Default <see cref="IRuleFactory"/>. Resolves the <see cref="IRuleBuilder"/> registered
/// under each <see cref="RuleConfig.Type"/> from the DI container and asks it to build the rule.
/// </summary>
public sealed class RuleFactory : IRuleFactory
{
	private readonly IServiceProvider _services;

	/// <summary>
	/// Creates a new <see cref="RuleFactory"/>.
	/// </summary>
	/// <param name="services">DI container used to resolve keyed <see cref="IRuleBuilder"/> instances.</param>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
	public RuleFactory(IServiceProvider services)
	{
		ArgumentNullException.ThrowIfNull(services);
		_services = services;
	}

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="configs"/> is <c>null</c>.</exception>
	/// <exception cref="InvalidOperationException">No builder is registered for some <see cref="RuleConfig.Type"/>.</exception>
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
