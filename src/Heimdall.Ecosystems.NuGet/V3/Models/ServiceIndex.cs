using System.Text.Json.Serialization;

namespace Heimdall.Ecosystems.NuGet.V3.Models;

public sealed class ServiceIndex
{
	[JsonPropertyName("version")]
	public string Version { get; set; } = "3.0.0";

	[JsonPropertyName("resources")]
	public List<ServiceResource> Resources { get; set; } = [];
}

public sealed class ServiceResource
{
	[JsonPropertyName("@id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("@type")]
	public string Type { get; set; } = "";

	[JsonPropertyName("comment")]
	public string? Comment { get; set; }
}
