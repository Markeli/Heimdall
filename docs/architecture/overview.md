---
sidebar_position: 1
---

# Architecture overview

Heimdall is built around one **registry-agnostic processing flow**. Everything
that is the same for every registry — resolving a feed, caching metadata,
running the filter rules, gating downloads — lives in a shared core; everything
that is specific to a registry (its wire protocol, URL shapes, metadata model)
lives behind an ecosystem adapter. Adding npm, PyPI, Go or Maven means writing
a new adapter, not touching the flow.

## The universal flow

Every read request, regardless of ecosystem, follows the same five steps:

```
request ──► 1. resolve feed config        IFeedConfigLookup.TryGet(ecosystem, feed)
            2. fetch upstream metadata     ecosystem adapter → upstream client
                                           (memoized in HybridCache; key carries
                                            the config-snapshot generation)
            3. filter versions             RuleEvaluator over the feed's IRule list
                                           (VersionListFilter for lists)
            4. rewrite URLs                @id / download links → publicBaseUrl
            5. respond                     200 + projected, filtered payload
```

The **download** path adds a gate instead of a list filter: before a single
`.nupkg`/tarball/etc. is streamed, `SingleVersionGate` re-runs the same rules on
that one version. This is deliberate — a client must not be able to bypass the
listing filter by requesting a binary URL directly. On a deny, the binary never
starts streaming and the client gets `403 ProblemDetails` naming the rule.

```
download ──► resolve feed ─► SingleVersionGate.Evaluate(version, rules)
                              ├── Allow → stream upstream body through (no disk)
                              └── Deny  → 403 ProblemDetails (ruleName + reason)
```

Steps 1, 3 and 5's gating are **ecosystem-independent**; steps 2 and 4 are the
adapter's job. That split is the whole architecture.

## Core vs. adapter

### `Heimdall.Core` — the registry-agnostic engine

- `PackageCoordinates` — `(Ecosystem, Id, SemVersion)`. The "coordinates" name
  is borrowed from Maven so it survives the move beyond NuGet.
- `PackageVersionMetadata` — coordinates plus the optional publication timestamp
  plus ecosystem-specific extras.
- `IRule` / `RuleVerdict` / `RuleEvaluator` — the pure filter pipeline. See
  [Filtering pipeline](filtering-pipeline.md).
- `VersionListFilter` / `SingleVersionGate` — the list-filter and single-version
  gate that wrap the evaluator (steps 3 and the download gate).
- `IConfigSnapshotProvider` / `IFeedConfigLookup` — the configuration contracts
  (step 1), implemented in Infrastructure.

Nothing here knows what NuGet is. A new ecosystem reuses all of it unchanged.

### `Heimdall.Ecosystems.NuGet` — a worked adapter

Everything NuGet v3 specific sits behind ecosystem-shaped interfaces — the
template a future ecosystem copies:

- `INuGetV3UpstreamClient` (Polly-backed `HttpClient`) — the only code that talks
  to nuget.org (step 2).
- `NuGetV3MetadataService` — orchestrates the five steps for NuGet: fetch,
  cache, filter, rewrite, project.
- `NuGetV3MetadataTransformer` — the filter-and-rewrite pass over registration
  documents (steps 3–4).
- `NuGetV3UrlRewriter` — builds Heimdall URLs from `publicBaseUrl` + feed name.
- `NuGetV3MetadataProjection` — projects registration documents into the
  flat-container versions list and the search-result shape (step 5).

### `Heimdall.Infrastructure` — cross-cutting plumbing

Binds and validates `HeimdallOptions` from the `heimdall:` YAML section,
registers `HybridCache` (with an in-memory `IDistributedCache` stub for the L2
strand — see [Caching](caching.md)), and provides `ConfigSnapshotProvider` (the
monotonic generation token that scopes cache keys) and `FeedConfigLookup`.

### `Heimdall.Api` — the host

Composes the layers via DI extensions (`AddHeimdallCore`,
`AddHeimdallInfrastructure`, `AddNuGetV3Ecosystem`) and exposes the controllers
(`NuGetV3MetadataController`, `NuGetV3BinaryController`, `HealthController`),
`MapMetrics()` (`/metrics`), and per-request logging.

## Adding a new ecosystem

The flow already exists; an adapter provides the registry-specific pieces:

1. A model that maps the registry's metadata onto `PackageVersionMetadata`.
2. An upstream client (step 2) and a URL rewriter (step 4) for that protocol.
3. A metadata service that runs the five steps and a projection that emits the
   registry's native response shapes.
4. Controllers exposing that registry's endpoints, plus an `Add…Ecosystem` DI
   extension wired up in `Heimdall.Api`.

The rules (`minAgeDays`, `allowDeny`, …) and the cache are inherited for free —
they operate on `PackageVersionMetadata`, not on any registry's wire format.

## What is and is not cached

- **Cached** — package metadata documents. Keyed by
  `(ecosystem, feed, packageId, configSnapshot)` with a per-feed TTL.
- **Not cached** — binaries (streamed through; caching them is a non-goal for
  the MVP) and search results (per-query, low reuse).

See [Caching](caching.md) for the design rationale.
