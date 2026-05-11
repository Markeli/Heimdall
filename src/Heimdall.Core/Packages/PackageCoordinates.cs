using Semver;

namespace Heimdall.Core.Packages;

/// <summary>
/// Uniquely identifies a single package version within an ecosystem.
/// </summary>
/// <remarks>
/// "Coordinates" is borrowed from Maven/Gradle, where it is the canonical term for the
/// fully-qualified tuple that addresses one package release. Heimdall is multi-ecosystem
/// (NuGet today; npm and Maven on the roadmap), so a vocabulary that already spans those
/// ecosystems is preferable to a NuGet-specific name like <c>PackageVersionId</c>.
/// </remarks>
/// <param name="Ecosystem">Ecosystem identifier (e.g. <c>nuget</c>).</param>
/// <param name="Id">Package identifier within the ecosystem.</param>
/// <param name="Version">Semantic version of this package release.</param>
public sealed record PackageCoordinates(string Ecosystem, string Id, SemVersion Version);
