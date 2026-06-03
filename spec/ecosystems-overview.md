# Cross-ecosystem analysis: can Heimdall's model travel?

A side-by-side reading of the four candidate ecosystems against the
NuGet v3 MVP. Each per-ecosystem document expands one column; this one
is the comparison.

## What "Heimdall's model" actually requires of an ecosystem

The MVP makes five assumptions. Every new ecosystem is judged against
all five.

1. **A version-list endpoint we can filter.** The client must learn
   which versions exist by asking the proxy, not by guessing URLs.
2. **A per-version publish timestamp we can read cheaply.** Either it
   already lives in the listing, or one extra round-trip per version
   suffices.
3. **A download endpoint we can intercept.** The version-list filter is
   advisory the moment a lockfile pins a filtered version; the real
   enforcement is at download.
4. **An error contract that propagates to the developer.** When the
   proxy says no, the build agent must see a message that names the
   rule, not a generic 404.
5. **A pass-through binary path.** The proxy streams bytes; clients
   verify cryptographic integrity against an upstream-issued hash
   without the proxy needing to re-sign anything.

NuGet v3 satisfies all five trivially. The others do not.

## At a glance

| Dimension | **NuGet v3** (MVP) | **npm** | **PyPI** | **Go modules** | **Maven Central** |
|---|---|---|---|---|---|
| Version list endpoint | `/flatcontainer/{id}/index.json` | packument (abbrev) | Simple index (HTML/JSON) | `/<mod>/@v/list` | A-level `maven-metadata.xml` |
| Per-version publish time | `catalogEntry.published` (registration) | `packument.time[version]` | PEP 700 `upload-time` (JSON Simple) or fallback `/pypi/<n>/json` | `.info.Time` (per-version GET) | **not in metadata** — `HEAD .pom` `Last-Modified` or Solr `core=gav` |
| Round-trips to filter N versions by age | 1 (registration covers all) | 1 (packument covers all) | 1 (JSON Simple) or 1+N (HTML fallback) | 1 (lazy, gate at `.info`) or 1+N (eager pre-warm) | 1+N (HEAD `.pom`) or 1 (Solr backfill) |
| Download path | `/flatcontainer/{id}/{ver}/{file}.nupkg` | `/<pkg>/-/<pkg>-<v>.tgz` | wheel/sdist absolute URL from index | `/<mod>/@v/<v>.zip` | `/<g>/<a>/<v>/<a>-<v>.<ext>` |
| Filter-rejected status | `403 ProblemDetails` | `403 ProblemDetails` | `403 ProblemDetails` | **`403` required** (not 404/410) | `403 ProblemDetails` |
| Integrity signal | `.nupkg` SHA-512 in index | `dist.integrity` (SRI) | `#sha256=` per file | sumdb `h1:` hash | `.sha1`/`.sha256`/`.asc` sidecars |
| Lockfile bypass | uncommon | **`package-lock.json` `resolved` URL** | `--require-hashes` works through proxy | `go.sum` has hashes only, no URLs | Gradle dep-lock has versions only |
| Most dangerous bypass | egress to api.nuget.org | direct `registry.npmjs.org` from absolute lockfile URL | `extra-index-url` containing upstream | `GOPROXY=...,direct` | `<mirrors>` with `mirrorOf=external:*` |

## The four hard problems by ecosystem

### npm — the easy one

Closest to NuGet v3 conceptually. `packument.time` carries all version
timestamps in the same document the resolver already fetches, so
`minAgeDays` is one filtering pass over `versions` + `time` + the
`dist-tags` map. The only real twist is `dist-tags.latest`: if the
"latest" tag points at a version we filtered out, every `npm install
pkg` (no version) breaks silently, so the proxy must rewrite the tag
to the newest *surviving* version. The lockfile-bypass risk is real
but is a network-policy problem, not a protocol problem.

### PyPI — the round-trip problem

