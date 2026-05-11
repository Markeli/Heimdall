using Heimdall.Core.Packages;
using Heimdall.Ecosystems.NuGet.V3.Models;
using Semver;

namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Projects raw NuGet V3 payloads into Heimdall's domain shape (<see cref="PackageVersionMetadata"/>) so
/// that ecosystem-agnostic filters can be applied uniformly.
/// </summary>
public static class NuGetMetadataProjection
{
	/// <summary>
	/// Flattens a registration index's pages and leaves into a list of <see cref="PackageVersionMetadata"/>.
	/// Versions that fail semver parsing or have empty identifiers are silently skipped.
	/// </summary>
	/// <param name="index">Raw registration index from the upstream.</param>
	/// <returns>Projected version metadata in the order the leaves were enumerated.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="index"/> is null.</exception>
	public static IReadOnlyList<PackageVersionMetadata> ToVersionMetadata(RegistrationIndex index)
	{
		ArgumentNullException.ThrowIfNull(index);

		var result = new List<PackageVersionMetadata>();
		foreach (var page in index.Items)
		{
			// A page may omit its leaves and require a follow-up fetch; the upstream client is expected to
			// have inlined them before reaching this projection, so a null Items here means "skip this page".
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
				// NuGet treats a missing "listed" as listed; mirror that here for downstream filters.
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
