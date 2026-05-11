using Heimdall.Api.BinaryProxy;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("/nuget/{feed}/v3/flatcontainer")]
public sealed class NuGetBinaryController : ControllerBase
{
	private readonly BinaryProxyService _proxy;

	public NuGetBinaryController(BinaryProxyService proxy)
	{
		ArgumentNullException.ThrowIfNull(proxy);
		_proxy = proxy;
	}

	[HttpGet("{packageId}/{version}/{fileName}")]
	[HttpHead("{packageId}/{version}/{fileName}")]
	public async Task<IActionResult> Download(
		string feed, string packageId, string version, string fileName, CancellationToken ct)
	{
		var problem = await _proxy.ProxyAsync(HttpContext, feed, packageId, version, fileName, ct);
		return problem ?? new EmptyResult();
	}
}
