---
sidebar_position: 2
---

# Caching

This page is the **designer** view — _what_ is cached, _why_ HybridCache,
and which design trade-offs to be aware of. For operator-facing tuning, see
[Configuration / Cache](../configuration/cache.md).

## What gets cached

| Thing | Cached? | Why |
|---|---|---|
| NuGet v3 registration documents | **Yes** | Hot path. Same package id requested by every restore. |
| Versions list (derived from registration) | Indirectly | Computed from the cached registration. |
| Search responses | No | Per-query, very low reuse. |
| `.nupkg` binaries | No | Multi-MB files; streaming is cheap, mirroring is a separate problem. |
| Service index | No | Built from config; near-zero cost. |

The cache is keyed by `(ecosystem, feed, packageId, snapshotGeneration)`.
The snapshot generation bumps on every successful config reload, so a rule
change invalidates the cache without an explicit flush.

## Why HybridCache

Until commit [`15d927c`](https://github.com/Markeli/Heimdall/commit/15d927c)
Heimdall ran a hand-rolled L1/L2 cache. We replaced it with
[`Microsoft.Extensions.Caching.Hybrid`](https://learn.microsoft.com/en-us/dotnet/core/extensions/caching)
because it solved three real problems for free:

1. **Stampede protection.** Concurrent misses for the same key collapse
   into a single upstream fetch. Critical when CI fleets all start
   `restore` at the same minute.
2. **L1/L2 layering.** One contract over the in-process and distributed
   strands; we can swap the L2 backend without touching consumers.
3. **Serialization done right.** Documents are stored as `byte[]` with the
   System.Text.Json serializer and a versioned key — no homegrown stale
   detection.

The trade-off is one more first-party dependency, but it ships with the
.NET runtime today, so the cost is mostly conceptual.

## L1 vs L2

- **L1** — in-process `IMemoryCache`-backed strand. Always on. Default TTL
  configurable via [`heimdall.cache.l1.defaultTtl`](../configuration/cache.md).
- **L2** — distributed strand. Wired via `IDistributedCache`. In the MVP
  this is `services.AddDistributedMemoryCache()` — an **in-memory stub**.
  When `services.AddStackExchangeRedisCache(...)` lands, the consumer
  contract (`HybridCache`) does not change.

Multi-instance deployments today share **no** cache state — every Heimdall
pod populates its own L1 from scratch.

## Eviction

HybridCache evicts on:

- TTL (per-feed `cacheTtl`, falling back to the strand default).
- Memory pressure (the underlying `IMemoryCache` reacts to GC events).
- Snapshot change (because the snapshot generation is part of the key, a
  config reload effectively orphans the previous generation's entries).

There is no manual flush endpoint. Hot-reloading `config.yml` is the
intended way to drop the cache when rules change.

## What we did **not** build

- A binary mirror (".nupkg locally on disk"). Streaming is fast; mirroring
  buys storage churn we don't need yet.
- Negative caching for 404s. The upstream rarely returns 404 for valid
  packages, and we prefer fresh failures over stale "no such package"
  answers.
- A separate cache for catalog entries. They are folded into the
  registration document we cache anyway.
