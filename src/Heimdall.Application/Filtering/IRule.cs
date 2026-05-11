using Heimdall.Domain.Filtering;
using Heimdall.Domain.Packages;

namespace Heimdall.Application.Filtering;

public interface IRule
{
	string Name { get; }
	RuleVerdict Evaluate(PackageVersionMetadata meta, RuleContext ctx);
}
