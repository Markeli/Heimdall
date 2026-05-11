---
slug: /
sidebar_position: 1
---

# Introduction

Heimdall is an internal proxy for public package repositories. It sits between
your developers' build agents and an upstream registry such as
[nuget.org](https://www.nuget.org/), enforcing organisational rules on every
version before metadata or binaries cross the perimeter.

The MVP supports the **NuGet v3 protocol**. npm and Maven are on the roadmap.

## What Heimdall does

- **Proxies** all NuGet v3 endpoints (service index, flat-container listings,
  registration documents, search, and `.nupkg` downloads) for one or more
  upstream feeds.
- **Filters** versions through a configurable rule pipeline:
  - [`minAgeDays`](rules/min-age-days.md) — reject versions younger than _N_ days.
  - [`allowDeny`](rules/allow-deny.md) — glob-based allow/deny on the package id.
- **Streams** binaries from upstream after the policy gate accepts them; metadata
  is cached locally with [`HybridCache`](architecture/caching.md).
- **Observes itself** with structured Serilog JSON logs, Prometheus metrics, and
  Kubernetes-style liveness / readiness probes.

When a download is rejected, Heimdall returns `403 ProblemDetails` naming the
rule that blocked it — the build agent sees an actionable error, not a silent
404.

## When to use Heimdall

- You want a single, auditable choke point in front of nuget.org.
- You want a "minimum version age" cooldown to mitigate the
  [event-stream-style](https://snyk.io/blog/malicious-code-found-in-npm-package-event-stream/)
  supply-chain risk window where a fresh malicious release is publicly available.
- You want to whitelist namespaces (`Microsoft.*`, `System.*`, …) and explicitly
  deny others without managing a private mirror.

## When not to use Heimdall (yet)

Heimdall is intentionally small. The MVP **does not** address:

- **Distributed L2 cache.** The HybridCache L2 strand uses an in-memory stub.
  Multi-instance deployments share nothing.
- **npm or Maven.** Only NuGet v3 is implemented.
- **CVE / vulnerability rules.** No CVE database is consulted.
- **Authentication.** Heimdall trusts anonymous traffic from inside the
  perimeter; put a reverse proxy in front for anything else.
- **Horizontal scaling.** One instance per perimeter for now.

These are out of scope for the MVP and tracked separately.

## Where to go next

- [Quick start](getting-started/quick-start.md) — run Heimdall locally, point a
  `dotnet nuget` client at it, watch a young version disappear from the listing.
- [Configuration overview](configuration/overview.md) — the YAML schema and how
  hot reload works.
- [Filtering rules](rules/overview.md) — what each rule means.
- [Architecture overview](architecture/overview.md) — how the pieces fit
  together.
