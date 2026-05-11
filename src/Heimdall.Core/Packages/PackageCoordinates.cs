using Semver;

namespace Heimdall.Core.Packages;

public sealed record PackageCoordinates(string Ecosystem, string Id, SemVersion Version);
