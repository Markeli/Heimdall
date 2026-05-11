using Heimdall.Core.Packages;
using Heimdall.Ecosystems.NuGet.V3.Models;
using Semver;

namespace Heimdall.Ecosystems.NuGet.V3;

public static class NuGetMetadataProjection
{
	public static IReadOnlyList<PackageVersionMetadata> ToVersionMetadata(RegistrationIndex index)
	{
		ArgumentNullException.ThrowIfNull(index);

		var result = new List<PackageVersionMetadata>();
		foreach (var page in index.Items)
		{
			if (page.Items is null)
			{
				continue;
			}

			foreach (var leaf in page.Items)
			{
				var entry = leaf.CatalogEntry;
				if (entry is null || string.IsNullOrEmpty(entry.PackageId) || string.IsNullOrEmpty(entry.Version))
				{
					continue;
				}

				if (!SemVersion.TryParse(entry.Version, SemVersionStyles.Any, out var version))
				{
					continue;
				}

				var coords = new PackageCoordinates("nuget", entry.PackageId, version);
				var extra = new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["listed"] = (entry.Listed ?? true) ? "true" : "false",
				};

				result.Add(new PackageVersionMetadata(coords, entry.Published, extra));
			}
		}

		return result;
	}
}
