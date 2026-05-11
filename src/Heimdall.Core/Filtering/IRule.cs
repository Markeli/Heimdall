using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

public interface IRule
{
	string Name { get; }
	RuleVerdict Evaluate(PackageVersionMetadata meta, RuleContext ctx);
}
