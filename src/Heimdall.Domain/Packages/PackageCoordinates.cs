using Semver;

namespace Heimdall.Domain.Packages;

public sealed record PackageCoordinates(string Ecosystem, string Id, SemVersion Version);
