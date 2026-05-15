# PyPI — concept study

Status: draft. Reference: [`ecosystems-overview.md`](ecosystems-overview.md).

## 1. Goal and non-goals

**Goal.** Make `pip install`, `uv pip install`, `uv lock`, `poetry
install`, and `pdm install` work against Heimdall as a sole index,
with `minAgeDays` and `allowDeny` applied to every Simple-API
listing and enforced at every file download.

**Non-goals for v1.**

- Upload via `twine` / Warehouse `/legacy/` endpoint.
- Account / user / token APIs.
- RSS feeds, BigQuery exports, attestations (PEP 740 / provenance
  verification beyond pass-through).
- Mirror protocols (`bandersnatch`).
- A custom search endpoint. PyPI's XML-RPC `search` was permanently
  deprecated in September 2024 and no public search API replaced it.
  Heimdall does not invent one.
- BOM-style transitive policy beyond what `allowDeny` over package
  names provides.

## 2. Protocol surface

PyPI exposes two distinct protocols a client may use; Heimdall
supports both because clients pick at runtime.

### Simple Repository API (PEP 503/691/700/658/592)

The current spec lives at
<https://packaging.python.org/en/latest/specifications/simple-repository-api/>,
version 1.4 at the time of writing. The two endpoints are:

| Endpoint | Method | Returns |
|---|---|---|
| `GET /simple/` | GET | root project listing |
| `GET /simple/<normalized-name>/` | GET | per-project file listing |

Both endpoints content-negotiate between
`application/vnd.pypi.simple.v1+json` (PEP 691),
`application/vnd.pypi.simple.v1+html`, and legacy `text/html`.

Per-file attributes Heimdall must understand:

- `filename`, `url`, `hashes` (SHA-256 primary).
- `requires-python` (PEP 503).
- `yanked` (PEP 592) — bool or reason string.
- `core-metadata` (PEP 658 / 714 attribute, also known as
  `data-dist-info-metadata` in the HTML form) — flags that
  `<file-url>.metadata` sibling is available with the wheel's
  `METADATA` block.
- `upload-time` (PEP 700) — ISO 8601, present in JSON Simple v1.1+ on
  PyPI. **This is the field that powers `minAgeDays`.**
- `size` (PEP 700) — mandatory at api-version ≥ 1.1.

### Legacy JSON API

| Endpoint | Returns |
|---|---|
| `GET /pypi/<name>/json` | full per-project metadata |
| `GET /pypi/<name>/<version>/json` | per-version metadata |

Treated by PyPI's docs as supported-but-deprecating: `releases`,
`downloads`, `has_sig`, `bugtrack_url` are slated for removal.
`urls[].upload_time_iso_8601` is still authoritative. Heimdall
proxies this for two reasons: (a) older Poetry hits it directly for
PyPI, (b) it is the timestamp fallback for non-PyPI upstreams that do
not implement PEP 700.

### File hosts

Files do not live under `pypi.org/simple/` — the Simple listing
points at `files.pythonhosted.org/...`. Heimdall must either:

- proxy file URLs through itself (rewriting the URLs in the Simple
  response and serving the file via a tarball-equivalent controller),
  or
- let clients hit `files.pythonhosted.org` directly.

MVP chooses to proxy. The download gate cannot fire otherwise.

## 3. `minAgeDays`: where the timestamp lives

**Preferred source:** PEP 700 `upload-time` in JSON Simple. One
filtering pass over the response is enough.

**Fallback source (upstream does not advertise PEP 700):** `GET
/pypi/<name>/json`, which returns `urls[].upload_time_iso_8601` for
every release file in one document. Heimdall caches the mapping
`(name, filename) → timestamp` for the cache TTL.

**Worst-case source:** `GET /pypi/<name>/<version>/json` per version.
Only used if both above fail. Per-version round-trip; expensive.

Name normalization (PEP 503): lowercase, replace runs of `[-_.]+`
with a single `-`. Heimdall must normalize on ingress before
allow/deny glob matching, otherwise `Django` and `django` and
`Django.Pinax` all need their own glob entries.

## 4. Filter integration points

- **`VersionListFilter`** applied to the file list of `GET
  /simple/<name>/`. The transformer drops files whose `upload-time`
  fails `minAgeDays` or whose project name fails `allowDeny`. It
  preserves `hashes`, `requires-python`, `yanked`, `core-metadata`,
  `size`. It rewrites file URLs to point at Heimdall.
- **`SingleVersionGate`** applied at file download. A lockfile
  (`poetry.lock`, `uv.lock`, `requirements.txt` with
  `--require-hashes`) bypasses the Simple listing and goes straight
  to the file URL. The gate is the load-bearing piece.

`PEP 658` `.metadata` sidecar requests are proxied unchanged — the
file they describe has already been gated by the time the client
asks for `.metadata`.

## 5. Status codes when filtered

- Filtered file requested directly → `403 ProblemDetails` on the
  file URL. pip surfaces the response body in `--verbose`; the
  default error is a generic download failure, but the URL itself
  encodes the rule's `feed` for log triage.
- Hidden from Simple listing → just absent. PEP 503/691 has no
  "removed" signal for index entries.
- Yanked files are *not* hidden by Heimdall — yanked is an
  upstream-level signal and is needed by lockfile-pinned exact
  matches per PEP 592. Heimdall passes `yanked` through. The filter
  pipeline still applies to yanked files: a yanked version younger
  than `minAgeDays` is rejected, a yanked version of a denied
  project is rejected. "Yanked" is not itself a deny signal — it is
  metadata the client uses to decide whether to install.

## 6. Client behaviour on restore

