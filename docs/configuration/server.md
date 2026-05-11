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
