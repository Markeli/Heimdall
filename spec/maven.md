# Maven Central — concept study

Status: draft. Reference: [`ecosystems-overview.md`](ecosystems-overview.md).

## 1. Goal and non-goals

**Goal.** Make `mvn install`, `mvn dependency:resolve`, and `gradle
build` work against Heimdall as the configured mirror, with
`minAgeDays` and `allowDeny` applied to every `maven-metadata.xml`
listing and enforced at every artifact download.

**Non-goals for v1.**

- Snapshots (`-SNAPSHOT` versions and V-level snapshot metadata).
  Maven Central is release-only; snapshots historically lived on
  Sonatype's OSSRH snapshots repo. Adding them is a separate
  ecosystem.
- Deploy / staging / publish (`/api/v1/publisher/*`, OSSRH
  upload-and-promote flows).
- GPG keyserver proxying.
- Solr search UI; Heimdall may *call* the Solr API as a timestamp
  source but does not expose it to clients.
- Transitive POM rewriting (we do not edit POM bodies to remove
  dependencies).

## 2. Protocol surface

Maven Central canonical base: `https://repo.maven.apache.org/maven2/`
(also `https://repo1.maven.org/maven2/`). Layout is filesystem-shaped:

```
/<groupId-with-dots-as-slashes>/<artifactId>/<baseVersion>/
  <artifactId>-<version>[-<classifier>].<extension>
```

Files per GAV:

- `.pom` — always present.
- `.jar` — usually present (absent for `<packaging>pom</packaging>`).
- `-sources.jar`, `-javadoc.jar` — optional documentation jars.
- `.module` — optional Gradle Module Metadata.
- Checksum sidecars: `.md5`, `.sha1` always; `.sha256`, `.sha512`
  commonly. Maven Central requires `.md5` + `.sha1`.
- `.asc` — GPG signatures. Required for upload to Central; usually
  not verified by consumers.

Metadata files:

- `<groupId>/<artifactId>/maven-metadata.xml` — A-level. Contains
  `<latest>`, `<release>`, `<versions>`, `<lastUpdated>`. **No
  per-version timestamp.**
- `<groupId>/<artifactId>/<baseVersion>/maven-metadata.xml` —
  V-level, snapshots only. Maps snapshot baseVersion to timestamped
  filename. Out of MVP.

**Endpoints to serve.** Anything under `/maven2/...`. Heimdall does
not need to enumerate endpoints individually — the protocol is "GET
the path". The filter is applied to two specific files
(`maven-metadata.xml` at A-level) and the download gate to all
artifact files at GAV paths.

## 3. `minAgeDays`: where the timestamp lives

**The fundamental problem: it doesn't.** Release `maven-metadata.xml`
has `<lastUpdated>` describing the metadata file itself, not each
version. Per-version publish timestamp is not part of the public
protocol.

Three sources, ranked:

1. **`HEAD` the per-version `.pom` and read `Last-Modified`.**
   Cheapest, on the same CDN we are already proxying. Not formally
   specified as an API contract by Maven Central, but stable in
   practice. One round-trip per version.
2. **Solr `core=gav` at `search.maven.org/solrsearch/select`.** One
   request returns `timestamp` (epoch ms) per version. Separate
   host, stricter rate limits, indexing lag of minutes to hours.
   Suitable for backfill, not for the hot path.
3. **Sonatype Central Portal REST.** Publish-side only, not
   suitable.

