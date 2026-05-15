# Go modules — concept study

Status: draft. Reference: [`ecosystems-overview.md`](ecosystems-overview.md).

## 1. Goal and non-goals

**Goal.** Make `go build`, `go mod download`, `go mod tidy`, and `go
get` work against Heimdall as the configured `GOPROXY`, with
`minAgeDays` and `allowDeny` applied to every module listing and
enforced at every `.info`, `.mod`, `.zip` request.

**Non-goals for v1.**

- Proxying `sum.golang.org` (the checksum database). Documented MVP
  non-goal; clients keep `GOSUMDB=sum.golang.org` direct. Revisited
  when air-gapped deployments are in scope.
- Search (`pkg.go.dev`, `index.golang.org`) — not part of the
  proxy protocol.
- `go install` package-level URL semantics beyond what GOPROXY
  delivers.
- `replace` / `vendor` enforcement (both are entirely client-side and
  invisible to the proxy).

## 2. Protocol surface

Five endpoints per module, plus the `@latest` shortcut.

| Endpoint | Method | Body | Purpose |
|---|---|---|---|
| `/<module>/@v/list` | GET | text, one semver per line | enumerate tagged versions |
| `/<module>/@latest` | GET | JSON `{"Version","Time"}` | latest meaningful version |
| `/<module>/@v/<version>.info` | GET | JSON `{"Version","Time"}` | per-version metadata |
| `/<module>/@v/<version>.mod` | GET | raw `go.mod` text | for MVS |
| `/<module>/@v/<version>.zip` | GET | zip of source tree | source payload |

**Case-sensitivity escape.** Uppercase letters in module paths and
versions are replaced with `!` + lowercase: `Azure/azure-sdk-for-go`
→ `!azure/azure-sdk-for-go`. The router must implement the inverse
exactly; otherwise `allowDeny` globs on canonical names silently
miss escaped requests.

**`GOPROXY` syntax.** `https://heimdall,direct` (comma) falls
through on `404`/`410` only; `https://heimdall|direct` (pipe) falls
through on any error. The protocol's fallback semantics drive the
status-code choice in §5.

## 3. `minAgeDays`: where the timestamp lives

`.info.Time` is the per-version commit time (UTC). The `list`
endpoint returns version strings only, no timestamps.

Two strategies for filtering a `list` response by age:

- **Pre-warm.** On first request for a module, fetch `list`, fan out
  `.info` per version, cache `(module, version) → Time` indefinitely
  (the data is immutable). Subsequent `list` requests are served
  from cache.
- **Lazy.** Serve `list` verbatim, gate at `.info`/`.mod`/`.zip`.
  Cheaper to operate; surfaces filtered versions to the resolver
  before the gate rejects them. The user sees a `403 ProblemDetails`
  with the rule name; the build fails. UX is slightly worse but the
  amplification is zero on cold cache.

MVP picks **lazy with bounded pre-warm**: serve `list` verbatim
(filtered only for cached versions and `allowDeny`), let the gate be
authoritative. A background warmer can opportunistically fill the
timestamp cache.

**Pseudo-versions.** Format `vX.Y.Z-yyyymmddhhmmss-abcdefabcdef`
embeds the commit timestamp in the string itself. `minAgeDays` can
parse it without a `.info` fetch. Pseudo-versions never appear in
`/list`; they arrive only as direct `.info`/`.mod`/`.zip` requests.

## 4. Filter integration points

- **`VersionListFilter`** applied to `/list` output for cached
  timestamps and for `allowDeny` on the module path.
- **`SingleVersionGate`** applied at every `.info`, `.mod`, `.zip`
  request. This is the load-bearing piece — clients with `go.sum`
  pin versions go straight here, skipping `list` entirely.
- **`@latest`** must return a *surviving* version. Resolving it
  through the filter is mandatory; otherwise `@latest` points at a
  version that subsequently 403s.

