using System.Net.Http.Headers;

namespace Heimdall.SmokeTests;

/// <summary>
/// Shared configuration for the smoke suite. The base URL is read from the
/// <c>HEIMDALL_SMOKE_BASEURL</c> environment variable, falling back to
/// <c>http://localhost:8080</c> for local Docker runs. The smoke suite is intentionally
/// kept out of <c>Heimdall.sln</c> so <c>dotnet cake --target=Test</c> does not pull it in
/// — it must only run against a real container in release CI.
/// </summary>
public static class SmokeEnvironment
{
	public const string BaseUrlEnvVar = "HEIMDALL_SMOKE_BASEURL";
	public const string DefaultBaseUrl = "http://localhost:8080";

	/// <summary>Returns the configured base URL with a trailing slash trimmed.</summary>
	public static string BaseUrl
	{
		get
		{
			var raw = Environment.GetEnvironmentVariable(BaseUrlEnvVar);
			var value = string.IsNullOrWhiteSpace(raw) ? DefaultBaseUrl : raw;
			return value.TrimEnd('/');
		}
	}

	/// <summary>
	/// Creates an <see cref="HttpClient"/> targeting <see cref="BaseUrl"/> with a 30-second
	/// per-request timeout and a recognisable User-Agent so audit logs distinguish smoke traffic.
	/// </summary>
	public static HttpClient CreateClient()
	{
		var client = new HttpClient
		{
			BaseAddress = new Uri(BaseUrl + "/"),
			Timeout = TimeSpan.FromSeconds(30),
		};
		client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("heimdall-smoke", "1"));
		return client;
	}
}
