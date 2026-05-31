# Progress

A rolling log so any agent (or human) can resume without replaying chat
history. Newest entry on top. `feature_list.json` is the map; this is the
journal. GitHub issues + `CHANGELOG.md` remain the source of truth for shipped
work — this file just records *in-flight* state and the next concrete step.

> **Last Updated:** 2026-05-31
> **Current Objective:** No feature active. NuGet MVP (proxy + `minAgeDays`
> + `allowDeny` + caching + health/metrics + smoke + release pipeline) is
> shipped. Next planned work is one of the open issues — see below.
> **Recommended Next Step:** Pick a single planned feature from
> `feature_list.json` (e.g. #19 "Filter latest versions"), set it
> `in_progress` + `activeFeatureId`, branch off `origin/main`, and start with
> a failing test per `CONTRIBUTING.md §7`.

## How to use this log

- **Current State** — what is true right now (active feature, branch, build
  status).
- **What changed** — what this session actually did.
- **Next** — the single next action, concrete enough to start cold.
- **Verification Evidence** — the exact command run and its observed result
  (e.g. `dotnet cake --target=Test` → green, 51 tests). No green run, no
  "done".

---

## 2026-05-31 — Filter latest versions (#19)

- **Current State:** Feature `filter-latest-versions` (#19) `in_progress` on
  branch `feature/#8-filter-latest-nuget` (branch name keeps the original #8
  slug; PR will use `Closes #19`). Implemented and verified locally; **not yet
  committed/pushed** (awaiting maintainer go-ahead).
- **What changed:**
  - New `src/Heimdall.Core/Packages/VersionOrdering.cs`: semver `Ascending`
    comparer (`SemVersion.SortOrderComparer`), `OrderAscending`, and
    `SelectLatest` (highest stable, else highest prerelease).
  - `NuGetV3MetadataTransformer`: flat-container list and registration now order
    by semver (was ordinal); `BuildVersionsListJson`/`RewriteRegistration`
    return `string?` → `null` when all versions filtered (controllers map to
    404); `RewriteSearch` takes optional registration-enriched metadata, filters
    on it, and recomputes the primary via `SelectLatest`.
  - `NuGetV3MetadataService.SearchJsonAsync`: enriches each hit with publish
    dates from the cached registration via `Parallel.ForEachAsync` bounded by
    the new `heimdall.server.search.maxConcurrentRegistrationFetches` (default
    8, `>=1`; in `HeimdallOptions`, validator, `config.yml`). Logs a warning on
    per-hit enrichment failure (falls back to date-less metadata).
  - Tests: `VersionOrderingTests`, transformer semver/404/search tests, a
    validator test, and E2E `query` + 404 tests (new upstream search/`all.new`
    WireMock stubs).
  - Docs: `docs/configuration/server.md`, `docs/api/nuget-v3.md`;
    `CHANGELOG.md` Added + Changed entries.
- **Next:** Maintainer to approve commit + PR (`Closes #19`); on merge flip
  `feature_list.json` `filter-latest-versions` → `done` with evidence.
- **Verification Evidence:** `./init.sh` → GREEN: build 0 warnings/0 errors,
  Heimdall.UnitTests 65 passed, Heimdall.IntegrationTests 12 passed (was 53/9).
  `cd website && npm run build` → SUCCESS (onBrokenLinks: throw, no broken
  links).

## 2026-05-31 — Harness bootstrap

- **Current State:** main is clean. NuGet MVP shipped. No feature `in_progress`.
- **What changed:** Added agent harness — `feature_list.json`, `progress.md`,
  `session-handoff.md`, `init.sh`, and a startup/scope/done/end-of-session
  block in `AGENTS.md`.
- **Next:** Maintainer to choose the next feature to activate.
- **Verification Evidence:** Harness files added only; no source/test changes
  this entry. Run `./init.sh` to confirm the build remains green before the
  first feature change.
