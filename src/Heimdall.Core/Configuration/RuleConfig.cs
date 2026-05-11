namespace Heimdall.Core.Configuration;

public sealed record RuleConfig(string Type, IReadOnlyDictionary<string, string?> Parameters);
