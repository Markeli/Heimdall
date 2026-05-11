---
sidebar_position: 3
---

# Cache

Tunables for the L1 / L2 cache strands live under `heimdall.cache`. This page
is the **operator** view (what to set); see
[Architecture / Caching](../architecture/caching.md) for the design story.

## Schema

```yaml
heimdall:
  cache:
    l1:
      maxEntries: 50000
      defaultTtl: "00:05:00"
      provider: "memory"
    l2:
      maxEntries: 50000
      defaultTtl: "00:05:00"
      provider: "none"
```

## L1 — in-process

`l1` is the in-memory strand backed by `Microsoft.Extensions.Caching.Hybrid`.
It always exists and is consulted on every request.

| Key | Default | Notes |
|-----|---------|-------|
| `provider` | `memory` | The MVP has no other backends here. |
| `maxEntries` | `50000` | Soft cap. |
| `defaultTtl` | `00:05:00` | Applied when an entry is stored without an explicit TTL. |

The TTL is a hint; HybridCache may evict earlier under memory pressure.

## L2 — distributed (stub)

The L2 strand is in place but unwired in the MVP — `provider: "none"` is the
default and the only supported value. The DI registration uses an in-memory
`IDistributedCache` stub so the HybridCache contract still works in tests and
integration, but multiple Heimdall instances share nothing.

When Redis lands, configure it here:

```yaml
heimdall:
  cache:
    l2:
      provider: "redis"
      defaultTtl: "00:30:00"
```

Until then, treat L2 settings as forward-compatible placeholders.

## Per-feed TTL override

Each feed in `heimdall.ecosystems.nuget.feeds` may define its own `cacheTtl`
to override the strand default for **that feed's** registration documents.
See [Feeds](feeds.md#cachettl).
