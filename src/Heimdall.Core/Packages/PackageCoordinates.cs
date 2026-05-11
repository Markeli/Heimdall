using Semver;

namespace Heimdall.Core.Packages;

/// <summary>
/// Uniquely identifies a single package version within an ecosystem.
/// </summary>
/// <param name="Ecosystem">Ecosystem identifier (e.g. <c>nuget</c>).</param>
/// <param name="Id">Package identifier within the ecosystem.</param>
/// <param name="Version">Semantic version of this package release.</param>
public sealed record PackageCoordinates(string Ecosystem, string Id, SemVersion Version);