The gate at `.info` is critical because it is the cheapest
discovery for a pinned dependency: the client tries `.info` first,
and a rejection there avoids the `.mod` and `.zip` round-trips.

## 5. Status codes when filtered

**`403 ProblemDetails`. Not `404`. Not `410`.**

This is unique to Go. `404` and `410` cause the `go` client to fall
through to the next `GOPROXY` entry. With the default `GOPROXY=...,direct`
in most installs, fall-through means the client connects to the VCS
directly and bypasses Heimdall entirely. Any other status (`403`,
`500`, `401`, ...) stops the chain and surfaces an error to the user.

`403 ProblemDetails` is the same body shape as the NuGet MVP. The
`go` command prints the proxy's response body for non-2xx, so the
rule name reaches the build log.

Operator documentation must call out: for strict enforcement,
`GOPROXY=https://heimdall` (no `direct` fallback) or
`GOPROXY=https://heimdall,off`. With `direct` in the chain, the
filter is advisory by design — and that is the protocol, not a
Heimdall bug.

## 6. Client behaviour on restore

Sequence for a known-version dependency:

1. `.info` of `module@version`. Confirms existence; `Time` powers
   pseudo-version ordering and MVS tie-breaking.
2. `.mod` of `module@version`. Needed to walk the transitive graph.
3. `.zip` of `module@version`. Only if source is required.

For an unpinned dependency:

1. `/@v/list`.
2. `/@latest` if `list` is empty or insufficient.
3. `.info` for candidates.
4. `.mod`, `.zip`.

**Path-prefix walk.** For `golang.org/x/net/html` the client probes
`golang.org/x/net/html/@v/list`, `golang.org/x/net/@v/list`,
`golang.org/x/@v/list`, `golang.org/@v/list` in **parallel** and
picks the longest 200. The proxy must answer each prefix correctly
(typically `404` if no module exists at that path; the prefix walk
is normal and 404 here is expected, not a filter rejection).

**Critical asymmetry.** A client with `module v1.2.3` in `go.sum`
**does not call `/list`**. It goes straight to `.info`, `.mod`,
`.zip`. Filtering `list` is therefore decorative for any pinned
dependency.

## 7. Checksum database (sumdb)

`sum.golang.org` is a transparency log of `module@version → h1:`
hashes. The client consults sumdb on **first encounter** of a
(module, version) — when `go.sum` does not yet contain a line. Once
in `go.sum`, the local hash is authoritative.

The protocol allows a `GOPROXY` to also proxy sumdb:

- `/sumdb/<sumdb-name>/supported` — 200 = "I proxy"; 404/410 = "fall
  back to direct".
- `/sumdb/<sumdb-name>/lookup/<module>@<version>`
- `/sumdb/<sumdb-name>/latest`
- `/sumdb/<sumdb-name>/tile/<H>/<L>/<N>[.p/<W>]`

**MVP non-goal.** Heimdall returns `404` on `/sumdb/.../supported`.
Clients fall back to `sum.golang.org` directly. This is documented
in the operator guide; the rationale is that sumdb is a transparency
log, not a filtering point, and proxying it adds operational surface
without enforcing anything.

**If the client disables sumdb** (`GOSUMDB=off`, `GONOSUMCHECK=*`,
`GOPRIVATE=*`): the client trusts whatever bytes Heimdall serves.
That makes Heimdall the sole integrity authority for those clients,
which is a posture decision for the operator, not the proxy.

## 8. Bypass surface

- **`GOPROXY=...,direct`** — protocol-level fall-through to the VCS.
  Mitigated by removing `direct` from `GOPROXY`. Document.
- **`GONOPROXY=*`** — patterns always go direct. Same mitigation.
- **`replace module/x => module/fork`** — the proxy sees requests
  for the fork's module path. `allowDeny` must cover both names or
  the fork escapes.
- **`replace module/x => ./local`** — filesystem replacement; proxy
  never consulted. Enforce at CI lint time, not at proxy.