MVP picks **`HEAD .pom` with Solr fallback**, cache forever (a
published version's publish time is immutable). The cache is
populated on first `maven-metadata.xml` filter pass.

## 4. Filter integration points

- **`VersionListFilter`** applied to `<versions>` inside A-level
  `maven-metadata.xml`. Drops versions failing `minAgeDays` or
  `allowDeny` (the latter on `<groupId>:<artifactId>`). Rewrites
  `<latest>` and `<release>` to the newest surviving version, sets
  `<lastUpdated>` to "now" as the MVP default (see §10 — preserving
  the upstream value is the alternative).
- **`SingleVersionGate`** applied to every artifact GET at a GAV
  path. This is the load-bearing piece: pinned dependencies skip
  `maven-metadata.xml` entirely.

POM bodies are passed through unmodified. Editing POM
`<dependencies>` to remove filtered deps is rejected as a design
choice — it breaks reproducible builds, can invalidate signatures if
anyone checks them, and the transitively-pinned version still gets
downloaded (and gated) on its own GAV path.

## 5. Status codes when filtered

- Filtered artifact download → `403 ProblemDetails`.
- Filtered version absent from rewritten `maven-metadata.xml` →
  just gone (no signal needed at metadata level).
- Sidecar files (`.sha1`, `.md5`, `.asc`) for filtered artifacts →
  `403 ProblemDetails`, mirroring the artifact.

Maven and Gradle handle 403 on artifact downloads cleanly: the build
fails with the upstream error body included in the message.

## 6. Client behaviour on restore

**Maven (`mvn install` / `dependency:resolve`).** With a fixed
`<version>`, Maven goes **straight to `<g>/<a>/<v>/<a>-<v>.pom`** and
**does not** fetch A-level `maven-metadata.xml`. It walks
`<parent>`, `<dependencyManagement>` imports, transitive
`<dependencies>`, downloading each POM and then the binary. A-level
metadata is fetched only for dynamic versions (`RELEASE`, `LATEST`,
ranges like `[1.0,2.0)`) or for plugins like
`versions:display-dependency-updates`.

**Gradle.** Variant-aware. Looks for `.module` first (when the POM
marker indicates it exists), falls back to `.pom`. Aggressive
parallel downloads. Dependency-locking pins concrete versions; with
a lockfile, Gradle treats versions as `strictly(version)` and skips
A-level metadata.

**Conclusion: A-level metadata filtering is decorative.** Every
pinned dependency bypasses it. The binary gate is the only
enforceable control.

## 7. Integrity verification

- **Checksums.** Maven Resolver downloads `.sha1` by default and
  validates transport integrity. `.md5`, `.sha256`, `.sha512` are
  opt-in. `checksumPolicy` (`fail`, `warn`, `ignore`) governs
  mismatch behaviour. Maven defaults to `warn` against Central;
  Gradle defaults stricter. Heimdall must proxy the matching
  sidecars byte-identically with the artifact.
- **GPG `.asc`.** Not verified by default by Maven or Gradle. Upload
  to Central requires signatures, but consumers rarely check them.
  Pass-through is sufficient.

## 8. Bypass surface

- **Mirror configuration.** `~/.m2/settings.xml` `<mirrors>` with
  `<mirrorOf>*</mirrorOf>` routes everything through Heimdall.
  `<mirrorOf>external:*</mirrorOf>` excludes internal repos.
  `<mirrorOf>central</mirrorOf>` matches the well-known `central`
  id only. A misconfigured mirror chain bypasses entirely. Document
  the recommended setting.
- **Gradle `repositories { ... }`.** Per-build, trivially adds
  upstream. Enforce via init-script policy or build-scan auditing.
- **`<dependencyManagement>` BOMs.** A consumer can pull a forbidden
  version transitively via a BOM without naming it in
  `<dependencies>`. The binary gate catches it; metadata filtering
  does not.
- **Maven local repository (`~/.m2/repository/`).** Persistent
  per-user cache. Policy tightening is not retroactive.
- **`-redhat-`, `-android-`, classifier variants.** Same GAV path,
  different file. Rules apply per version, not per classifier;
  sources/javadoc usually shouldn't be age-gated (they are
  documentation, not executed code). MVP applies rules uniformly;
  a future rule flag may exempt sources/javadoc.

## 9. Out of MVP

