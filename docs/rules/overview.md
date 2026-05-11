---
sidebar_position: 1
---

# Filtering rules overview

Every feed has an ordered list of rules. A version is **allowed** when every
rule allows it, and **denied** the moment one rule rejects it. There is no
"override" or "exception" rule type — the pipeline is monotonic.

## Built-in rules

| `type` | Page | What it does |
|---|---|---|
| `minAgeDays` | [`minAgeDays`](min-age-days.md) | Rejects versions younger than _N_ days. |
| `allowDeny` | [`allowDeny`](allow-deny.md) | Glob-based allow/deny on package identifier. |

## Evaluation order

Rules run in the order written in `config.yml`. The first deny wins —
subsequent rules are not consulted. This is intentional: cheap rules
(`allowDeny`) come first so expensive ones (`minAgeDays`, which needs
upstream metadata to know `published`) do less work.

```yaml
rules:
  - type: allowDeny
    patterns: "Microsoft.*;System.*"
  - type: minAgeDays
    days: "14"
```

A package called `Some.Random` is rejected by the first rule and the age
check never runs.

## Deny reasons

Every deny verdict carries a `RuleName` and a human-readable `Message`. They
surface in two places:

1. **`/nuget/{feed}/v3/flatcontainer/{id}/{ver}/{file}.nupkg`** — when a
   download is rejected, the response is `403 ProblemDetails` with the
   reason in the body:
   ```json
   {
     "type": "https://heimdall.local/problems/blocked",
     "title": "package version blocked",
     "status": 403,
     "ruleName": "minAgeDays",
     "detail": "version published 0.3 days ago, requires 14"
   }
   ```
2. **Audit log** ([logging](../configuration/logging.md)) — every decision is
   logged at info level under `Heimdall.Audit` when audit is enabled.

## Where rules apply

Rules run wherever a version is evaluated:

- **Listing** — `flatcontainer/{id}/index.json` returns only allowed
  versions.
- **Registration** — entries from registration documents are pruned.
- **Search** — search hits are pruned.
- **Download gate** — the `.nupkg` endpoint re-evaluates the requested
  version before streaming; this catches clients that have a stale listing
  cached.

## Combining rules

Most feeds use a `minAgeDays` floor plus an `allowDeny` whitelist:

```yaml
rules:
  - type: minAgeDays
    days: "14"
  - type: allowDeny
    patterns: "Microsoft.*;System.*;Newtonsoft.*;!*.Internal"
```

A "deny only" `allowDeny` (no allow patterns) is sometimes useful when you
want to block a specific compromised package across an otherwise open feed:

```yaml
rules:
  - type: allowDeny
    patterns: "!Compromised.Package"
```

## Extending

The MVP ships two rules. The interface (`IRule` plus `IRuleBuilder` and the
`RuleFactory`) is in `src/Heimdall.Core/Filtering` and is meant to grow.
Adding a CVE/vulnerability rule, an SBOM-driven allow-list, or a
license-whitelist rule is straightforward but out of scope for the MVP.
