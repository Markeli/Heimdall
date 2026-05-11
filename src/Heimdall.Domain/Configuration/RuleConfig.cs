namespace Heimdall.Domain.Configuration;

public sealed record RuleConfig(string Type, IReadOnlyDictionary<string, string?> Parameters);
