# Agents guide

This file binds AI agents (Claude Code, Codex, GitHub Copilot, Cursor, etc.) to
the same rules as human contributors. See [`CONTRIBUTING.md`](CONTRIBUTING.md)
for prose and rationale. If anything here conflicts with `CONTRIBUTING.md`,
`CONTRIBUTING.md` wins.

## Startup Workflow

Before writing code, in this order:

1. Read this file and `CONTRIBUTING.md`.
2. Read `progress.md` (newest entry) and `session-handoff.md` to learn the
   current objective, active feature, and recommended next step.
3. Open `feature_list.json` — the at-a-glance feature map. GitHub issues stay
   the source of truth; this file routes you to the right one.
4. Pick **one** feature, set its `status` to `in_progress` and set
   `activeFeatureId`. Branch off the latest `origin/main`.
5. Run `./init.sh` once to confirm a green baseline before you change anything.

State lives in `feature_list.json` + `progress.md` (+ `session-handoff.md` for
mid-task resumes), not in chat history. Update them as you go.

## Hard rules

- MUST work on a short-lived feature branch off the latest `origin/main`. MUST
  NOT commit directly to `main`.
- MUST NOT force-push, amend pushed commits, or pass `--no-verify`.
- MUST update `CHANGELOG.md` under `[Unreleased]` for any user-visible change.
  If the change truly has no user-visible effect, request the `skip-changelog`
  label and justify it in the PR description.
- MUST run `dotnet cake --target=Test --configuration=Release` locally and
  observe a green run before opening a PR.
- MUST keep `.editorconfig` style: tabs, max 130 chars, file-scoped namespaces,
  nullable reference types on, warnings = errors.
- MUST link the issue you close with `Closes #N` in the PR body.
- MUST NOT introduce new dependencies without justifying them in the PR body.
- MUST NOT modify `.github/workflows/*` without prefixing the PR title with
  `[ci]`.
- MUST NOT touch `Directory.Build.props`, `Directory.Packages.props`,
  `global.json`, or `build.cake` unless the task explicitly requires it; flag
  such changes in the PR body.
- MUST NOT delete and re-push a release tag. If a release fails, open a fix PR
  and tag the next patch version.
- MUST keep documentation source in [`docs/`](docs/), not in `website/docs/`.
  The Docusaurus site under [`website/`](website/) reads from `../docs`.
  MUST NOT mass-duplicate `README.md` content into `docs/` — the site
  expands on README, it does not mirror it.

## Workflow

1. Sync `main`, branch off it: `git fetch origin main && git checkout -b
   <type>/<short-slug> origin/main`.
2. Implement the change with a test (`tests/Heimdall.UnitTests` or
   `tests/Heimdall.IntegrationTests`).
3. Update `CHANGELOG.md` under `[Unreleased]`.
4. Run `dotnet cake --target=Test --configuration=Release` locally.
5. Open a PR. Fill in
   [`.github/pull_request_template.md`](.github/pull_request_template.md);
   link the issue.
6. On red CI, fix the root cause. **Never** disable, skip, or weaken a check.

## Verification Commands & Evidence

- **One gate:** `./init.sh` — restores tools, then `dotnet cake --target=Test
  --configuration=Release` (restore + build + test), the exact CI pipeline.
- Docs changes also require `cd website && npm run build` (`onBrokenLinks:
  "throw"`).
- The `release` pipeline additionally runs `tests/Heimdall.SmokeTests` against a
  live container talking to `api.nuget.org`; that suite is kept out of
  `Heimdall.sln`, so the local `./init.sh` gate does not run it.
- **Verification Evidence is mandatory:** record the command and its output
  (e.g. `./init.sh` → green, N tests) in `progress.md` before claiming a
  feature done. No green run, no "done". A red run means fix the root cause —
  never disable, skip, or weaken a check.

## Scope

- **One feature at a time.** Exactly one entry in `feature_list.json` may be
  `in_progress`; it must match `activeFeatureId`. Finish or explicitly park it
  before starting another.
- **Stay in scope.** Make the smallest change that satisfies the active
  feature. Respect the MVP "Out of scope" list in `README.md` (L2 Redis,
  npm/Maven/PyPI/Go ecosystems until their issue is the active feature, CVE
  rules, auth, multi-instance). Don't refactor unrelated code or close issues
  you weren't asked to.
- Track each feature's `dependencies` and `status` in `feature_list.json`;
  don't start a feature whose dependencies aren't `done`.

## Definition of Done

A feature is done only when **all** of these hold:

1. Behaviour change is covered by a test (`tests/Heimdall.UnitTests` or
   `tests/Heimdall.IntegrationTests`), failing-test-first for bugfixes.
2. `./init.sh` is green and the evidence is recorded in `progress.md`.
3. `CHANGELOG.md` `[Unreleased]` updated (or `skip-changelog` justified).
4. Docs updated under `docs/` if behaviour/config changed; `website` build
   passes.
5. PR links the issue (`Closes #N`) and fills the PR template.
6. `feature_list.json` status flipped to `done` with an `evidence` note.

## End of Session

Before ending a session with work in flight:

1. Update `progress.md` — append a dated entry: Current State / What changed /
   Next / Verification Evidence.
2. Update `session-handoff.md` — Blockers, Files, Next Session, and the
   restart markers (Last Updated / Current Objective / Recommended Next Step).
3. Leave the tree restartable: committed or clearly noted, with `./init.sh`
   either green or its failure captured as the next step.

## When in doubt

- Read `CONTRIBUTING.md` first.
- If the requirement is ambiguous, ask in the PR or issue thread before
  changing behaviour.
- Prefer the smallest change that fixes the reported problem.
