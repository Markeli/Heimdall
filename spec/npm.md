# npm — concept study

Status: draft. Reference: [`ecosystems-overview.md`](ecosystems-overview.md).

## 1. Goal and non-goals

**Goal.** Make `npm install`, `npm ci`, `yarn`, and `pnpm` work
against Heimdall as a sole registry, with `minAgeDays` and `allowDeny`
applied to every metadata listing and enforced at every tarball
download.

**Non-goals for v1.**

- `npm publish` / `npm unpublish` / `npm deprecate` / `dist-tag`
  mutations. Heimdall is read-only.
- `npm login` / `whoami` / `npm adduser`.
- `npm audit` (advisory endpoints).
- Hooks, two-factor, package access ACLs.
- Search beyond `GET /-/v1/search` (no autocomplete, no popularity
  metadata).
- GitHub Packages / private-mirror quirks (e.g. missing `time` field).
  Documented as a deferred limitation.

## 2. Protocol surface

| Endpoint | Method | Purpose | In scope |
|---|---|---|---|
| `GET /<pkg>` | GET | packument — all versions of a package | yes |
| `GET /@scope/<pkg>` | GET | scoped variant | yes |
| `GET /<pkg>/<version>` | GET | single-version manifest | yes |
| `GET /<pkg>/-/<file>.tgz` | GET, HEAD | tarball download | yes |
| `GET /-/v1/search` | GET | registry search | yes |
| `GET /` | GET | DB stats / capability probe | pass-through |
| `PUT /<pkg>` and other writes | * | publish / mutate | **405** |

**Packument variants.** Same URL, content negotiated:

- `Accept: application/json` → full packument (every historical
  version's manifest plus `time`, `users`, `readme`, etc.).
- `Accept: application/vnd.npm.install-v1+json` → abbreviated
  packument: only install-time fields. This is what npm/pnpm/yarn
  request by default. Heimdall must filter both shapes identically.

**Scoped packages.** URL-encoded `/` is accepted by registries but
clients send the raw form `@scope/name`. The router must accept both
`@scope/name` and `@scope%2Fname`. The tarball file name drops the
scope: `/@scope/name/-/name-1.2.3.tgz` — do not assume the file name
contains the scope.

**Endpoints that `npm install` actually needs.** Only the packument
(abbreviated) and the tarball. Everything else in the in-scope list
is optional for the install hot path and is included for tooling
parity (`npm view`, `npm pack`, `npm search`).

## 3. `minAgeDays`: where the timestamp lives

`packument.time` is an object: `{ created, modified, <version>:
ISO8601, unpublished?: {...} }`. The per-version entries are
ISO 8601 publish timestamps. **One filtering pass over the
packument is sufficient** — no per-version round-trip needed.

Pseudocode:

```text
for each version v in packument.versions:
  published = packument.time[v]
  if published is null or now - published < minAgeDays:
    drop v from versions
    drop v from time
    if dist-tags.<tag> == v: rewrite tag to newest surviving version
```

Notes:

- `time.unpublished` is an *object*, not a string. Skip it when
  iterating `Object.entries(time)`. Pass it through unchanged.
- `time` may be absent on mirrored registries (e.g. GitHub Packages
  dropped it in March 2021). MVP policy: if `time[<version>]` is
  missing and `minAgeDays > 0`, the version is rejected (same
  fail-closed posture as the NuGet `published == null` case).

## 4. Filter integration points

Two enforcement layers, same as the NuGet MVP:

- **`VersionListFilter`** applied to the packument's `versions` and
  `time` maps inside a new `NpmPackumentTransformer`. The transformer
  also (a) rewrites `dist.tarball` URLs to point at Heimdall, (b)
  scrubs `dist-tags` entries that point at filtered versions, and (c)
  rewrites `dist-tags.latest` to the newest surviving version.
- **`SingleVersionGate`** applied at `GET /<pkg>/-/<file>.tgz`. A
  `package-lock.json` with absolute `resolved` URLs causes `npm ci`
  to skip the packument entirely; without a gate here, the filter is
  evaded.

Search results from `GET /-/v1/search` are filtered through the same
rules over `objects[].package` (`name`, `date`).

## 5. Status codes when filtered

- Filtered version requested directly via `GET /<pkg>/<version>` →
  `403 ProblemDetails`.
- Filtered tarball download → `403 ProblemDetails`. npm prints the
  response body for non-2xx tarball fetches, so the rule name reaches
  the CI log.
- Mutating verbs on read-only endpoints → `405 Method Not Allowed`.

Why not `404`: npm retries on 404 ("no matching version available")
and surfaces a misleading "no matching versions" error to the user.
`403` is honest and routes to the operator's rule name.

## 6. Client behaviour on restore

Without lockfile (`npm install`): abbreviated packument per direct
and transitive dependency, local SAT-like resolution, then tarball
downloads with up to 15 parallel sockets. A medium project triggers
~1,400 metadata + ~1,400 tarball requests against a cold cache.

With lockfile (`npm ci` or `npm install` with consistent
`package-lock.json`/`npm-shrinkwrap.json` / `yarn.lock` / `pnpm-lock.yaml`):
**packument fetching is skipped**. Client goes straight to the
`resolved` URL, verifies `integrity` against the local hash, fails on
`EINTEGRITY` if bytes diverge.

**Integrity model.** `dist.integrity` (SRI: `sha512-…`) is verified
post-download. Heimdall must stream bytes byte-identically — no
transcoding, no decompression-recompression. Rewriting
`dist.tarball`'s host in the packument is fine because clients verify
hash, not URL.

## 7. Bypass surface

- **Absolute upstream URLs in lockfiles.** A `package-lock.json`
  generated against the public registry embeds `https://registry.npmjs.org/...`
  in `resolved`. `npm ci` hits that URL directly, ignoring the
  configured registry. Mitigation: enforce egress to npmjs.org at the
  network layer (route all traffic through Heimdall) or instruct
  users to regenerate the lockfile against the proxy. Document the
  detection signal: log unexpected upstream fetches the proxy itself
  performs (clients still resolve the host via DNS at home).
- **`npm config set registry` not applied.** Per-project `.npmrc`
  overrides global config. The proxy cannot enforce client-side
  configuration. Tooling-level guidance only.
- **Yarn Plug'n'Play / pnpm content-addressable store.** Once a
  tarball is in the local cache or `pnpm` store, the client does not
  re-fetch. Policy tightening is not retroactive. Document.

## 8. Out of MVP

- Publish path (any `PUT` / `DELETE`).
- `/-/npm/v1/security/*` audit endpoints.
- `/-/all` legacy full-listing.
- `/-/user/*` authentication endpoints.
- Hooks.
- Two-factor.

## 9. Open questions

- **Abbreviated vs. full packument as the canonical filter
  target.** Filtering both is mandatory, but the upstream
  representation differs. Decision: fetch the full packument, derive
  the abbreviated form locally (so `time` is always available even
  when the client only asked for abbreviated).
- **`dist-tags` rewrite policy.** MVP default: rewrite `latest`
  (and other tags that point at filtered versions) to the newest
  surviving version, log a warning. Open question: should a config
  flag let operators opt for "fail loudly when `latest` is filtered"
  instead?
- **Cache key for packuments.** Same `(ecosystem, feed, name,
  configSnapshot)` shape as NuGet. TTL configurable per feed.
- **Search index consistency.** `total` field after filtering — leave
  upstream value (lies, but conservative) or recompute (truthful, but
  may break paginated clients)? Default to recompute.
- **Handling missing `time`.** Fail closed on `minAgeDays`; pass
  through on `allowDeny` only.

## 10. Implementation sketch

New project: `src/Heimdall.Ecosystems.Npm/`.

- `INpmRegistryUpstreamClient` — Polly-resilient HttpClient, fetches
  full packument, abbreviated packument, single-version manifest,
  search.
- `NpmPackumentService` — orchestrates fetch + filter + URL rewrite +
  cache, mirroring `NuGetV3MetadataService`.
- `NpmPackumentTransformer` — applies `IVersionListFilter` over
  `versions` + `time`, scrubs `dist-tags`, rewrites
  `versions[v].dist.tarball` and `dist-tags.latest`.
- `NpmUrlRewriter` — `publicBaseUrl + /npm/<feed>/<pkg>` etc.
- `NpmTarballProxyService` — single-version gate then stream.
- `NpmSearchService` — filter `objects[]`.

Controllers in `src/Heimdall.Api/`:

- `NpmPackumentController` — packument (both `Accept` variants),
  single-version manifest, search.
- `NpmTarballController` — `.tgz` download.

DI extension `AddNpmEcosystem(IServiceCollection)` mirrors
`AddNuGetV3Ecosystem`. Configuration shape:

```yaml
heimdall:
  ecosystems:
    npm:
      feeds:
        - name: strict
          upstream: "https://registry.npmjs.org/"
          cacheTtl: "00:10:00"
          rules:
            - type: minAgeDays
              days: "14"
            - type: allowDeny
              patterns: "@mycorp/*;!@mycorp/internal-*"
```

Same rule types as NuGet — no new builders needed. Tests follow the
existing `Heimdall.UnitTests` / `Heimdall.IntegrationTests` split,
with WireMock.Net standing in for `registry.npmjs.org`.

## 11. Acceptance criteria

- `dotnet nuget`-equivalent end-to-end test using `npm install`
  inside a container against Heimdall, with two rules configured.
- Versions younger than `minAgeDays` are absent from the packument
  (both `Accept` variants).
- Denied package names return `403 ProblemDetails` on packument.
- A lockfile that pins a filtered version produces `403
  ProblemDetails` on the tarball fetch, with the rule name in the
  body.
- `dist-tags.latest` always points at a surviving version.
- Tarball bytes are byte-identical to upstream (SHA-512 unchanged).
- Tests cover scoped packages, missing-`time` fail-closed, search
  filtering, and the `npm ci` lockfile path.
