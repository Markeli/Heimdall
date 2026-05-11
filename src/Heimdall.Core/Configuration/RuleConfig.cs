namespace Heimdall.Core.Configuration;

/// <summary>
/// Untyped, declarative rule configuration. The <see cref="Type"/> selects an
/// <see cref="Heimdall.Core.Filtering.IRuleBuilder"/>, which interprets <see cref="Parameters"/>
/// and constructs the concrete rule.
/// </summary>
/// <param name="Type">Discriminator selecting the rule builder (e.g. <c>minAgeDays</c>, <c>allowDeny</c>).</param>
/// <param name="Parameters">Free-form string parameters consumed by the matching builder.</param>
public sealed record RuleConfig(string Type, IReadOnlyDictionary<string, string?> Parameters);