- Snapshots and V-level metadata.
- Solr search proxying as a client-facing endpoint.
- Sonatype Central Portal publish/staging endpoints.
- Per-classifier rule semantics.
- GPG keyserver / signature verification by the proxy.

## 10. Open questions

- **Cold-cache amplification of `HEAD .pom`.** N versions = N HEAD
  requests on first listing. Acceptable given immutable cache; revisit
  if pathological.
- **Solr rate limits.** Maven Central enforces consumption limits
  since 2024/2025. Fallback to Solr should be rate-limited at the
  proxy.
- **`<lastUpdated>` after rewrite.** Set to "now" so downstream
  caches refresh, or preserve upstream value? MVP: "now". This is
  the only synthetic field in the rewritten metadata.
- **`<latest>` / `<release>` rewriting.** Always rewrite to newest
  surviving version. If no version survives, return `404` on the
  metadata file (better than empty `<versions>`).
- **BOM and parent POM passthrough.** Treat them as ordinary `.pom`
  GETs gated by the rule pipeline against `<groupId>:<artifactId>`.
  Their content is not modified.
- **Sources/javadoc rule semantics.** Apply rules uniformly in MVP;
  a `kinds: [binary, sources, javadoc]` rule field is a v2 ask.

## 11. Implementation sketch

New project: `src/Heimdall.Ecosystems.Maven/`.

- `IMavenCentralUpstreamClient` — Polly-resilient HttpClient for
  metadata, artifacts, and `HEAD .pom` timestamp probes. Optional
  Solr backfill client.
- `MavenMetadataService` — orchestrates fetch + filter + rewrite +
  cache for A-level `maven-metadata.xml`.
- `MavenMetadataTransformer` — parses XML, filters `<versions>`,
  rewrites `<latest>`/`<release>`/`<lastUpdated>`, serialises.
- `MavenArtifactProxyService` — single-version gate, stream
  pass-through for `.pom`, `.jar`, sources/javadoc, sidecars.
- `MavenTimestampResolver` — `HEAD .pom` first, Solr fallback,
  immutable cache.

Controllers:

- `MavenMetadataController` — `GET /maven/<feed>/maven2/<g>/<a>/maven-metadata.xml`
  and its `.sha1`/`.md5`.
- `MavenArtifactController` — `GET /maven/<feed>/maven2/<g>/<a>/<v>/<file>`
  for every artifact and sidecar.

Config:

```yaml
heimdall:
  ecosystems:
    maven:
      feeds:
        - name: strict
          upstream: "https://repo.maven.apache.org/maven2/"
          solrUpstream: "https://search.maven.org/solrsearch/select"
          cacheTtl: "00:10:00"
          rules:
            - type: minAgeDays
              days: "14"
            - type: allowDeny
              patterns: "org.apache.*;com.google.*;!org.mycorp.internal.*"
```

`allowDeny` patterns operate on `groupId:artifactId`, in line with
the Maven coordinate convention.

## 12. Acceptance criteria

- `mvn install` against `<mirrors><mirror><url>http://heimdall/maven/strict/maven2/</url><mirrorOf>*</mirrorOf></mirror></mirrors>` resolves and builds a small project.
- `gradle build` against the same URL resolves and builds.
- Versions younger than `minAgeDays` are absent from rewritten
  A-level `maven-metadata.xml`.
- Denied `groupId:artifactId` return `403 ProblemDetails` on any
  artifact GET.
- A pinned `<version>` for a filtered version produces `403
  ProblemDetails` on the `.pom` and `.jar`.
- `<latest>` and `<release>` always point at surviving versions, or
  the metadata file returns `404` when no version survives.
- Artifact bytes are byte-identical to upstream (`.sha1`/`.sha256`
  unchanged).
- Tests cover the timestamp-resolver path (`HEAD .pom` + Solr
  fallback), BOM transitive resolution, classifier variants, and
  the mirror-config integration.
