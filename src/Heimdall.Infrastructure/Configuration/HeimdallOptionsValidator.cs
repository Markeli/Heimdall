using Microsoft.Extensions.Options;

namespace Heimdall.Infrastructure.Configuration;

public sealed class HeimdallOptionsValidator : IValidateOptions<HeimdallOptions>
{
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
