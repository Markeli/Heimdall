# Changelog

All notable changes to Heimdall are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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
