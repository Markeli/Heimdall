using Heimdall.Ecosystems.NuGet.V3;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// NuGet v3 read-only metadata endpoints (service index, flat-container versions list, registration,
/// and search). Each action delegates to <see cref="INuGetV3MetadataService"/>.
/// </summary>
[ApiController]
[Route("/nuget/{feed}/v3")]
public sealed class NuGetV3MetadataController : ControllerBase
{
	private readonly INuGetV3MetadataService _service;

	/// <summary>
	/// Initializes a new instance of the <see cref="NuGetV3MetadataController"/> class.
	/// </summary>
	/// <param name="service">Service that builds and fetches NuGet v3 metadata documents.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
	public NuGetV3MetadataController(INuGetV3MetadataService service)
	{
		ArgumentNullException.ThrowIfNull(service);
		_service = service;
	}

	/// <summary>
	/// Returns the NuGet v3 service index document for the requested feed, rewritten to point at Heimdall.
	/// </summary>
	/// <param name="feed">Name of the configured logical feed.</param>
	/// <returns>The service index JSON, or ProblemDetails when the feed is unknown.</returns>
	/// <response code="200">Service index returned.</response>
	/// <response code="404">The feed is not configured.</response>
	[HttpGet("index.json")]
	public IActionResult GetServiceIndexV3(string feed)
	{
		if (!_service.TryGetFeed(feed, out _))
		{
			return Problem(statusCode: 404, title: "Unknown feed", detail: $"feed '{feed}' is not configured");
		}
		return Content(_service.BuildServiceIndexV3Json(feed), "application/json");
	}

	/// <summary>
	/// Returns the flat-container versions list for a package.
	/// </summary>
	/// <param name="feed">Name of the configured logical feed.</param>
	/// <param name="packageId">NuGet package identifier (case-insensitive).</param>
	/// <param name="ct">Token used to cancel the upstream call.</param>
	/// <returns>The versions JSON, or 404 when the package or feed is not found.</returns>
	/// <response code="200">Versions list returned.</response>
	/// <response code="404">The feed or the package is unknown.</response>
	[HttpGet("flatcontainer/{packageId}/index.json")]
	public async Task<IActionResult> GetVersionsList(string feed, string packageId, CancellationToken ct)
	{
		if (!_service.TryGetFeed(feed, out _))
		{
			return Problem(statusCode: 404, title: "Unknown feed");
		}

		var json = await _service.GetVersionsListJsonAsync(feed, packageId, ct);
		if (json is null)
		{
			return NotFound();
		}
		return Content(json, "application/json");
	}

	/// <summary>
	/// Returns the registration index document (gzipped semver2 variant) for a package.
	/// </summary>
	/// <param name="feed">Name of the configured logical feed.</param>
	/// <param name="packageId">NuGet package identifier (case-insensitive).</param>
	/// <param name="ct">Token used to cancel the upstream call.</param>
	/// <returns>The registration JSON, or 404 when the package or feed is not found.</returns>
	/// <response code="200">Registration index returned.</response>
	/// <response code="404">The feed or the package is unknown.</response>
	[HttpGet("registration5-gz-semver2/{packageId}/index.json")]
	public async Task<IActionResult> GetRegistration(string feed, string packageId, CancellationToken ct)
	{
		if (!_service.TryGetFeed(feed, out _))
		{
			return Problem(statusCode: 404, title: "Unknown feed");
		}

		var json = await _service.GetRegistrationJsonAsync(feed, packageId, ct);
		if (json is null)
		{
			return NotFound();
		}
		return Content(json, "application/json");
	}

	/// <summary>
	/// Returns a registration page for a package. The current implementation collapses pages into the
	/// inlined registration index, so the page bounds are accepted but not used for upstream paging.
	/// </summary>
	/// <param name="feed">Name of the configured logical feed.</param>
	/// <param name="packageId">NuGet package identifier (case-insensitive).</param>
	/// <param name="lower">Lower version bound from the URL (unused, see remarks).</param>
	/// <param name="upper">Upper version bound from the URL (unused, see remarks).</param>
	/// <param name="ct">Token used to cancel the upstream call.</param>
	/// <returns>The registration JSON, or 404 when the package or feed is not found.</returns>
	/// <response code="200">Registration page returned.</response>
	/// <response code="404">The feed or the package is unknown.</response>
	[HttpGet("registration5-gz-semver2/{packageId}/page/{lower}/{upper}.json")]
	public Task<IActionResult> GetRegistrationPageV3(
		string feed, string packageId, string lower, string upper, CancellationToken ct) =>
		GetRegistration(feed, packageId, ct);

	/// <summary>
	/// Executes a NuGet v3 search query against the configured upstream and returns the JSON response.
	/// </summary>
	/// <param name="feed">Name of the configured logical feed.</param>
	/// <param name="query">Search term passed as the <c>q</c> query parameter.</param>
	/// <param name="skip">Number of results to skip.</param>
	/// <param name="take">Maximum number of results to return; non-positive values default to 20.</param>
	/// <param name="prerelease">When true, prerelease versions are included.</param>
	/// <param name="ct">Token used to cancel the upstream call.</param>
	/// <returns>The search JSON, or 404 when the feed or upstream search service is not available.</returns>
	/// <response code="200">Search results returned.</response>
	/// <response code="404">The feed is unknown or upstream returned no payload.</response>
	[HttpGet("query")]
	public async Task<IActionResult> Search(
		string feed,
		[FromQuery(Name = "q")] string? query,
		[FromQuery] int skip,
		[FromQuery] int take,
		[FromQuery] bool prerelease,
		CancellationToken ct)
	{
		if (!_service.TryGetFeed(feed, out _))
		{
			return Problem(statusCode: 404, title: "Unknown feed");
		}

		if (take <= 0)
		{
			take = 20;
		}

		var json = await _service.SearchJsonAsync(feed, query, skip, take, prerelease, ct);
		if (json is null)
		{
			return NotFound();
		}
		return Content(json, "application/json");
	}
}
