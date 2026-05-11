using Heimdall.Domain.Configuration;
using Heimdall.Domain.Packages;

namespace Heimdall.Application.Filtering;

public interface IVersionListFilter
{
	IReadOnlyList<PackageVersionMetadata> Apply(
		IEnumerable<PackageVersionMetadata> metas,
		FeedConfig feed,
		DateTimeOffset nowUtc);
}
