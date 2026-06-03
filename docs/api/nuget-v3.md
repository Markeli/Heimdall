---
sidebar_position: 1
---

# NuGet v3

Heimdall implements the read-only subset of the
[NuGet v3 protocol](https://learn.microsoft.com/en-us/nuget/api/overview)
that `dotnet nuget` and `nuget.exe` rely on for resolve / restore / install.
Publish flows (`PackagePublish`) are not implemented.

All endpoints are routed under `/nuget/{feed}/v3/...`. `{feed}` is the
logical feed name declared in [configuration](../configuration/feeds.md).

## Service index — `GET /nuget/{feed}/v3/index.json`

Returns the NuGet v3 service index, but **with every resource URL rewritten
to point back at Heimdall**. This is the entire reason `publicBaseUrl` is
required: it is the base for these rewritten URLs.

```sh
curl -fsSL http://localhost:8080/nuget/strict/v3/index.json
```

```json
{
  "version": "3.0.0",
  "resources": [
    {
      "@id": "http://localhost:8080/nuget/strict/v3/registration5-gz-semver2/",
      "@type": "RegistrationsBaseUrl/3.6.0",
      "comment": "Heimdall registration base"
    },
    {
      "@id": "http://localhost:8080/nuget/strict/v3/flatcontainer/",
      "@type": "PackageBaseAddress/3.0.0",
      "comment": "Heimdall flat container"
    },
    {
      "@id": "http://localhost:8080/nuget/strict/v3/query",
      "@type": "SearchQueryService",
      "comment": "Heimdall search proxy"
    }
  ]
}
```

| Response | When |
|---|---|
| `200` | Feed exists. Body is the rewritten index. |
| `404 ProblemDetails` | Feed name unknown. |

## Versions list — `GET /nuget/{feed}/v3/flatcontainer/{id}/index.json`

Returns the array of versions Heimdall is willing to expose for the package,
after running the feed's [filter rules](../rules/overview.md). The response
shape matches NuGet's flat container exactly:

```json
{
  "versions": ["13.0.1", "13.0.2", "13.0.3"]
}
```

Versions are ordered ascending by **semantic version** (so `2.0.0` precedes
`10.0.0`, not lexicographically). The id is case-insensitive. When the feed is
unknown the response is `404 ProblemDetails`; when the package is unknown
upstream **or every version is filtered out**, `404` — Heimdall does not expose
a package that has no servable versions.

## Registration — `GET /nuget/{feed}/v3/registration5-gz-semver2/{id}/index.json`

Returns the registration index document, again with `@id` URLs rewritten and
denied versions pruned. Surviving leaves are ordered by semantic version and the
page `lower`/`upper` bounds are the true min/max of the surviving set. The
current implementation collapses page bounds, so `/page/{lower}/{upper}.json` is
also routed but ignores its bounds — acceptable because Heimdall inlines the
full registration. When every version is filtered out the response is `404`.

## Search — `GET /nuget/{feed}/v3/query`

Standard NuGet search. Query parameters:

| Param | Type | Notes |
|---|---|---|
| `q` | string | Search term. May be empty for "list all". |
| `skip` | int | Pagination offset. |
| `take` | int | Page size. Non-positive values default to `heimdall.server.search.defaultTake` (default 20); values above `heimdall.server.search.maxTake` (default 100) are clamped. |
| `prerelease` | bool | When `true`, include prerelease versions. |

The response is the upstream search response with rejected versions filtered
out and `@id` URLs rewritten. Each hit's surviving versions are ordered by
semantic version, and the hit's primary `version` is recomputed as the latest
survivor: the highest stable version, falling back to the highest prerelease —
or, when the query passed `prerelease=true`, simply the highest version overall.
A hit whose every version is filtered out is dropped from the results, but the
response's `totalHits` preserves the upstream's global total so paging still
works.

Because NuGet search hits carry no per-version publish dates, Heimdall enriches
each hit with the dates from that package's registration index before filtering,
so date-based rules (e.g. `minAgeDays`) apply to search exactly as they do to
the versions list and registration endpoints. Enrichment is skipped for feeds
without a date-based rule, and its parallelism is bounded by
[`search.maxConcurrentEnrichmentFetches`](../configuration/server.md).

```sh
curl -fsSL 'http://localhost:8080/nuget/strict/v3/query?q=Newtonsoft.Json&take=3'
```

## Download — `GET|HEAD /nuget/{feed}/v3/flatcontainer/{id}/{version}/{file}.nupkg`

The download gate. Heimdall re-evaluates the rules for the requested version
(in case the client raced ahead of the listing cache), then either streams
the binary from upstream to the client, or returns
`403 ProblemDetails`:

```json
{
  "type": "https://heimdall.local/problems/blocked",
  "title": "package version blocked",
  "status": 403,
  "ruleName": "minAgeDays",
  "detail": "version published 0.3 days ago, requires 14"
}
```

`HEAD` is supported and follows the same gate logic — clients use it to
probe for cached availability before issuing a `GET`.

| Response | When |
|---|---|
| `200` | Allowed; binary streamed. |
| `403 ProblemDetails` | A rule rejected the version. Reason in body. |
| `404 ProblemDetails` | Feed unknown. |
| `404` | Feed known, version not found upstream. |
| `502 / 504` | Upstream failure (passed through). |
