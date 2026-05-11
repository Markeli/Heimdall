using Heimdall.Core.Configuration;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

public interface IVersionListFilter
{
	IReadOnlyList<PackageVersionMetadata> Apply(
		IEnumerable<PackageVersionMetadata> metas,
		FeedConfig feed,
		DateTimeOffset nowUtc);
}
