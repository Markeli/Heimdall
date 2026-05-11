using Heimdall.Core.Configuration;

namespace Heimdall.Core.Filtering;

public interface IRuleBuilder
{
	string Type { get; }
	IRule Build(RuleConfig config);
}
