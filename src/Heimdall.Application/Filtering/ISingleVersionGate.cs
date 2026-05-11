using Heimdall.Domain.Configuration;
using Heimdall.Domain.Filtering;
using Heimdall.Domain.Packages;

namespace Heimdall.Application.Filtering;

public interface ISingleVersionGate
{
	RuleVerdict Check(PackageVersionMetadata meta, FeedConfig feed, DateTimeOffset nowUtc);
}