The Simple Repository API (PEP 503/691) was originally designed without
timestamps. PEP 700 added `upload-time` to the JSON variant, but only
JSON Simple clients see it; HTML Simple is still the fallback for some
mirrors. When the upstream advertises only HTML Simple,
`minAgeDays` requires either an N-times call to `/pypi/<pkg>/<v>/json`
to learn each upload time, or a one-shot call to `/pypi/<pkg>/json`
returning every release's `urls[].upload_time` in one document. The
PyPI public registry has served JSON Simple with `upload-time` (PEP 700)
since 2023, so against PyPI itself the round-trip cost is zero.
Against private mirrors that have not adopted PEP 700, the proxy must
either degrade (refuse `minAgeDays` for that feed) or pay the JSON-API
cost. Resolver backtracking (pip's, uv's, poetry's) means many probes,
but `Cache-Control: max-age=600` from PyPI bounds the amplification.

### Go modules — the timestamp-fan-out and the 403-must-be-403 problem

Two distinct issues.

The first is the same as PyPI: `.info` is one GET per version, and
`/<mod>/@v/list` carries no timestamps. Eager pre-warm of `.info` for a
200-version module fans out 200 requests on first contact. The data is
immutable (a version's commit time never changes), so cache-once-forever
applies, but the cold-cache amplification is real. MVP avoids the
amplification by serving `/list` verbatim and letting the `.info` /
`.mod` / `.zip` gate be authoritative (see [go.md §4](go.md)).

The second is more dangerous and is unique to Go: **the choice of HTTP
status code on a filter rejection changes whether the filter is even
enforceable.** Returning `404` or `410` causes the `go` client to fall
through to the next `GOPROXY` entry, which in default installs is
`direct` — i.e. straight to the VCS, bypassing Heimdall. Any
non-404/410 (`403`, `500`, etc.) is treated as a hard error and stops
the chain. So Heimdall must return `403` for filter rejections, and
the docs must recommend operators remove `direct` from `GOPROXY` for
strict environments. The MVP's existing `403 ProblemDetails` is the
correct choice for Go, *because of* a protocol quirk, not just for
consistency.

There is also a sumdb (`sum.golang.org`) traffic strand that Heimdall
deliberately does not proxy in MVP; we recommend clients keep
`GOSUMDB=sum.golang.org` direct.

### Maven Central — the missing-timestamp problem

The hardest of the four. The release-level `maven-metadata.xml` simply
**does not** publish a per-version timestamp; `<lastUpdated>` is the
file-wide moment of the last metadata regeneration, not the publish
time of any individual version. The honest options are:

- **`HEAD` the `.pom` and read `Last-Modified`.** Cheapest, lives on
  the same CDN we are already proxying. Not formally specified as an
  API contract, but stable in practice on Maven Central.
- **Solr `core=gav` at `search.maven.org`.** Documented, returns
  `timestamp` per version. Separate host, stricter rate limits,
  indexing lag of minutes to hours.

Recommended posture: prefer `HEAD .pom`, fall back to Solr, cache the
result forever (`(group, artifact, version) → timestamp` is
immutable). Then the rest of the problem looks like the others:
filter the metadata version list, gate the artifact download.

The other Maven-specific surprise is that **release-version metadata is
decorative for any pinned dependency**: Maven and Gradle resolving a
fixed `<version>` skip the A-level `maven-metadata.xml` entirely and
go straight to `/<g>/<a>/<v>/<a>-<v>.pom`. So the binary gate is
again the load-bearing piece; filtering the metadata is a UX-only
nicety for `mvn versions:display-dependency-updates` and friends.

## The bypass surface is wider than NuGet's

NuGet's `dotnet nuget` client follows the configured feed faithfully;
the bypass surface is small (someone could `dotnet nuget add source
https://api.nuget.org/...` directly, but that is a deliberate action).
The four candidate ecosystems each have a richer bypass story:

- **npm:** `package-lock.json` written against the upstream embeds
  absolute `registry.npmjs.org` URLs in `resolved`. `npm ci` honours
  those URLs verbatim, bypassing any proxy reconfiguration. Egress
  policy is the only mitigation.
- **PyPI:** `pip install --index-url ... --extra-index-url ...` lets a
  developer keep upstream alongside the proxy, and pip will pick the
  "best" version from either, defeating allow/deny. uv inverts the
  priority (helpful), poetry uses explicit labels (helpful), but pip
  remains the lowest common denominator.
- **Go modules:** `GOPROXY=https://heimdall,direct` falls through to
  the VCS on any 404/410. The fact that the protocol *requires*
  fallback semantics is why the status-code choice is load-bearing.
  `vendor/` and `replace` directives are fully client-side and
  invisible to the proxy.
- **Maven:** `<mirrors>` with `mirrorOf=external:*` excludes internal
  repos; `mirrorOf=central` covers only one well-known id. Gradle's
  `repositories { ... }` is per-build and trivially adds upstream.

Implication for the spec: every ecosystem doc must list its bypasses
explicitly so operators know which network/policy control compensates.

## What is shared and what is per-ecosystem

**Shared (`Heimdall.Core`, unchanged):**

- `IRule`, `RuleVerdict`, `RuleEvaluator`, `RuleContext`.
- `VersionListFilter`, `SingleVersionGate`.
- `MinAgeDaysRule`, `AllowDenyRule` (and their builders).
- `IConfigSnapshotProvider`, `IFeedConfigLookup`.
- `PackageCoordinates`, `PackageVersionMetadata`.

**Per-ecosystem (new `Heimdall.Ecosystems.<Name>` projects):**

- Upstream HTTP client (Polly-resilient).
- Metadata service (orchestration + cache).
- Transformer (filter-and-rewrite of the listing document).
- URL rewriter (proxy → upstream and back).
- Download proxy service (gate + stream).
- Controllers under a single route prefix.

The MVP's project shape transplants without surgery. The work per
ecosystem is the protocol projection, the timestamp-acquisition
strategy, and the status-code policy. No new rule types are needed
for any of the four — `minAgeDays` and `allowDeny` cover the whole
quartet at v1.

## Open questions for the implementation phase

These are deliberately *not* decided in this draft; each is called out
in the per-ecosystem document and will be resolved when its
implementation issue is picked up.

- **`@latest`/`dist-tags`/`<latest>` rewriting** after filtering.
  Every ecosystem with a "latest" pointer needs to rewrite it. MVP
  default across all four is "always rewrite to newest surviving
  version, log a warning". The open question is whether a config flag
  should let operators opt out (e.g. fail loudly when `latest` is
  filtered).
- **Cold-cache amplification for `.info`/`HEAD .pom`/JSON-API
  timestamp fan-out.** When does the proxy back off from filtering
  versus block until warm?
- **Snapshot/pre-release semantics.** PyPI yanked, Maven snapshots,
  npm `dist-tags.beta`, Go pseudo-versions. Are these in scope for
  `minAgeDays` at all, or should the rule grow a flag?
- **Sumdb proxying for Go.** MVP non-goal; revisit if air-gapped
  deployments materialise.
- **Single feed vs. multi-feed per ecosystem.** NuGet already supports
  named feeds; carry the pattern to all four. Implication: per-feed
  rule sets and per-feed upstream URLs from day one.

## Roadmap

The implementation work decomposes into four independent
issues, each driven by its own per-ecosystem spec in this directory.
There is no hard ordering — each spec is self-contained — but the
recommended order, based on conceptual surface area and value
delivered per unit of work, is:

1. **npm.** Closest to NuGet, biggest user base, no novel
   timestamp problem.
2. **PyPI.** Well-specified, but the round-trip story needs care.
3. **Maven.** The timestamp gap drives most of the design.
4. **Go.** The status-code policy and `direct` fallback require the
   most operator documentation; smallest user base of the four for
   our target audience.
