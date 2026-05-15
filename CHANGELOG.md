# Changelog

All notable changes to Heimdall are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Smoke test suite (`tests/Heimdall.SmokeTests/`) that drives a running Heimdall
  container against the real `api.nuget.org` upstream. Read-path coverage:
  service index URL rewrite, flat-container versions list, registration,
  search, `.nupkg` GET and HEAD, and an unknown-feed 404. Filter-rule coverage
  (via `tests/Heimdall.SmokeTests/config.smoke.yml`, bind-mounted into the
  container in release CI): `allowDeny` allow-pattern admits matching packages
  and blocks non-matching ones in both listing and download; `allowDeny`
  deny-pattern blocks matching packages and lets non-matching through; an
  `age-locked` feed whose `minAgeDays` is computed at smoke-run time as
  `(now − Newtonsoft.Json 12.0.3 published date) + 1 day` (anchor:
  2019-11-09) and injected via `envsubst`, so the rule always rejects the
  anchor version in both listing and download — no magic numbers, no
  time-dependent drift. Deliberately kept out of `Heimdall.sln` so
  `dotnet cake --target=Test` does not pull it in.
- `samples/nuget-consumer/`: a minimal project whose `NuGet.config` points
  exclusively at Heimdall, used by release CI to validate that `dotnet restore`
  works end-to-end through the proxy.
- Release workflow now builds the image locally, smoke-tests it against the
  running container (`/readyz` poll up to 120 s, smoke suite, sample restore),
  and only pushes to GHCR after smoke passes — preventing a half-released state
  where `:latest` lands ahead of a red smoke run.
- npm smoke coverage is intentionally deferred: Heimdall does not yet implement
  an npm ecosystem, so there is nothing to smoke. It will follow once npm is
  added.
- CI/CD pipeline on GitHub Actions: `ci` workflow (build + test + changelog guard)
  and `release` workflow (tag-triggered SemVer release, GHCR image push, release
  notes extracted from this file).
- MinVer-driven assembly versioning for `Heimdall.Api` (derived from `v*.*.*` tags).
- `CONTRIBUTING.md` and `AGENTS.md` codifying branching, PR, release, and code
  style rules for human and AI contributors.
- PR template and issue forms (`bug_report`, `feature_request`) under `.github/`.
- Documentation site (Docusaurus 3) under `website/` with source markdown in
  `docs/`, deployed to GitHub Pages via `.github/workflows/docs.yml`. Covers
  installation, configuration, filtering rules, API, operations, and
  architecture.
- `CLAUDE.md` at the repository root pointing Claude Code users at `AGENTS.md`.

### Security
- Pin `serialize-javascript` to `^7.0.5` in `website/` via an npm `overrides`
  block. The dependency arrives transitively through
  `@docusaurus/core@3.10.1` → `@docusaurus/bundler` →
  `copy-webpack-plugin@11` / `css-minimizer-webpack-plugin@5`, which still
  pin `^6`; Docusaurus 3.10.1 is the latest release so Dependabot's
  security update could not resolve it on its own.

## [0.1.0] - YYYY-MM-DD

### Added
- NuGet V3 proxy MVP: service index, flatcontainer listing,
  registration5-gz-semver2, search, and package download — backed by
  WireMock.Net integration tests.
- Filtering rules: `minAgeDays`, `allowDeny` (glob, case-insensitive, deny-wins).
- YAML configuration with hot-reload (`config.yml`, optional `config.{Env}.yml`,
  `config.secret.yml`).
- Serilog → JSON to stdout; prometheus-net `/metrics`; `/healthz` and `/readyz`
  liveness/readiness endpoints.
- `Microsoft.Extensions.Caching.Hybrid` for L1/L2 caching.
- Cake build (`build.cake`) shared by local developer flow and the Dockerfile.

[Unreleased]: https://github.com/Markeli/Heimdall/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/Markeli/Heimdall/releases/tag/v0.1.0
