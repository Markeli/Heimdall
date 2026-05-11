namespace Heimdall.Ecosystems.NuGet.V3;

public sealed class NuGetUrlRewriter
{
	private readonly Uri _publicBase;

	public NuGetUrlRewriter(Uri publicBaseUrl)
	{
		ArgumentNullException.ThrowIfNull(publicBaseUrl);
		if (!publicBaseUrl.IsAbsoluteUri)
		{
			throw new ArgumentException("publicBaseUrl must be absolute", nameof(publicBaseUrl));
		}

		_publicBase = publicBaseUrl;
	}

	public Uri ServiceIndex(string feed) =>
		Combine($"nuget/{feed}/v3/index.json");

	public Uri RegistrationsBase(string feed) =>
		Combine($"nuget/{feed}/v3/registration5-gz-semver2/");

	public Uri FlatContainerBase(string feed) =>
		Combine($"nuget/{feed}/v3/flatcontainer/");

	public Uri RegistrationIndex(string feed, string packageId) =>
		Combine($"nuget/{feed}/v3/registration5-gz-semver2/{packageId.ToLowerInvariant()}/index.json");

	public Uri RegistrationLeaf(string feed, string packageId, string version) =>
		Combine(
			$"nuget/{feed}/v3/registration5-gz-semver2/{packageId.ToLowerInvariant()}"
			+ $"/{version.ToLowerInvariant()}.json");

	public Uri RegistrationPage(string feed, string packageId, string lower, string upper) =>
		Combine(
			$"nuget/{feed}/v3/registration5-gz-semver2/{packageId.ToLowerInvariant()}"
			+ $"/page/{lower.ToLowerInvariant()}/{upper.ToLowerInvariant()}.json");

	public Uri FlatContainerVersions(string feed, string packageId) =>
		Combine($"nuget/{feed}/v3/flatcontainer/{packageId.ToLowerInvariant()}/index.json");

	public Uri PackageContent(string feed, string packageId, string version)
	{
		var idLower = packageId.ToLowerInvariant();
		var verLower = version.ToLowerInvariant();
		return Combine($"nuget/{feed}/v3/flatcontainer/{idLower}/{verLower}/{idLower}.{verLower}.nupkg");
	}

	public Uri SearchQuery(string feed) =>
		Combine($"nuget/{feed}/v3/query");

	private Uri Combine(string relative)
	{
		var basePart = _publicBase.AbsoluteUri.TrimEnd('/') + "/";
		return new Uri(basePart + relative);
	}
}
