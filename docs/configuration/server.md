---
sidebar_position: 2
---

# Server

Server-level settings live under `heimdall.server`.

## Schema

```yaml
heimdall:
  server:
    listen: "http://0.0.0.0:8080"        # Kestrel bind URL
    publicBaseUrl: "http://localhost:8080" # external URL clients reach Heimdall on
    forwardedHeaders:
      knownProxies: []                    # individual proxy IPs to trust
      knownNetworks: []                   # CIDR networks to trust
    search:
      defaultTake: 20                     # default page size for /query
      maxTake: 100                        # upper clamp on a client-supplied take
      maxConcurrentEnrichmentFetches: 8   # cap on parallel enrichment fetches per search page
                                          # (defaults to the host processor count)
```

## Keys

### `listen`

Kestrel bind URL (scheme + host + port). The default is
`http://0.0.0.0:8080`. Override with TLS termination upstream if needed —
Heimdall itself does not terminate TLS in the MVP.

### `publicBaseUrl`

The external URL clients use to reach Heimdall. **Required** and **must be
non-empty.** Registration and service-index responses rewrite NuGet `@id`
fields to point at Heimdall using this base URL — without it, clients would
follow `@id` links straight back to nuget.org and bypass filtering.

If Heimdall sits behind a reverse proxy at `https://nuget.internal.example`,
set:

```yaml
publicBaseUrl: "https://nuget.internal.example"
```

A trailing slash is tolerated but not required.

### `forwardedHeaders`

When non-empty, registers ASP.NET Core's
[`UseForwardedHeaders`](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer)
middleware to honour `X-Forwarded-For` and `X-Forwarded-Proto` from the listed
proxies.

```yaml
forwardedHeaders:
  knownProxies:
    - "10.0.0.5"
  knownNetworks:
    - "10.0.0.0/24"
```

When both `knownProxies` and `knownNetworks` are empty the middleware is not
registered and Kestrel falls back to its loopback-only default — safer than
blindly trusting forwarded values.

### `search.defaultTake`

Default page size for the `query` endpoint when the client omits or passes a
non-positive `take`. Constrained to `1..100` by the validator. Default `20`.

### `search.maxTake`

Upper bound applied to a client-supplied `take`. Because each returned hit can
trigger a metadata enrichment fetch, an unbounded `take` would let a single
request fan out into arbitrarily many upstream calls; this caps it. A `take`
above `maxTake` is clamped down. Must be `>= 1`. Default `100`.

### `search.maxConcurrentEnrichmentFetches`

NuGet search results carry no per-version publish dates, so to apply date-based
rules (such as `minAgeDays`) consistently Heimdall enriches each search hit with
the publish dates from that package's registration index. (Enrichment is skipped
entirely for feeds that have no date-based rule.) Those registration documents
are fetched concurrently and served from the same cache as the metadata
endpoints; this key bounds how many run in parallel for a single search page,
keeping the upstream and thread-pool fan-out in check under load. Must be
`>= 1`. Defaults to the host's processor count (`Environment.ProcessorCount`).
