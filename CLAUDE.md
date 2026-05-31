# Claude Code guide

Claude Code follows the same rules as every other AI agent working on this
repo. The binding contract is in [`AGENTS.md`](AGENTS.md); read it first —
including its **Startup Workflow**, which routes you through `progress.md`,
`session-handoff.md`, and `feature_list.json`, and its single verify gate
`./init.sh`.

## Claude Code-specific notes

- **Docs source lives in [`docs/`](docs/)**, not in `website/docs/`. The
  Docusaurus project under [`website/`](website/) reads from `../docs` via
  `path: '../docs'` in `docusaurus.config.js`.
- **Do not touch `.github/workflows/docs.yml`** unless the task is
  explicitly about CI. If you must, prefix the PR title with `[ci]` per
  `AGENTS.md`.
- **Run `npm run build` in `website/` after meaningful doc edits** —
  `onBrokenLinks: "throw"` catches stale internal references at build
  time. The exact command CI runs is in `.github/workflows/docs.yml`.
- **Style on the C# side**: tabs, max 130 chars, file-scoped namespaces,
  nullable reference types on, warnings = errors. See
  [`.editorconfig`](.editorconfig).
- **Style on the docs side**: tabs in YAML / JS / JSON to match the rest
  of the repo; markdown is freeform.
- **Always run `./init.sh` locally before pushing** — it wraps
  `dotnet cake --target=Test --configuration=Release` (the exact pipeline
  `CONTRIBUTING.md §3` requires), and CI will fail the PR otherwise.

If `AGENTS.md` and this file disagree, `AGENTS.md` wins.
