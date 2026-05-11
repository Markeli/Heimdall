namespace Heimdall.Api.Http;

/// <summary>
/// Reusable helpers for streaming HTTP proxy scenarios: builds an outbound <see cref="HttpRequestMessage"/>
/// from an inbound <see cref="HttpRequest"/> and pipes an upstream <see cref="HttpResponseMessage"/> into
/// the current <see cref="HttpContext"/> response without buffering the body. Strips hop-by-hop headers
/// per RFC 9110 §7.6.1 in both directions.
/// </summary>
public static class HttpProxyHelpers
{
	/// <summary>
	/// Builds an outbound <see cref="HttpRequestMessage"/> targeted at <paramref name="target"/>, forwarding
	/// end-to-end headers from <paramref name="source"/>. The HTTP method mirrors the source (HEAD or GET)
	/// unless explicitly overridden. <c>Accept-Encoding</c> is cleared so the upstream returns an
	/// uncompressed body we can stream verbatim.
	/// </summary>
	/// <param name="source">Inbound request whose headers and method we forward.</param>
	/// <param name="target">Absolute upstream URL the request will be sent to.</param>
	/// <param name="method">Optional override for the HTTP method.</param>
	/// <returns>A new <see cref="HttpRequestMessage"/> ready to be sent to the upstream.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="target"/> is <c>null</c>.</exception>
	public static HttpRequestMessage BuildUpstreamRequest(HttpRequest source, Uri target, HttpMethod? method = null)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(target);

		var resolvedMethod = method ?? (HttpMethods.IsHead(source.Method) ? HttpMethod.Head : HttpMethod.Get);
		var req = new HttpRequestMessage(resolvedMethod, target);

		foreach (var header in source.Headers)
		{
			if (HopByHopHeaders.Contains(header.Key))
			{
				continue;
			}

			req.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
		}

		req.Headers.AcceptEncoding.Clear();

		return req;
	}

	/// <summary>
	/// Copies <paramref name="response"/> into the current <see cref="HttpContext"/> response, streaming
	/// the body without buffering. End-to-end headers are forwarded; hop-by-hop headers are stripped.
	/// HEAD and 304 responses skip the body copy per RFC 9110.
	/// </summary>
	/// <param name="response">Upstream response to forward.</param>
	/// <param name="context">Current HTTP context the response is written to.</param>
	/// <param name="ct">Token used to cancel the upstream read and downstream write.</param>
	/// <exception cref="ArgumentNullException"><paramref name="response"/> or <paramref name="context"/> is <c>null</c>.</exception>
	public static async Task PipeResponseAsync(HttpResponseMessage response, HttpContext context, CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(response);
		ArgumentNullException.ThrowIfNull(context);

		context.Response.StatusCode = (int)response.StatusCode;

		foreach (var header in response.Headers)
		{
			if (HopByHopHeaders.Contains(header.Key))
			{
				continue;
			}
			context.Response.Headers[header.Key] = header.Value.ToArray();
		}

		foreach (var header in response.Content.Headers)
		{
			if (HopByHopHeaders.Contains(header.Key))
			{
				continue;
			}
			context.Response.Headers[header.Key] = header.Value.ToArray();
		}

		context.Response.Headers.Remove("transfer-encoding");

		// HEAD and 304 responses must not carry a body per RFC 9110; skip copying upstream content.
		if (HttpMethods.IsHead(context.Request.Method) || context.Response.StatusCode == 304)
		{
			return;
		}

		await using var upstream = await response.Content.ReadAsStreamAsync(ct);
		await upstream.CopyToAsync(context.Response.Body, ct);
	}

	/// <summary>
	/// Hop-by-hop headers per RFC 9110 §7.6.1 — connection-scoped, not message-scoped. Forwarding any of
	/// these corrupts the next hop's framing or trust assumptions.
	/// </summary>
	public static readonly IReadOnlySet<string> HopByHopHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"connection",
		"keep-alive",
		"proxy-authenticate",
		"proxy-authorization",
		"te",
		"trailer",
		"transfer-encoding",
		"upgrade",
		"host",
	};
}
