using System.Text.Json.Serialization;

namespace Heimdall.Ecosystems.NuGet.V3.Models;

/// <summary>
/// NuGet V3 service index (wire format). Lists the resource endpoints exposed by a V3 feed at <c>index.json</c>.
/// </summary>
public sealed class ServiceIndexV3
{
	/// <summary>Protocol schema version reported by the service index. Defaults to <c>3.0.0</c>.</summary>
	[JsonPropertyName("version")]
	public string Version { get; set; } = "3.0.0";

	/// <summary>Resources advertised by the feed, each identifying a typed endpoint.</summary>
	[JsonPropertyName("resources")]
	public List<ServiceResourceV3> Resources { get; set; } = [];
}

/// <summary>
/// NuGet V3 service-index resource entry (wire format). Pairs an absolute URL with a typed role.
/// </summary>
public sealed class ServiceResourceV3
{
	/// <summary>Absolute URL of the resource (the <c>@id</c> in the wire payload).</summary>
	[JsonPropertyName("@id")]
	public string Id { get; set; } = "";

	/// <summary>Resource type identifier, e.g. <c>RegistrationsBaseUrl/3.6.0</c> or <c>SearchQueryService</c>.</summary>
	[JsonPropertyName("@type")]
	public string Type { get; set; } = "";

	/// <summary>Optional human-readable description for the resource.</summary>
	[JsonPropertyName("comment")]
	public string? Comment { get; set; }
}
