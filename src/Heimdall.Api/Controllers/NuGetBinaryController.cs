using Heimdall.Api.BinaryProxy;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// NuGet v3 flat-container binary endpoint that proxies <c>.nupkg</c> downloads through the policy gate.
/// </summary>
[ApiController]
[Route("/nuget/{feed}/v3/flatcontainer")]
public sealed class NuGetBinaryController : ControllerBase
{
	private readonly BinaryProxyService _proxy;

	/// <summary>
	/// Initializes a new instance of the <see cref="NuGetBinaryController"/> class.
	/// </summary>
	/// <param name="proxy">Service that evaluates the policy gate and streams the upstream binary to the client.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="proxy"/> is null.</exception>
	public NuGetBinaryController(BinaryProxyService proxy)
	{
		ArgumentNullException.ThrowIfNull(proxy);
		_proxy = proxy;
	}

	/// <summary>
	/// Downloads (or probes via HEAD) a single package file from the configured upstream feed.
	/// </summary>
	/// <param name="feed">Name of the configured logical feed, used to resolve the upstream service index.</param>
	/// <param name="packageId">NuGet package identifier (case-insensitive).</param>
	/// <param name="version">Package version in the URL segment (case-insensitive).</param>
	/// <param name="fileName">Requested file name, typically <c>{id}.{version}.nupkg</c>.</param>
	/// <param name="ct">Token used to cancel the upstream call and response streaming.</param>
	/// <returns>
	/// An empty result when the binary has been streamed to the response, or a ProblemDetails result
	/// produced by the proxy when the request was denied or the feed/version was not found.
	/// </returns>
	/// <response code="200">The package file was streamed from upstream.</response>
	/// <response code="403">A filtering rule denied the download.</response>
	/// <response code="404">The feed or the requested version is unknown.</response>
	[HttpGet("{packageId}/{version}/{fileName}")]
	[HttpHead("{packageId}/{version}/{fileName}")]
	public async Task<IActionResult> Download(
		string feed, string packageId, string version, string fileName, CancellationToken ct)
	{
		var problem = await _proxy.ProxyAsync(HttpContext, feed, packageId, version, fileName, ct);
		// Returning EmptyResult prevents the MVC pipeline from overwriting the response body
		// that BinaryProxyService has already streamed to the client.
		return problem ?? new EmptyResult();
	}
}
