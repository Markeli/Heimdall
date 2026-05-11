using Heimdall.Core.Configuration;

namespace Heimdall.Core.Filtering;

/// <summary>
/// Constructs a concrete <see cref="IRule"/> from its declarative <see cref="RuleConfig"/>.
/// Implementations are registered as keyed services under <see cref="Type"/>.
/// </summary>
public interface IRuleBuilder
{
	/// <summary>Rule type discriminator this builder handles (matches <see cref="RuleConfig.Type"/>).</summary>
	string Type { get; }

	/// <summary>
	/// Builds a rule instance from configuration.
	/// </summary>
	/// <param name="config">Declarative configuration to interpret.</param>
	/// <returns>The constructed rule.</returns>
	IRule Build(RuleConfig config);
}