- **pip (≥24).** JSON Simple by `Accept` preference, falls back to
  HTML. Uses PEP 658 sidecars to read deps without downloading
  wheels. Classic resolver still backtracks; `--require-hashes`
  in `requirements.txt` forces hash mode for the whole file.
- **uv.** JSON Simple by default. Caches API responses for 10 minutes
  (respects `Cache-Control: max-age=600` from PyPI) and files
  indefinitely (`max-age=365000000, immutable`). Persists
  `upload-time` into `uv.lock`. PubGrub resolver. **Persistent
  on-disk cache** means a previously cached wheel survives policy
  tightening — see bypass section.
- **Poetry (≥1.2).** Historically hit `/pypi/<pkg>/json` for PyPI
  specifically. PEP 658 since 1.2.0. Per-source priorities (`primary`,
  `supplemental`, `explicit`).
- **PDM.** Resolvelib-based; PEP 658 supported.

**Multiple-index behaviour matters.** pip's `--index-url` +
`--extra-index-url` are searched with **no priority** — pip picks the
"best" version from the union. uv reverses this (extra wins by
declaration order). Poetry uses explicit labels. The MVP must
document the recommended client config: Heimdall as `--index-url`,
no `--extra-index-url` to upstream.

## 7. Bypass surface

- **`pip --extra-index-url https://pypi.org/simple/`.** A developer
  keeps upstream alongside the proxy, defeats `allowDeny`. Mitigation:
  network policy + documentation. There is no protocol-level
  prevention.
- **uv's persistent cache.** Once a wheel is cached locally, a
  developer can install offline even after Heimdall starts rejecting
  it. Tighten via `uv cache prune` / `--refresh`; document the gap.
- **`requirements.txt` with absolute upstream URLs.** Possible but
  not idiomatic. Egress policy is the mitigation.
- **Poetry's PyPI special-case** in older Poetry versions hits
  `/pypi/<pkg>/json` against `pypi.org` directly. Configuration via
  `[[tool.poetry.source]] priority = "primary"` redirects it; older
  installations need a one-time settings change.

## 8. Out of MVP

- Upload (`POST /legacy/`).
- `/pypi/<name>/<version>/json` for non-fallback use cases.
- `PEP 740` provenance verification beyond byte pass-through.
- Custom search.

## 9. Open questions

- **Wheel-vs-sdist policy.** Should `allowDeny` operate on the
  filename (which distinguishes them) or only the project name? MVP:
  project name only; deny-by-filename is a v2 ask.
- **`extra-index-url` warning at proxy ingress.** Can the proxy
  detect when a client is also configured with upstream? Probably
  not from server side — the second index is invisible to us.
  Document only.
- **PEP 740 attestations.** Proxy through unchanged; do not become a
  verification authority.
- **Per-file vs per-version gate.** A version may have a wheel and an
  sdist; rules apply to the version. Confirm the gate evaluates the
  rule once per version, not per file.
- **HTML fallback parity.** Both HTML and JSON Simple representations
  must be filter-consistent; share the projection layer.
- **File-host rewriting.** Rewrite all `files.pythonhosted.org` URLs
  to Heimdall, or only the ones that pass the filter? Decision:
  rewrite all surviving files; do not surface upstream hosts to the
  client.

## 10. Implementation sketch

New project: `src/Heimdall.Ecosystems.Python/`.

- `IPypiUpstreamClient` — Polly-resilient HttpClient for Simple
  (JSON + HTML), JSON API, and file download.
- `PypiSimpleService` — orchestration: fetch upstream, enrich with
  `upload-time` if absent (call JSON API fallback), filter, rewrite
  URLs, cache.
- `PypiSimpleTransformer` — JSON and HTML projection of the file
  list. Drops files that fail the rule pipeline; preserves
  `hashes`, `requires-python`, `yanked`, `core-metadata`, `size`.
- `PypiUrlRewriter` — file URL rewriting.
- `PypiFileProxyService` — single-version gate, stream pass-through.
- `PypiNameNormalizer` — PEP 503 normalization at ingress.

Controllers:

- `PypiSimpleController` — `GET /python/<feed>/simple/` and
  `GET /python/<feed>/simple/<name>/` (both Accept variants).
- `PypiFileController` — `GET /python/<feed>/files/<...>` for wheels,
  sdists, and `.metadata` sidecars.
- `PypiJsonController` — `GET /python/<feed>/pypi/<name>/json` and
  per-version variant. Pass-through-with-filter; needed for older
  Poetry compatibility and for our own fallback.

Config:

```yaml
heimdall:
  ecosystems:
    python:
      feeds:
        - name: strict
          upstream: "https://pypi.org/simple/"
          jsonApiUpstream: "https://pypi.org/pypi/"   # for fallback
          fileHost: "https://files.pythonhosted.org/" # for streaming
          cacheTtl: "00:10:00"
          rules:
            - type: minAgeDays
              days: "14"
            - type: allowDeny
              patterns: "django;requests;!internal-*"
```

## 11. Acceptance criteria

- `pip install <pkg>` works against `--index-url http://heimdall/python/strict/simple/`.
- `uv lock` + `uv sync` works against the same URL.
- `poetry install` with `[[tool.poetry.source]] priority = "primary"`
  pointing at Heimdall works.
- Versions younger than `minAgeDays` are absent from JSON Simple and
  HTML Simple.
- Denied projects return `403 ProblemDetails` on file download.
- A pinned `--require-hashes` entry for a filtered version produces
  `403 ProblemDetails`.
- PEP 658 `.metadata` sidecars are served for surviving files.
- File bytes are byte-identical to upstream (sha256 from upstream
  hash fragment matches).
- Tests cover both Accept variants, the JSON API fallback path,
  yanked-files-passed-through, and name normalization.
