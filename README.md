# Heimdall

![CI](https://github.com/Markeli/Heimdall/actions/workflows/ci.yml/badge.svg)
![docs](https://github.com/Markeli/Heimdall/actions/workflows/docs.yml/badge.svg)

A minimalist **dependency firewall** in front of public package registries.
Heimdall sits between your build agents and an upstream registry and enforces
organisational policy on every package version before it crosses the
perimeter — package files are streamed through, metadata is cached in memory.

## Why Heimdall

Public registries are a supply-chain attack surface: a freshly published
malicious version is available to your builds the moment it lands upstream, and
nothing stops a typo-squatted or unwanted package from being pulled. Heimdall
gives you one **auditable choke point** to:

- impose a **minimum version age** cooldown, so freshly published (and
  not-yet-detected-as-malicious) versions are invisible until they age out;
- **allow/deny by package id**, so only the namespaces you trust flow through;
- see **what crossed the perimeter** via structured logs and an audit trail —
  without standing up and curating a full private mirror.

The policy engine is registry-agnostic; each ecosystem plugs in as an adapter.

## Supported registries

| Registry | | Status |
|---|---|---|
| NuGet v3 | <img src="website/static/img/registries/nuget.svg" alt="NuGet" height="20"> | ✅ Available today |
| npm | <img src="website/static/img/registries/npm.svg" alt="npm" height="20"> | 🔜 Planned ([#11](https://github.com/Markeli/Heimdall/issues/11)) |
| PyPI | <img src="website/static/img/registries/pypi.svg" alt="PyPI" height="20"> | 🔜 Planned ([#12](https://github.com/Markeli/Heimdall/issues/12)) |
| Go modules | <img src="website/static/img/registries/go.svg" alt="Go" height="20"> | 🔜 Planned ([#13](https://github.com/Markeli/Heimdall/issues/13)) |
| Maven Central | <img src="website/static/img/registries/maven.svg" alt="Maven" height="20"> | 🔜 Planned ([#14](https://github.com/Markeli/Heimdall/issues/14)) |

## Quick start

```sh
docker pull ghcr.io/markeli/heimdall:latest
docker run --rm -p 8080:8080 -v $(pwd)/config.yml:/app/config.yml ghcr.io/markeli/heimdall:latest
```

A minimal `config.yml`:

```yaml
heimdall:
  server:
    publicBaseUrl: "http://localhost:8080"   # required — used to rewrite @id URLs
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
              patterns: "Microsoft.*;System.*;!Internal.*"
```

Point a client at the feed and pull as usual:

```sh
dotnet nuget add source http://localhost:8080/nuget/strict/v3/index.json -n heimdall-strict
dotnet add package Newtonsoft.Json
```

Versions younger than `minAgeDays` disappear from the listing; downloading a
disallowed version returns `403 ProblemDetails` naming the rule that blocked it.

## Documentation

Full documentation — configuration, rules, the API, operations, and
architecture — is published at <https://markeli.github.io/Heimdall/>.

## Out of scope (MVP)

L2 Redis cache (contract is in place via DI), CVE/vulnerability rules,
authentication (anonymous access inside the perimeter), and multi-instance
scaling. Additional ecosystems (npm/PyPI/Go/Maven) are tracked as separate
issues — see the table above.

## Releases

Heimdall uses [Semantic Versioning](https://semver.org/) with
[MinVer](https://github.com/adamralph/minver) deriving assembly versions from
`v*.*.*` git tags. Pushing a tag of the form `vX.Y.Z` triggers the `release`
workflow, which re-runs the full test suite, builds and pushes the container
image to GHCR (`ghcr.io/markeli/heimdall:X.Y.Z` and `:latest`), and creates a
GitHub Release whose body is the matching `[X.Y.Z]` section of
[`CHANGELOG.md`](CHANGELOG.md). The full procedure lives in
[`CONTRIBUTING.md`](CONTRIBUTING.md#5-releasing).

## Contributing

Read [`CONTRIBUTING.md`](CONTRIBUTING.md) before opening a PR; building, testing
and the architecture are documented on the
[docs site](https://markeli.github.io/Heimdall/). AI agents working on this repo
must also follow [`AGENTS.md`](AGENTS.md), which mirrors the same rules in a
machine-readable form.
