using Heimdall.Core.Packages;
using Semver;

namespace Heimdall.UnitTests.Packages;

public class VersionOrderingTests
{
	private static PackageVersionMetadata V(string version) =>
		PackageVersionMetadata.Create(
			new PackageCoordinates("nuget", "Pkg", SemVersion.Parse(version, SemVersionStyles.Any)),
			published: null);

	[Fact]
	public void OrderAscending_uses_semver_not_lexicographic_order()
	{
		var ordered = VersionOrdering.OrderAscending([V("10.0.0"), V("2.0.0"), V("1.0.0")]);

		ordered.Select(m => m.Coords.Version.ToString())
			.Should().ContainInOrder("1.0.0", "2.0.0", "10.0.0");
	}

	[Fact]
	public void OrderAscending_places_prerelease_below_its_release()
	{
		var ordered = VersionOrdering.OrderAscending([V("2.0.0"), V("2.0.0-rc.1"), V("1.0.0")]);

		ordered.Select(m => m.Coords.Version.ToString())
			.Should().ContainInOrder("1.0.0", "2.0.0-rc.1", "2.0.0");
	}

	[Fact]
	public void SelectLatest_prefers_highest_stable_over_higher_prerelease()
	{
		var latest = VersionOrdering.SelectLatest([V("1.0.0"), V("2.0.0"), V("2.1.0-rc")]);

		latest!.Coords.Version.ToString().Should().Be("2.0.0");
	}

	[Fact]
	public void SelectLatest_falls_back_to_highest_prerelease_when_no_stable()
	{
		var latest = VersionOrdering.SelectLatest([V("1.0.0-alpha"), V("1.0.0-rc"), V("1.0.0-beta")]);

		latest!.Coords.Version.ToString().Should().Be("1.0.0-rc");
	}

	[Fact]
	public void SelectLatest_returns_null_for_empty_set()
	{
		VersionOrdering.SelectLatest([]).Should().BeNull();
	}
}
