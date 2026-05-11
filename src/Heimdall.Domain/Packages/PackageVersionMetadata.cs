namespace Heimdall.Domain.Packages;

public sealed record PackageVersionMetadata(
	PackageCoordinates Coords,
	DateTimeOffset? Published,
	IReadOnlyDictionary<string, string> Extra)
{
	public static PackageVersionMetadata Create(PackageCoordinates coords, DateTimeOffset? published) =>
		new(coords, published, EmptyExtra);

	private static readonly IReadOnlyDictionary<string, string> EmptyExtra =
		new Dictionary<string, string>();
}