- **`vendor/`** — `go build -mod=vendor` (default when vendor dir
  exists and `go 1.14+` directive is set) is fully offline. Out of
  scope. Enforce at `go mod vendor` time (which does go through the
  proxy).
- **`GOSUMDB=off`** — disables integrity verification, makes the
  proxy the sole authority. Operator policy concern.

## 9. Out of MVP

- sumdb proxying (`/sumdb/...`).
- Search/discovery (no protocol surface for it).
- `Disable-Module-Fetch` header (non-standard, used by some proxies
  to indicate "serve from cache only"; not required by the official
  `go` client).
- Authentication (`go` reads `.netrc` and module-private flow for
  upstream; Heimdall does not need to forward credentials).

## 10. Open questions

- **`@latest` after filtering.** Must return newest surviving
  version, not whatever upstream's `@latest` says. Implementation:
  fetch `/list`, filter, return newest passing. Cache TTL = feed TTL.
- **Cold-cache amplification.** `.info` per version on a 200-version
  module is 200 round-trips on first `list` request. Acceptable for
  MVP given immutable per-version cache; revisit if a feed proves
  pathological.
- **Pseudo-version handling.** Parse timestamp from version string
  directly; do not require `.info` for the timestamp itself. Confirm
  cryptographic hash via `.info`/sumdb on demand.
- **Path-prefix-walk 404s.** The proxy must return 404 (not 403) for
  prefixes that are not modules. `allowDeny` must not collide with
  the prefix walk. Decision: rule evaluation happens *after* upstream
  resolves the module path; prefix-walk 404s are propagated
  unchanged.
- **`@latest` vs `list` consistency.** If `@latest` is filtered out,
  what does the proxy say? Decision: rewrite `@latest` to newest
  surviving version (same posture as npm `dist-tags.latest`).

## 11. Implementation sketch

New project: `src/Heimdall.Ecosystems.Go/`.

- `IGoModuleUpstreamClient` — Polly-resilient HttpClient for `list`,
  `@latest`, `.info`, `.mod`, `.zip`.
- `GoModuleService` — orchestrates per-endpoint fetch + filter +
  cache. Mirrors `NuGetV3MetadataService` in shape.
- `GoModuleTransformer` — projects `list` through the filter,
  rewrites `@latest`, gates `.info`/`.mod`/`.zip`.
- `GoModulePathEscaper` — bidirectional case-escape per protocol.
- `GoModuleZipProxyService` — single-version gate, stream
  pass-through for `.zip`.

Controllers:

- `GoModuleController` — `/go/<feed>/<module>/@v/list`,
  `/@latest`, `/@v/<v>.{info,mod,zip}`.
- `GoSumdbController` — only `/sumdb/<name>/supported` returning 404,
  to make the client's fallback explicit and logged.

Config:

```yaml
heimdall:
  ecosystems:
    go:
      feeds:
        - name: strict
          upstream: "https://proxy.golang.org/"
          cacheTtl: "00:10:00"
          rules:
            - type: minAgeDays
              days: "14"
            - type: allowDeny
              patterns: "github.com/mycorp/*;!github.com/mycorp/internal-*"
```

## 12. Acceptance criteria

- `GOPROXY=http://heimdall/go/strict go build` resolves and builds a
  small project end-to-end.
- Versions younger than `minAgeDays` 403 on `.info` (and `.mod`,
  `.zip`).
- Denied module paths 403 on all five endpoints.
- A pinned `go.sum` entry for a filtered version produces 403 on
  `.info` with the rule name in the body.
- `@latest` returns the newest surviving version, not the upstream
  latest.
- `.zip` bytes are byte-identical to upstream (sumdb hash unchanged
  for clients still using `sum.golang.org`).
- Path-prefix-walk 404s are not converted to 403.
- Tests cover the escape mapping, pseudo-versions, `@latest`
  rewriting, the `go.sum`-pinned-version gate, and the
  `/sumdb/.../supported` → 404 response.
