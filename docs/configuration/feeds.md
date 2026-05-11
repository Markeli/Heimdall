---
sidebar_position: 4
---

# Feeds

Per-ecosystem feed configuration lives under `heimdall.ecosystems`. Only NuGet
is supported in the MVP.

## Schema

```yaml
heimdall:
  ecosystems:
    nuget:
      feeds:
        - name: <string>             # logical feed identifier (URL path segment)
          upstream: <url>             # absolute http(s) URL of the upstream registry
          cacheTtl: <TimeSpan>        # optional per-feed override
          rules:                      # ordered list of filter rules
            - type: <ruleName>
              <ruleSpecificFields>
```

The `name` becomes the URL path segment — a feed named `strict` serves
`/nuget/strict/v3/...`. Names must be unique within an ecosystem.

## Worked example

```yaml
heimdall:
  ecosystems:
    nuget:
      feeds:
        - name: strict
          upstream: "https://api.nuget.org/v3/index.json"
          cacheTtl: "00:10:00"
          rules:
            - type: minAgeDays
              days: "14"
            - type: allowDeny
              patterns: "Microsoft.*;System.*;Newtonsoft.*;Serilog.*;Polly.*;Microsoft.AspNetCore.*"

        - name: relaxed
          upstream: "https://api.nuget.org/v3/index.json"
          cacheTtl: "00:02:00"
          rules:
            - type: minAgeDays
              days: "1"
```

Two feeds against the same upstream with different policies — useful when CI
should use `strict` and a sandbox environment can pull from `relaxed`.

## Fields

### `name`

Feed identifier. Becomes the URL segment in `/nuget/{name}/v3/...`.
Case-sensitive. Required, non-empty.

### `upstream`

Absolute URL of the upstream NuGet v3 service index. Heimdall fetches this
once at startup (via `NuGetV3UpstreamClient`) to resolve resource URLs, then
caches it for the feed's `cacheTtl`. Required, must be a valid absolute URL.

### `cacheTtl`

Per-feed TTL for registration documents. Optional; falls back to the L1
strand default ([cache configuration](cache.md)). The format is a
.NET `TimeSpan` — `"00:10:00"` for ten minutes, `"1.00:00:00"` for one day.

### `rules`

Ordered list of [filter rules](../rules/overview.md). The pipeline
short-circuits on the first deny — see [Filtering pipeline
architecture](../architecture/filtering-pipeline.md). Two rule types are
shipped:

- [`minAgeDays`](../rules/min-age-days.md) — `days: <integer>`
- [`allowDeny`](../rules/allow-deny.md) — `patterns: <semicolon-separated globs>`

Rules are evaluated per version, on every metadata response and on the
download gate. The exact deny reason is returned in `ProblemDetails` when a
binary is rejected.

## Adding a feed

1. Pick a unique `name` within the `nuget` ecosystem.
2. Decide on the upstream URL (usually `https://api.nuget.org/v3/index.json`).
3. Decide on rules and TTL.
4. Save `config.yml`. The hot-reload picks up the new feed on the next
   request — clients can immediately hit `/nuget/{newName}/v3/index.json`.
