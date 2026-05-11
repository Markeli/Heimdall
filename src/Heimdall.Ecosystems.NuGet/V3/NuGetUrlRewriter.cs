namespace Heimdall.Ecosystems.NuGet.V3;

/// <summary>
/// Builds the Heimdall-facing URLs that replace upstream nuget.org URLs in proxied payloads.
/// All paths are anchored at <c>{publicBaseUrl}/nuget/{feed}/v3/...</c> and use lowercased
/// package identifiers and versions as required by the NuGet V3 protocol.
/// </summary>
public sealed class NuGetUrlRewriter
{
	private readonly Uri _publicBase;

	/// <summary>
	/// Creates a new <see cref="NuGetUrlRewriter"/>.
	/// </summary>
	/// <param name="publicBaseUrl">Absolute base URL of the Heimdall server reachable by clients.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="publicBaseUrl"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="publicBaseUrl"/> is not absolute.</exception>
	public NuGetUrlRewriter(Uri publicBaseUrl)
	{
		ArgumentNullException.ThrowIfNull(publicBaseUrl);
		if (!publicBaseUrl.IsAbsoluteUri)
		{
			throw new ArgumentException("publicBaseUrl must be absolute", nameof(publicBaseUrl));
		}

		_publicBase = publicBaseUrl;
	}

	/// <summary>Returns the Heimdall URL of the feed's V3 service index (<c>index.json</c>).</summary>
	/// <param name="feed">Configured feed name.</param>
	public Uri ServiceIndex(string feed) =>
		Combine($"nuget/{feed}/v3/index.json");

	// We advertise the registration5-gz-semver2 path on Heimdall so clients negotiate the modern
	// resource type even though Heimdall itself does not require gzip on the wire.
	/// <summary>Returns the Heimdall base URL of the <c>RegistrationsBaseUrl</c> resource.</summary>
	/// <param name="feed">Configured feed name.</param>
	public Uri RegistrationsBase(string feed) =>
		Combine($"nuget/{feed}/v3/registration5-gz-semver2/");

	/// <summary>Returns the Heimdall base URL of the <c>PackageBaseAddress/3.0.0</c> (flat container).</summary>
	/// <param name="feed">Configured feed name.</param>
	public Uri FlatContainerBase(string feed) =>
		Combine($"nuget/{feed}/v3/flatcontainer/");

	/// <summary>Returns the Heimdall URL of a package's registration index.</summary>
	/// <param name="feed">Configured feed name.</param>
	/// <param name="packageId">Package identifier (will be lowercased).</param>
	public Uri RegistrationIndex(string feed, string packageId) =>
		Combine($"nuget/{feed}/v3/registration5-gz-semver2/{packageId.ToLowerInvariant()}/index.json");

	/// <summary>Returns the Heimdall URL of a single registration leaf for a specific version.</summary>
	/// <param name="feed">Configured feed name.</param>
	/// <param name="packageId">Package identifier (will be lowercased).</param>
	/// <param name="version">Version string (will be lowercased).</param>
	public Uri RegistrationLeaf(string feed, string packageId, string version) =>
		Combine(
			$"nuget/{feed}/v3/registration5-gz-semver2/{packageId.ToLowerInvariant()}"
			+ $"/{version.ToLowerInvariant()}.json");

	/// <summary>Returns the Heimdall URL of a registration page covering the given version range.</summary>
	/// <param name="feed">Configured feed name.</param>
	/// <param name="packageId">Package identifier (will be lowercased).</param>
	/// <param name="lower">Lower bound version (will be lowercased).</param>
	/// <param name="upper">Upper bound version (will be lowercased).</param>
	public Uri RegistrationPage(string feed, string packageId, string lower, string upper) =>
		Combine(
			$"nuget/{feed}/v3/registration5-gz-semver2/{packageId.ToLowerInvariant()}"
			+ $"/page/{lower.ToLowerInvariant()}/{upper.ToLowerInvariant()}.json");

	/// <summary>Returns the Heimdall URL of the flat-container <c>index.json</c> (versions list).</summary>
	/// <param name="feed">Configured feed name.</param>
	/// <param name="packageId">Package identifier (will be lowercased).</param>
	public Uri FlatContainerVersions(string feed, string packageId) =>
		Combine($"nuget/{feed}/v3/flatcontainer/{packageId.ToLowerInvariant()}/index.json");

	/// <summary>Returns the Heimdall URL of the .nupkg content for a specific package version.</summary>
	/// <param name="feed">Configured feed name.</param>
	/// <param name="packageId">Package identifier (will be lowercased).</param>
	/// <param name="version">Version string (will be lowercased).</param>
	public Uri PackageContent(string feed, string packageId, string version)
	{
		var idLower = packageId.ToLowerInvariant();
		var verLower = version.ToLowerInvariant();
		return Combine($"nuget/{feed}/v3/flatcontainer/{idLower}/{verLower}/{idLower}.{verLower}.nupkg");
	}

	/// <summary>Returns the Heimdall URL of the search query endpoint.</summary>
	/// <param name="feed">Configured feed name.</param>
	public Uri SearchQuery(string feed) =>
		Combine($"nuget/{feed}/v3/query");

	private Uri Combine(string relative)
	{
		var basePart = _publicBase.AbsoluteUri.TrimEnd('/') + "/";
		return new Uri(basePart + relative);
	}
}
