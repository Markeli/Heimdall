using Heimdall.Core.Configuration;
using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

public interface ISingleVersionGate
{
	RuleVerdict Check(PackageVersionMetadata meta, FeedConfig feed, DateTimeOffset nowUtc);
}
