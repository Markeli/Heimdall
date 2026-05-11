using Heimdall.Core.Configuration;

namespace Heimdall.Infrastructure.Configuration;

/// <summary>
/// Maps the YAML-bound <see cref="HeimdallOptions"/> shape onto the immutable
/// <see cref="FeedConfig"/> records exposed by Heimdall.Core. Assumes options have
/// already been validated by <see cref="HeimdallOptionsValidator"/>.
/// </summary>
internal static class FeedConfigMapper
{
	public static IReadOnlyList<FeedConfig> Map(HeimdallOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var feeds = new List<FeedConfig>();
		foreach (var def in options.Ecosystems.NuGet.Feeds)
		{
			feeds.Add(MapFeed("nuget", def));
		}

		return feeds;
	}

	private static FeedConfig MapFeed(string ecosystem, FeedDefinition def)
	{
		var rules = new List<RuleConfig>(def.Rules.Count);
		foreach (var rule in def.Rules)
		{
			if (!rule.TryGetValue("type", out var type) || string.IsNullOrWhiteSpace(type))
			{
				// Validator guarantees rule.type is present; reaching here means the validator was bypassed.
				throw new InvalidOperationException("rule.type is required (validated upstream)");
			}

			var parameters = new Dictionary<string, string?>(StringComparer.Ordinal);
			foreach (var (key, value) in rule)
			{
				if (string.Equals(key, "type", StringComparison.Ordinal))
				{
					continue;
				}
				parameters[key] = value;
			}

			rules.Add(new RuleConfig(type, parameters));
		}

		return new FeedConfig(
			Ecosystem: ecosystem,
			Name: def.Name,
			Upstream: new Uri(def.Upstream, UriKind.Absolute),
			Rules: rules,
			CacheTtl: def.CacheTtl);
	}
}
