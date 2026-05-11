---
sidebar_position: 1
---

# Architecture overview

Heimdall is a small ASP.NET Core MVC service split into four projects under
`src/`. The split exists so each layer is testable in isolation and so a
future second ecosystem (npm, Maven) plugs in without touching the API
host.

## Project layout

```
src/
  Heimdall.Core              # models, contracts, filters, rules
  Heimdall.Infrastructure    # config binding, validation, cache wiring
  Heimdall.Ecosystems.NuGet  # NuGet v3 specifics
  Heimdall.Api               # ASP.NET Core host and controllers
tests/
  Heimdall.UnitTests
  Heimdall.IntegrationTests  # WebApplicationFactory + WireMock.Net
```

### `Heimdall.Core`

Domain types and the filter pipeline:

- `PackageCoordinates` — `(Ecosystem, Id, SemVersion)` tuple. The
  "coordinates" name is borrowed from Maven so it survives the move beyond
  NuGet.
- `PackageVersionMetadata` — coordinates plus the optional publication
  timestamp plus ecosystem-specific extras.
- `IRule` / `RuleVerdict` / `RuleEvaluator` — the filter pipeline. See
  [Filtering pipeline](filtering-pipeline.md) for details.
- `IConfigSnapshotProvider` / `IFeedConfigLookup` — the contract layer for
  configuration, implemented in Infrastructure.

### `Heimdall.Infrastructure`

Cross-cutting plumbing:

- Binds `HeimdallOptions` from the `heimdall:` YAML section, with
  validation via `HeimdallOptionsValidator`.
- Registers `Microsoft.Extensions.Caching.Hybrid` with an in-memory
  `IDistributedCache` stub for the L2 strand (see
  [Caching](caching.md)).
- Provides `ConfigSnapshotProvider`, the monotonic snapshot service that
  lets cache keys include a generation token.
- Provides `FeedConfigLookup`, the per-request lookup used by controllers.

### `Heimdall.Ecosystems.NuGet`

Everything NuGet v3 specific lives behind ecosystem-shaped interfaces:

- `INuGetV3UpstreamClient` (Polly-backed HttpClient) — the only place that
  talks to nuget.org.
- `NuGetV3MetadataService` — orchestrates upstream fetches, runs filters,
  rewrites URLs, and memoizes registration documents via `HybridCache`.
- `NuGetV3MetadataTransformer` — the filter-and-rewrite pass applied to
  registration documents.
- `NuGetV3UrlRewriter` — produces Heimdall URLs from `publicBaseUrl` + feed
  name.
- `NuGetV3MetadataProjection` — projects registration documents into the
  flat-container versions list and the search-result shape.

### `Heimdall.Api`

The ASP.NET Core host. Composes the layers via DI extensions
(`AddHeimdallCore`, `AddHeimdallInfrastructure`, `AddNuGetV3Ecosystem`) and
exposes three controllers:

- `NuGetV3MetadataController` — service index, versions list, registration,
  search.
- `NuGetV3BinaryController` — `.nupkg` download gate (delegates to
  `NuGetV3BinaryProxyService`).
- `HealthController` — `/healthz` and `/readyz`.

Plus `MapMetrics()` (`/metrics`) and `UseSerilogRequestLogging()`.

## Request flow

A typical "give me the versions of package X on feed Y" request:

```
client
  └── GET /nuget/Y/v3/flatcontainer/x/index.json
       NuGetV3MetadataController.GetVersionsList
        └── NuGetV3MetadataService
             ├── IFeedConfigLookup.TryGet("nuget", Y)         ← feed config
             ├── HybridCache.GetOrCreateAsync(key)            ← L1, then L2 stub
             │     └── INuGetV3UpstreamClient.FetchRegistration ← only on miss
             ├── NuGetV3MetadataTransformer.Apply(rules)      ← filter + rewrite
             └── NuGetV3MetadataProjection.ToVersionsList     ← extract versions
       returns 200, JSON
```

The download path is similar but additionally streams the upstream response
body through `NuGetV3BinaryProxyService` so the binary never lands on disk.

## What is and is not cached

- **Cached** — registration documents (the metadata for a package across
  all versions). Keyed by `(ecosystem, feed, packageId, configSnapshot)`
  with a per-feed TTL.
- **Not cached** — `.nupkg` binaries. They are streamed through; caching
  binaries is an explicit non-goal for the MVP.
- **Not cached** — search results (per-query, low reuse).

See [Caching](caching.md) for the design rationale.

## DI graph at a glance

```
HeimdallOptions  ──┐
                   ├──► HeimdallOptionsValidator
HybridCache  ──────┤
ConfigGeneration ──┤
ConfigSnapshotProv─┤
FeedConfigLookup ──┤
                   │
RuleFactory ───────┤
RuleEvaluator ─────┤
VersionListFilter ─┤
SingleVersionGate ─┘
   │
   └──► NuGetV3UpstreamClient (Polly resilience)
        NuGetV3MetadataTransformer
        NuGetV3UrlRewriter
        NuGetV3MetadataProjection
        NuGetV3MetadataService
            │
            └──► Controllers
                 NuGetV3BinaryProxyService
                 UpstreamReadinessCheck
                 AuditLogger
```
