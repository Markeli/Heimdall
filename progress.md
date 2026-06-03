# Progress

A rolling log so any agent (or human) can resume without replaying chat
history. Newest entry on top. `feature_list.json` is the map; this is the
journal. GitHub issues + `CHANGELOG.md` remain the source of truth for shipped
work — this file just records *in-flight* state and the next concrete step.

> **Last Updated:** 2026-05-31
> **Current Objective:** `docs-improvement` (issue #10) — docs reworked in the
> working tree on branch `docs/improve-docs-issue-10`. Implementation complete
> and verified locally; not yet committed/PR'd.
> **Recommended Next Step:** Commit the docs changes and open a PR with
> `Closes #10` (awaiting maintainer go-ahead to commit/push). Then flip
> `docs-improvement` → `done` in `feature_list.json` with the evidence below.

## How to use this log

- **Current State** — what is true right now (active feature, branch, build
  status).
- **What changed** — what this session actually did.
- **Next** — the single next action, concrete enough to start cold.
- **Verification Evidence** — the exact command run and its observed result
  (e.g. `dotnet cake --target=Test` → green, 51 tests). No green run, no
  "done".

---

## 2026-06-01 — Docs theme: orange + shield logo (issue #10)

- **Current State:** Same branch `docs/improve-docs-issue-10`. Changes in the
  working tree, **not committed**.
- **What changed:**
  - `website/src/css/custom.css`: brand palette recoloured blue → burnt orange
    (`#d9480f` base, full light/dark ramps); `--ifm-link-color: #b93d08` in
    light theme for WCAG AA link-text contrast on white.
  - `website/static/img/logo.svg` + `favicon.svg`: rounded tile → heater-shield
    with white "H" monogram, orange fill (shared shield path).
  - `CHANGELOG.md` `[Unreleased]` Changed entry added.
- **Next:** Same as below — commit + PR `Closes #10` (awaiting go-ahead).
- **Verification Evidence:**
  - `cd website && npm run build` → GREEN; orange compiled into the CSS bundle
    (`d9480f`/`b93d08`/`ff8a4c`), shield path present in `build/img/logo.svg`
    and `build/img/favicon.svg`, favicon wired as `/Heimdall/img/favicon.svg`.
  - `grep -rni "2b5fb3" website/src website/static` → empty (no blue left).
  - `./init.sh` → GREEN: 53 unit + 9 integration tests (no code changed).

---

## 2026-05-31 — Docs improvement (issue #10)

- **Current State:** Branch `docs/improve-docs-issue-10` off `origin/main`.
  `docs-improvement` is `in_progress` + `activeFeatureId`. Changes are in the
  working tree, **not committed**.
- **What changed:**
  - Repositioned Heimdall as a *minimalist dependency firewall* (not a
    NuGet-specific proxy): `README.md`, `docs/intro.md`,
    `website/docusaurus.config.js` tagline.
  - README slimmed to a pointer — dropped Stack/Layout/Endpoints/Tests/local
    run, removed a leaked absolute path; added a **Supported registries** table
    with local SVG logos (`website/static/img/registries/*.svg`).
  - `docs/intro.md`: universal "What Heimdall does" + Supported registries
    table (uses `useBaseUrl` so logos resolve under the `/Heimdall/` baseUrl).
  - `docs/configuration/overview.md`: trimmed hot-reload + validation to brief
    mentions. `docs/configuration/logging.md`: dropped the Request logging
    section.
  - `docs/architecture/overview.md`: rewritten around the registry-agnostic
    processing flow + core-vs-adapter split.
  - `docs/development/testing.md`: added a Smoke tests section.
  - `AGENTS.md`: noted release-only smoke suite. `CHANGELOG.md`: `[Unreleased]`
    Changed entry (Closes #10).
- **Next:** Commit + open PR `Closes #10` (awaiting go-ahead), then flip
  `docs-improvement` → `done`.
- **Verification Evidence:**
  - `./init.sh` → GREEN: 53 unit + 9 integration tests passed (no code changed).
  - `cd website && npm run build` → GREEN (`onBrokenLinks: "throw"`); SVG assets
    present in `build/img/registries/` and referenced as
    `/Heimdall/img/registries/*.svg` in the generated intro page.
  - `grep -rn "/Users/" README.md docs` → empty (no leaked paths).

---

## 2026-05-31 — Harness bootstrap

- **Current State:** main is clean. NuGet MVP shipped. No feature `in_progress`.
- **What changed:** Added agent harness — `feature_list.json`, `progress.md`,
  `session-handoff.md`, `init.sh`, and a startup/scope/done/end-of-session
  block in `AGENTS.md`.
- **Next:** Maintainer to choose the next feature to activate.
- **Verification Evidence:** Harness files added only; no source/test changes
  this entry. Run `./init.sh` to confirm the build remains green before the
  first feature change.
