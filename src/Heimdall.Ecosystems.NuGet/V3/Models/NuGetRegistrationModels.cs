using System.Text.Json.Serialization;

namespace Heimdall.Ecosystems.NuGet.V3.Models;

public sealed class RegistrationIndex
{
	[JsonPropertyName("@id")]
	public string? Id { get; set; }

	[JsonPropertyName("count")]
	public int Count { get; set; }

	[JsonPropertyName("items")]
	public List<RegistrationPage> Items { get; set; } = [];
}

public sealed class RegistrationPage
{
	[JsonPropertyName("@id")]
	public string? Id { get; set; }

	[JsonPropertyName("count")]
	public int Count { get; set; }

	[JsonPropertyName("lower")]
	public string? Lower { get; set; }

	[JsonPropertyName("upper")]
	public string? Upper { get; set; }

	[JsonPropertyName("items")]
	public List<RegistrationLeaf>? Items { get; set; }
}

public sealed class RegistrationLeaf
{
	[JsonPropertyName("@id")]
	public string? Id { get; set; }

	[JsonPropertyName("catalogEntry")]
	public CatalogEntry? CatalogEntry { get; set; }

	[JsonPropertyName("packageContent")]
	public string? PackageContent { get; set; }
}

public sealed class CatalogEntry
{
	[JsonPropertyName("@id")]
	public string? Id { get; set; }

	[JsonPropertyName("id")]
	public string PackageId { get; set; } = "";

	[JsonPropertyName("version")]
	public string Version { get; set; } = "";

	[JsonPropertyName("published")]
	public DateTimeOffset? Published { get; set; }

	[JsonPropertyName("listed")]
	public bool? Listed { get; set; }

	[JsonPropertyName("packageContent")]
	public string? PackageContent { get; set; }
}
