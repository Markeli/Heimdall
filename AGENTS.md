# Agents guide

This file binds AI agents (Claude Code, Codex, GitHub Copilot, Cursor, etc.) to
the same rules as human contributors. See [`CONTRIBUTING.md`](CONTRIBUTING.md)
for prose and rationale. If anything here conflicts with `CONTRIBUTING.md`,
`CONTRIBUTING.md` wins.

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

## When in doubt

- Read `CONTRIBUTING.md` first.
- If the requirement is ambiguous, ask in the PR or issue thread before
  changing behaviour.
- Prefer the smallest change that fixes the reported problem.
