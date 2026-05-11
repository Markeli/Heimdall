# Heimdall

![CI](https://github.com/Markeli/Heimdall/actions/workflows/ci.yml/badge.svg)
![docs](https://github.com/Markeli/Heimdall/actions/workflows/docs.yml/badge.svg)

Internal proxy for public package repositories (NuGet — MVP; npm/Maven later) with filtering rules (minimum version age, allow/deny by package name). Package files are streamed through; metadata is cached in memory.

## Documentation

Full documentation is published at <https://markeli.github.io/Heimdall/> (built from [`docs/`](docs/) via Docusaurus 3 — see [`website/`](website/)). The README below stays as a quick reference; the site goes deeper on configuration, rules, the API, operations, and architecture.

## Stack

- .NET 10, ASP.NET Core MVC controllers
- HttpClient + Polly (`Microsoft.Extensions.Http.Resilience`) for upstream calls
- Serilog → JSON to stdout
- prometheus-net → `/metrics`
- xUnit + WireMock.Net for tests
- Configuration — YAML with hot-reload

## Layout

```
src/
  Heimdall.Core              # models, contracts, filters, rules
  Heimdall.Infrastructure    # cache, YAML config, generation
  Heimdall.Ecosystems.NuGet  # NuGet V3 specifics
  Heimdall.Api               # ASP.NET Core host, controllers
tests/
  Heimdall.UnitTests
  Heimdall.IntegrationTests  # WebApplicationFactory + WireMock.Net
```

## Running locally

```sh
dotnet run --project src/Heimdall.Api
```

The server listens on `http://localhost:8080`. Configuration sits next to `Heimdall.Api.dll` as `config.yml`, with optional `config.{Environment}.yml` and `config.secret.yml` (gitignored) overrides on top. All files are hot-reloaded on change.

## Build (Cake)

Build, test, and publish go through Cake so local and Docker builds produce identical artifacts.

```sh
dotnet tool restore
dotnet cake --target=Test      # restore + build + test
dotnet cake --target=Publish   # produces ./artifacts/publish/
```

The Docker image (`src/Heimdall.Api/Dockerfile`) calls the same `Publish` target inside the SDK image.

## Wiring up a client

```sh
dotnet nuget add source http://localhost:8080/nuget/strict/v3/index.json -n heimdall-strict
dotnet add package Newtonsoft.Json
```

Versions younger than `minAgeDays` are filtered out of `flatcontainer/{id}/index.json`. Attempting to download a disallowed version returns `403 ProblemDetails` naming the rule that blocked it.

## Configuration (YAML)

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

### `allowDeny` semantics
- Glob (`*`, `?`), case-insensitive. The `!` prefix marks a deny-pattern.
- Any deny match → version rejected (deny wins).
- If at least one allow-pattern is present, the package must match at least one of them; otherwise it is denied.
- Deny-only patterns → everything is allowed except the denied set.

### `minAgeDays` semantics
- A version is allowed when `now - catalogEntry.published >= days`.
- `published == null` → denied (safeguard against corrupted/unlisted metadata).

## Endpoints

| Path | Purpose |
|---|---|
| `GET /nuget/{feed}/v3/index.json` | Service index (URLs point back at Heimdall) |
| `GET /nuget/{feed}/v3/flatcontainer/{id}/index.json` | List of allowed versions |
| `GET /nuget/{feed}/v3/registration5-gz-semver2/{id}/index.json` | Registration with filtering + URL rewrite |
| `GET /nuget/{feed}/v3/query?q=...` | Search with filtering |
| `GET\|HEAD /nuget/{feed}/v3/flatcontainer/{id}/{ver}/{file}.nupkg` | Download via gate + stream |
| `GET /healthz` | Liveness (200 OK) |
| `GET /readyz` | Readiness (checks upstream reachability) |
| `GET /metrics` | Prometheus metrics |

## Tests

```sh
dotnet test
```

- 42 unit tests: rules, filters, cache, config, transformer, URL rewriter.
- 9 integration tests: WebApplicationFactory + WireMock.Net (service index, listing, download allow/deny, health, hot-reload).

## Docker

```sh
docker build -f src/Heimdall.Api/Dockerfile -t heimdall:dev .
docker run --rm -p 8080:8080 -v $(pwd)/config.yml:/app/config.yml heimdall:dev
```

## Out of scope (MVP)

- L2 Redis (contract is in place, registered through DI).
- npm/Maven ecosystems.
- CVE / vulnerability rules.
- Auth (anonymous access inside the perimeter).
- Multi-instance scaling.

See `/Users/markelow/.claude/plans/expressive-skipping-beacon.md` — the final architectural plan.

## Releases

Heimdall uses [Semantic Versioning](https://semver.org/) with
[MinVer](https://github.com/adamralph/minver) deriving assembly versions from
`v*.*.*` git tags. Pushing a tag of the form `vX.Y.Z` triggers the `release`
workflow, which:

- re-runs the full test suite,
- builds and pushes the container image to GHCR:
  - `ghcr.io/markeli/heimdall:X.Y.Z`
  - `ghcr.io/markeli/heimdall:latest`
- creates a GitHub Release whose body is the matching `[X.Y.Z]` section of
  [`CHANGELOG.md`](CHANGELOG.md).

Pull the latest stable image with:

```sh
docker pull ghcr.io/markeli/heimdall:latest
```

The full release procedure lives in [`CONTRIBUTING.md`](CONTRIBUTING.md#5-releasing).

## Contributing

Read [`CONTRIBUTING.md`](CONTRIBUTING.md) before opening a PR. AI agents
working on this repo must also follow [`AGENTS.md`](AGENTS.md), which mirrors
the same rules in a machine-readable form.
