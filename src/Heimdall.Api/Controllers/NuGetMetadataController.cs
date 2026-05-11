using Heimdall.Ecosystems.NuGet.V3;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("/nuget/{feed}/v3")]
public sealed class NuGetMetadataController : ControllerBase
{
	private readonly INuGetMetadataService _service;

	public NuGetMetadataController(INuGetMetadataService service)
	{
		ArgumentNullException.ThrowIfNull(service);
		_service = service;
	}

	[HttpGet("index.json")]
	public IActionResult GetServiceIndex(string feed)
	{
		if (!_service.TryGetFeed(feed, out _))
		{
			return Problem(statusCode: 404, title: "Unknown feed", detail: $"feed '{feed}' is not configured");
		}
		return Content(_service.BuildServiceIndexJson(feed), "application/json");
	}

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

	[HttpGet("registration5-gz-semver2/{packageId}/page/{lower}/{upper}.json")]
	public Task<IActionResult> GetRegistrationPage(
		string feed, string packageId, string lower, string upper, CancellationToken ct) =>
		GetRegistration(feed, packageId, ct);

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
