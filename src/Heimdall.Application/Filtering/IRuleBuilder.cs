using Heimdall.Domain.Configuration;

namespace Heimdall.Application.Filtering;

public interface IRuleBuilder
{
	string Type { get; }
	IRule Build(RuleConfig config);
}
