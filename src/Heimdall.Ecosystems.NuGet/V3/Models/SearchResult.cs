using System.Text.Json.Serialization;

namespace Heimdall.Ecosystems.NuGet.V3.Models;

public sealed class SearchResult
{
	[JsonPropertyName("totalHits")]
	public int TotalHits { get; set; }

	[JsonPropertyName("data")]
	public List<SearchHit> Data { get; set; } = [];
}

public sealed class SearchHit
{
	[JsonPropertyName("@id")]
	public string? Id { get; set; }

	[JsonPropertyName("id")]
	public string PackageId { get; set; } = "";

	[JsonPropertyName("version")]
	public string Version { get; set; } = "";

	[JsonPropertyName("registration")]
	public string? Registration { get; set; }

	[JsonPropertyName("description")]
	public string? Description { get; set; }

	[JsonPropertyName("versions")]
	public List<SearchVersion> Versions { get; set; } = [];
}

public sealed class SearchVersion
{
	[JsonPropertyName("version")]
	public string Version { get; set; } = "";

	[JsonPropertyName("downloads")]
	public long Downloads { get; set; }

	[JsonPropertyName("@id")]
	public string? Id { get; set; }
}
