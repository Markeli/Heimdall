using System.Net;
using Microsoft.Extensions.Options;

namespace Heimdall.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="HeimdallOptions"/> at startup and on every reload. Errors here
/// are fatal because downstream components assume a structurally-correct config (valid
/// upstream URIs, present rule types, unique feed names, etc.).
/// </summary>
public sealed class HeimdallOptionsValidator : IValidateOptions<HeimdallOptions>
{
	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
	public ValidateOptionsResult Validate(string? name, HeimdallOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var errors = new List<string>();

		if (string.IsNullOrWhiteSpace(options.Server.PublicBaseUrl))
		{
			errors.Add("heimdall.server.publicBaseUrl is required (used to rewrite registration @id URLs)");
		}
		else if (!IsHttpUrl(options.Server.PublicBaseUrl))
		{
			errors.Add($"heimdall.server.publicBaseUrl must be an absolute http(s) URL: '{options.Server.PublicBaseUrl}'");
		}

		for (var i = 0; i < options.Server.ForwardedHeaders.KnownProxies.Count; i++)
		{
			var raw = options.Server.ForwardedHeaders.KnownProxies[i];
			if (!IPAddress.TryParse(raw, out _))
			{
				errors.Add($"heimdall.server.forwardedHeaders.knownProxies[{i}] is not a valid IP address: '{raw}'");
			}
		}

		for (var i = 0; i < options.Server.ForwardedHeaders.KnownNetworks.Count; i++)
		{
			var raw = options.Server.ForwardedHeaders.KnownNetworks[i];
			if (!IPNetwork.TryParse(raw, out _))
			{
				errors.Add($"heimdall.server.forwardedHeaders.knownNetworks[{i}] is not a valid CIDR network: '{raw}'");
			}
		}

		if (options.Server.Search.DefaultTake is < 1 or > 100)
		{
			errors.Add($"heimdall.server.search.defaultTake must be in 1..100, got {options.Server.Search.DefaultTake}");
		}

		if (options.Server.Search.MaxConcurrentEnrichmentFetches < 1)
		{
			errors.Add("heimdall.server.search.maxConcurrentEnrichmentFetches must be >= 1, got "
				+ options.Server.Search.MaxConcurrentEnrichmentFetches);
		}

		if (options.Server.Search.MaxTake < 1)
		{
			errors.Add($"heimdall.server.search.maxTake must be >= 1, got {options.Server.Search.MaxTake}");
		}

		var seenFeedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < options.Ecosystems.NuGet.Feeds.Count; i++)
		{
			var feed = options.Ecosystems.NuGet.Feeds[i];
			var prefix = $"heimdall.ecosystems.nuget.feeds[{i}]";

			if (string.IsNullOrWhiteSpace(feed.Name))
			{
				errors.Add($"{prefix}.name is required");
			}
			else if (!seenFeedNames.Add(feed.Name))
			{
				errors.Add($"{prefix}.name '{feed.Name}' is duplicated");
			}

			if (string.IsNullOrWhiteSpace(feed.Upstream))
			{
				errors.Add($"{prefix}.upstream is required");
			}
			else if (!IsHttpUrl(feed.Upstream))
			{
				errors.Add($"{prefix}.upstream must be an absolute http(s) URL: '{feed.Upstream}'");
			}

			for (var r = 0; r < feed.Rules.Count; r++)
			{
				var rule = feed.Rules[r];
				if (!rule.TryGetValue("type", out var type) || string.IsNullOrWhiteSpace(type))
				{
					errors.Add($"{prefix}.rules[{r}].type is required");
				}
			}
		}

		return errors.Count == 0
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(errors);
	}

	private static bool IsHttpUrl(string value) =>
		Uri.TryCreate(value, UriKind.Absolute, out var uri)
			&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
