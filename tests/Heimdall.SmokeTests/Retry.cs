using System.Net;

namespace Heimdall.SmokeTests;

/// <summary>
/// Tiny retry helper for upstream-touching smoke assertions. Public nuget.org occasionally
/// returns 5xx or times out on a cold cache; failing the release pipeline on a single transient
/// blip is unhelpful, so we retry idempotent reads up to three times with linear backoff.
/// Caller-supplied success predicate decides when to stop retrying.
/// </summary>
internal static class Retry
{
	public static async Task<HttpResponseMessage> GetAsync(
		HttpClient client, string url, int attempts = 3, int backoffSeconds = 2)
	{
		HttpResponseMessage? last = null;
		Exception? lastEx = null;
		for (var i = 0; i < attempts; i++)
		{
			try
			{
				last?.Dispose();
				last = await client.GetAsync(url);
				if (IsTerminal(last.StatusCode))
				{
					return last;
				}
			}
			catch (HttpRequestException ex)
			{
				lastEx = ex;
			}
			catch (TaskCanceledException ex)
			{
				lastEx = ex;
			}

			if (i < attempts - 1)
			{
				await Task.Delay(TimeSpan.FromSeconds(backoffSeconds * (i + 1)));
			}
		}

		if (last is not null) return last;
		throw new InvalidOperationException(
			$"smoke GET {url} failed after {attempts} attempts", lastEx);
	}

	private static bool IsTerminal(HttpStatusCode status)
	{
		// 2xx and the deterministic 4xx cases the suite asserts on are terminal.
		// 5xx and 408/429 are treated as transient and retried.
		var code = (int)status;
		if (code is 408 or 429) return false;
		if (code >= 500) return false;
		return true;
	}
}
