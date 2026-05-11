using System.Text.Json.Serialization;

namespace Heimdall.Ecosystems.NuGet.V3.Models;

/// <summary>
/// NuGet V3 search response (wire format) returned by the <c>SearchQueryService</c> endpoint.
/// </summary>
public sealed class SearchResultV3
{
	/// <summary>Total number of hits matching the query on the upstream, before paging.</summary>
	[JsonPropertyName("totalHits")]
	public int TotalHits { get; set; }

	/// <summary>The page of matching package hits returned by the upstream.</summary>
	[JsonPropertyName("data")]
	public List<SearchHitV3> Data { get; set; } = [];
}

/// <summary>
/// NuGet V3 search hit (wire format). Represents a single package and its known versions.
/// </summary>
public sealed class SearchHitV3
{
	/// <summary>Absolute URL identifying this hit's registration index (the <c>@id</c> field).</summary>
	[JsonPropertyName("@id")]
	public string? Id { get; set; }

	/// <summary>Package identifier (case-insensitive on NuGet, but normalized lowercase in URLs).</summary>
	[JsonPropertyName("id")]
	public string PackageId { get; set; } = "";

	/// <summary>The version selected as the hit's primary version (typically the latest).</summary>
	[JsonPropertyName("version")]
	public string Version { get; set; } = "";

	/// <summary>Absolute URL of the package's registration index.</summary>
	[JsonPropertyName("registration")]
	public string? Registration { get; set; }

	/// <summary>Short package description.</summary>
	[JsonPropertyName("description")]
	public string? Description { get; set; }

	/// <summary>All versions of the package included in the hit.</summary>
	[JsonPropertyName("versions")]
	public List<SearchVersionV3> Versions { get; set; } = [];
}

/// <summary>
/// NuGet V3 search hit version entry (wire format).
/// </summary>
public sealed class SearchVersionV3
{
	/// <summary>Package version string.</summary>
	[JsonPropertyName("version")]
	public string Version { get; set; } = "";

	/// <summary>Reported download count for this version on the upstream.</summary>
	[JsonPropertyName("downloads")]
	public long Downloads { get; set; }

	/// <summary>Absolute URL of the registration leaf for this version.</summary>
	[JsonPropertyName("@id")]
	public string? Id { get; set; }
}
