# Contributing to Heimdall

Thanks for working on Heimdall. This guide is the contract for **both human
contributors and AI agents**. The terse machine-readable mirror lives in
[`AGENTS.md`](AGENTS.md) — if the two ever disagree, **this file wins**.

## 1. Branching

- `main` is the only long-lived branch. It is protected and only receives
  changes through pull requests.
- Work happens on short-lived feature branches off the latest `origin/main`
  (e.g. `feature/issue-3-cicd`, `fix/url-rewriter-edge-case`).
- **Never** force-push to `main`. **Never** push directly to `main`.

## 2. Commits

- Subject line ≤72 chars, imperative present tense
  ("Add release workflow", not "Added release workflow").
- Empty line then a body when the *why* is non-obvious.
- Reference the issue you are closing in the PR body (`Closes #N`), not the
  subject line.

## 3. Pull requests

Before opening a PR:

```sh
dotnet tool restore
dotnet cake --target=Test --configuration=Release
```

Every PR must:

- Link the issue it closes (`Closes #N`) in the PR body.
- Add an entry under `[Unreleased]` in [`CHANGELOG.md`](CHANGELOG.md). If the
  change really has no user-visible effect (docs typo, internal refactor with
  no behaviour change), apply the `skip-changelog` label and explain why in the
  PR description.
- Pass all required CI checks (`ci/build`, `ci/changelog-guard`).
- Have at least one reviewer approval before merge.
- **Not** be merged with `--no-verify`, `--force`, or by disabling required
  checks.

The PR template under [`.github/pull_request_template.md`](.github/pull_request_template.md)
captures these as a checklist.

If you change anything under `.github/workflows/`, mark the PR title with `[ci]`
so reviewers know to scrutinise it.

## 4. Local build

```sh
dotnet tool restore
dotnet cake --target=Test      # restore + build + test
dotnet cake --target=Publish   # produces ./artifacts/publish/
```

Container build (matches the release pipeline exactly):

```sh
docker build -f src/Heimdall.Api/Dockerfile -t heimdall:dev .
```

### Working on docs

The documentation site is a Docusaurus 3 project under [`website/`](website/);
source markdown lives in [`docs/`](docs/). Local loop:

```sh
cd website
npm ci
npm start          # local preview at http://localhost:3000/Heimdall/
npm run build      # what CI runs (onBrokenLinks: "throw")
```

Node 20 LTS — see [`website/.nvmrc`](website/.nvmrc). Prefix doc-only PR
titles with `[docs]` so reviewers know not to expect a behaviour change
(informational, not enforced).

## 5. Releasing

Heimdall uses [Semantic Versioning](https://semver.org/) with
[MinVer](https://github.com/adamralph/minver) driving assembly versions from
`v*.*.*` git tags.

To cut a release:

1. **Release PR** — promote the `[Unreleased]` section in `CHANGELOG.md` to
   `[X.Y.Z] - YYYY-MM-DD` and update the link references at the bottom of the
   file. Open as a PR, get it reviewed, merge.
2. **Tag the merge commit on `main`**:

   ```sh
   git checkout main
   git pull --ff-only
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```

3. The `release` workflow will validate the tag, re-run tests, build and push
   `ghcr.io/markeli/heimdall:{X.Y.Z,latest}` to GHCR, and create a GitHub
   Release whose body is the matching `[X.Y.Z]` section of `CHANGELOG.md`.

If any step of the release workflow fails, **do not delete and re-push the
tag**. Open a fix PR, merge it, and tag `vX.Y.(Z+1)`.

## 6. Code style

`.editorconfig` is authoritative. The non-obvious bits:

- Tabs for indentation, 4-wide.
- Maximum line length 130 characters (C# files).
- File-scoped namespaces; `using` outside the namespace.
- Nullable reference types on; warnings = errors; `latest-recommended` analysers.
- Prefer `record` for immutable data and `async` APIs with `CancellationToken`.

Don't add new dependencies without justifying them in the PR body.

## 7. Tests

Every behaviour change needs a test:

- `tests/Heimdall.UnitTests` — pure logic (rules, filters, transformers).
- `tests/Heimdall.IntegrationTests` — `WebApplicationFactory` + `WireMock.Net`
  for end-to-end controller flows.

When fixing a bug, add the failing test first, then the fix.

## 8. Repository administration (one-time bootstrap)

These steps belong to the maintainer, not contributors, but are recorded here
so they aren't forgotten:

- **Settings → Branches → `main`**: require pull requests, require status
  checks `ci/build` and `ci/changelog-guard`, disallow force-pushes and direct
  pushes.
- **Settings → Actions → General**: "Workflow permissions" = *Read and write*.
- **Settings → Pages → Source**: *"GitHub Actions"*. Required for the
  `docs` workflow to deploy `https://markeli.github.io/Heimdall/`. Without
  this the deploy job's OIDC handshake fails and the site is never
  published.
- **After the first release**: link the GHCR package
  (`ghcr.io/markeli/heimdall`) to this repository and set its visibility to
  public if external pulls are intended; otherwise leave it private.

## 9. Questions

Open a GitHub Discussion or, for actionable problems, file an issue using one
of the templates under `.github/ISSUE_TEMPLATE/`.
