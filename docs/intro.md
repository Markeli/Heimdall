---
slug: /
sidebar_position: 1
---

import useBaseUrl from '@docusaurus/useBaseUrl';

# Introduction

Heimdall is a minimalist **dependency firewall** in front of public package
registries. It sits between your developers' build agents and an upstream
registry, enforcing organisational policy on every package version before its
metadata or binaries cross the perimeter.

The policy engine is **registry-agnostic** — the same rule pipeline applies to
any supported ecosystem. NuGet v3 is implemented today; npm, PyPI, Go and Maven
are on the roadmap (see [Supported registries](#supported-registries)).

## What Heimdall does

For any supported registry, Heimdall:

- **Proxies** the registry's read endpoints (service/index documents, version
  listings, package metadata, search, and binary downloads) for one or more
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

## Supported registries

| Registry | | Status |
|---|---|---|
| NuGet v3 | <img src={useBaseUrl('/img/registries/nuget.svg')} alt="NuGet" height="20" /> | ✅ Available today |
| npm | <img src={useBaseUrl('/img/registries/npm.svg')} alt="npm" height="20" /> | 🔜 Planned ([#11](https://github.com/Markeli/Heimdall/issues/11)) |
| PyPI | <img src={useBaseUrl('/img/registries/pypi.svg')} alt="PyPI" height="20" /> | 🔜 Planned ([#12](https://github.com/Markeli/Heimdall/issues/12)) |
| Go modules | <img src={useBaseUrl('/img/registries/go.svg')} alt="Go" height="20" /> | 🔜 Planned ([#13](https://github.com/Markeli/Heimdall/issues/13)) |
| Maven Central | <img src={useBaseUrl('/img/registries/maven.svg')} alt="Maven" height="20" /> | 🔜 Planned ([#14](https://github.com/Markeli/Heimdall/issues/14)) |

Each ecosystem plugs in as an adapter behind the shared rule engine — see the
[Architecture overview](architecture/overview.md) for the extension points.

## When to use Heimdall

- You want a single, auditable choke point in front of a public registry.
- You want a "minimum version age" cooldown to mitigate the
  [event-stream-style](https://snyk.io/blog/malicious-code-found-in-npm-package-event-stream/)
  supply-chain risk window where a fresh malicious release is publicly available.
- You want to whitelist namespaces (`Microsoft.*`, `System.*`, …) and explicitly
  deny others without managing a private mirror.

## When not to use Heimdall (yet)

Heimdall is intentionally small. It **does not** address:

- **Distributed L2 cache.** The HybridCache L2 strand uses an in-memory stub.
  Multi-instance deployments share nothing.
- **CVE / vulnerability rules.** No CVE database is consulted.
- **Authentication.** Heimdall trusts anonymous traffic from inside the
  perimeter; put a reverse proxy in front for anything else.
- **Horizontal scaling.** One instance per perimeter for now.

These are out of scope and tracked separately. Registries other than NuGet are
not a "won't do" — they are [planned work](#supported-registries).

## Where to go next

- [Quick start](getting-started/quick-start.md) — run Heimdall locally, point a
  `dotnet nuget` client at it, watch a young version disappear from the listing.
- [Configuration overview](configuration/overview.md) — the YAML schema and
  layering.
- [Filtering rules](rules/overview.md) — what each rule means.
- [Architecture overview](architecture/overview.md) — how the pieces fit
  together.
