---
sidebar_position: 3
---

# Filtering pipeline

The contract that makes "minAgeDays" and "allowDeny" plug into the same
machine, and the place to look when adding a new rule.

## The contract

```csharp
public interface IRule
{
    string Name { get; }
    RuleVerdict Evaluate(PackageVersionMetadata meta, RuleContext ctx);
}
```

- `Name` is the configuration discriminator and shows up in deny reasons
  and logs. Rule implementations expose this as a `const`
  (`MinAgeDaysRule.RuleName = "minAgeDays"`).
- `Evaluate` is **pure**. No I/O, no side effects, no clock — `NowUtc`
  comes in via `RuleContext` so the entire pipeline can be exercised
  deterministically in tests.

The verdict is a sealed record:

```csharp
public sealed record RuleVerdict(FilterDecision Decision, FilterReason? Reason)
{
    public static RuleVerdict Allow { get; }
    public static RuleVerdict Deny(string ruleName, string message);
}
```

`FilterReason` is `(RuleName, Message)` and is what surfaces in
`ProblemDetails` and the audit log.

## The evaluator

`RuleEvaluator` walks the configured rules in order and short-circuits on
the first deny. There is no allow-override pattern — once a rule denies,
the verdict is final.

```csharp
foreach (var rule in rules)
{
    var verdict = rule.Evaluate(meta, ctx);
    if (verdict.IsDeny) return verdict;
}
return RuleVerdict.Allow;
```

Two helpers wrap this:

- `VersionListFilter` (and `IVersionListFilter`) — filters an enumerable of
  `PackageVersionMetadata` (used on listing / registration / search).
- `SingleVersionGate` (and `ISingleVersionGate`) — evaluates one version,
  used by the binary download gate.

The download gate exists so a client cannot bypass the listing filter by
asking for a `.nupkg` directly — Heimdall re-runs the rules at the gate.

## Building rules from config

`FeedDefinition.Rules` is a list of `Dictionary<string, string?>` — loose
typing on purpose. Each entry has at minimum a `type` key plus
rule-specific fields. The factory chain is:

```
RuleFactory  →  IRuleBuilder (one per rule type)  →  IRule
```

`IRuleBuilder` is one builder per rule type. `RuleFactory` looks up the
right builder by `type` and delegates. Adding a rule means:

1. Implement `IRule` (a single method) in
   `Heimdall.Core.Filtering.Rules`.
2. Implement `IRuleBuilder` for parsing the config dictionary into your
   rule instance.
3. Register the builder in `CoreServiceCollectionExtensions`. The factory
   picks it up automatically.

The MVP ships two implementations as the worked examples:
`MinAgeDaysRule` + `MinAgeDaysRuleBuilder`, and `AllowDenyRule` +
`AllowDenyRuleBuilder`.

## How the gate uses the pipeline

The download flow:

```
GET /nuget/{feed}/v3/flatcontainer/{id}/{ver}/{file}.nupkg
  → NuGetV3BinaryController.Download
    → NuGetV3BinaryProxyService.ProxyAsync
      → SingleVersionGate.Evaluate(meta, rules, ctx)
        ├── Allow → stream upstream body to client
        └── Deny  → 403 ProblemDetails with ruleName + detail
```

The `ProblemDetails` carries the `FilterReason` straight through, so a
build agent's error message names the rule the operator can grep for in
configuration.

## Why "deny wins"

Two competing models existed at design time:

1. **Deny wins, no override.** The current model. Simple, predictable,
   short-circuits early.
2. **Last-rule-wins with allow overrides.** More expressive, but creates
   confusing interactions (a deeply-buried allow can resurrect a denied
   package).

Deny-wins is the safer default for a security gate. If we later need
"approve specific versions" workflow, it grows into the existing
`ISingleVersionGate` as a separate, explicit code path rather than as a
rule type.
