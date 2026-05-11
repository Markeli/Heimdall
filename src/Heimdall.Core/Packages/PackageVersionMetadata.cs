namespace Heimdall.Core.Packages;

/// <summary>
/// Ecosystem-agnostic metadata describing a single package version: its coordinates, the
/// publication timestamp (when known) and any ecosystem-specific extra fields.
/// </summary>
/// <param name="Coords">Coordinates identifying the package version.</param>
/// <param name="Published">Publication timestamp from the upstream catalog, when available.</param>
/// <param name="Extra">Free-form ecosystem-specific metadata fields.</param>
public sealed record PackageVersionMetadata(
	PackageCoordinates Coords,
	DateTimeOffset? Published,
	IReadOnlyDictionary<string, string> Extra)
{
	/// <summary>
	/// Creates a metadata instance with no extra fields.
	/// </summary>
	/// <param name="coords">Coordinates identifying the package version.</param>
	/// <param name="published">Publication timestamp, or <c>null</c> if unknown.</param>
	/// <returns>A new metadata instance with an empty <see cref="Extra"/> map.</returns>
	public static PackageVersionMetadata Create(PackageCoordinates coords, DateTimeOffset? published) =>
		new(coords, published, EmptyExtra);

	private static readonly IReadOnlyDictionary<string, string> EmptyExtra =
		new Dictionary<string, string>();
}
