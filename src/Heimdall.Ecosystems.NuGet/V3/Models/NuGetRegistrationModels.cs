using System.Text.Json.Serialization;

namespace Heimdall.Ecosystems.NuGet.V3.Models;

/// <summary>
/// NuGet V3 registration index (wire format) — the top-level document for a package's versions.
/// </summary>
public sealed class RegistrationIndexV3
{
	/// <summary>Absolute URL of this registration index (the <c>@id</c> field).</summary>
	[JsonPropertyName("@id")]
	public string? Id { get; set; }

	/// <summary>Total number of registration pages enumerated by <see cref="Items"/>.</summary>
	[JsonPropertyName("count")]
	public int Count { get; set; }

	/// <summary>Pages of registration leaves; large packages split versions across multiple pages.</summary>
	[JsonPropertyName("items")]
	public List<RegistrationPageV3> Items { get; set; } = [];
}

/// <summary>
/// NuGet V3 registration page (wire format). Either inlines its leaves via <see cref="Items"/> or
/// requires a follow-up fetch by <see cref="Id"/>.
/// </summary>
public sealed class RegistrationPageV3
{
	/// <summary>Absolute URL of this page (used to fetch leaves when <see cref="Items"/> is null).</summary>
	[JsonPropertyName("@id")]
	public string? Id { get; set; }

	/// <summary>Number of leaves on this page.</summary>
	[JsonPropertyName("count")]
	public int Count { get; set; }

	/// <summary>Lowest version in the page range.</summary>
	[JsonPropertyName("lower")]
	public string? Lower { get; set; }

	/// <summary>Highest version in the page range.</summary>
	[JsonPropertyName("upper")]
	public string? Upper { get; set; }

	// Pages on a registration index may inline their leaves (small packages) or omit them
	// and require a follow-up fetch of the page URL (large packages).
	/// <summary>
	/// Registration leaves on this page. Null when the upstream did not inline them; in that case the
	/// page must be re-fetched from <see cref="Id"/>.
	/// </summary>
	[JsonPropertyName("items")]
	public List<RegistrationLeafV3>? Items { get; set; }
}

/// <summary>
/// NuGet V3 registration leaf (wire format) — one published version of a package.
/// </summary>
public sealed class RegistrationLeafV3
{
	/// <summary>Absolute URL of this leaf.</summary>
	[JsonPropertyName("@id")]
	public string? Id { get; set; }

	/// <summary>Catalog entry containing the version metadata used by Heimdall filters.</summary>
	[JsonPropertyName("catalogEntry")]
	public CatalogEntryV3? CatalogEntryV3 { get; set; }

	/// <summary>Absolute URL of the .nupkg content for this version.</summary>
	[JsonPropertyName("packageContent")]
	public string? PackageContent { get; set; }
}

/// <summary>
/// NuGet V3 catalog entry (wire format) — descriptive metadata for a single package version.
/// </summary>
public sealed class CatalogEntryV3
{
	/// <summary>Absolute URL identifying this catalog entry.</summary>
	[JsonPropertyName("@id")]
	public string? Id { get; set; }

	/// <summary>Package identifier as reported by the upstream.</summary>
	[JsonPropertyName("id")]
	public string PackageId { get; set; } = "";

	/// <summary>Package version as reported by the upstream.</summary>
	[JsonPropertyName("version")]
	public string Version { get; set; } = "";

	/// <summary>UTC timestamp the version was published; used by Heimdall age-based filters.</summary>
	[JsonPropertyName("published")]
	public DateTimeOffset? PublishedUtc { get; set; }

	/// <summary>Whether the version is listed on the upstream. Null is treated as listed.</summary>
	[JsonPropertyName("listed")]
	public bool? Listed { get; set; }

	/// <summary>Absolute URL of the .nupkg content.</summary>
	[JsonPropertyName("packageContent")]
	public string? PackageContent { get; set; }
}
