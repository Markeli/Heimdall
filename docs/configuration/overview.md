---
sidebar_position: 1
---

# Configuration overview

Heimdall is configured via a layered YAML file. The default file lives next to
`Heimdall.Api.dll` as `config.yml`; the container image ships one at
`/app/config.yml` which you typically override by bind-mounting your own.

## Layering

Configuration is loaded in this order, with later entries overriding earlier
ones (see `src/Heimdall.Api/Program.cs`):

1. `config.yml`
2. `config.{Environment}.yml` — where `{Environment}` is `Development`,
   `Production`, etc., taken from `ASPNETCORE_ENVIRONMENT`.
3. `config.secret.yml` — gitignored by convention; reserved for credentials
   when L2 caches and authentication land.
4. Environment variables prefixed with `HEIMDALL_`. The mapping uses double
   underscores for nesting: `heimdall.server.listen` becomes
   `HEIMDALL_HEIMDALL__SERVER__LISTEN`.

All YAML files are loaded with `reloadOnChange: true`, so edits are picked up on
the next request without a restart (a failed validation keeps the previous
snapshot live). That lets you ramp `minAgeDays` or flip a feed allow-list on a
running instance.

## Top-level structure

```yaml
heimdall:
  server:        # see configuration/server
    listen: "http://0.0.0.0:8080"
    publicBaseUrl: "http://localhost:8080"
  cache:         # see configuration/cache
    l1:
      defaultTtl: "00:05:00"
    l2:
      provider: "none"
  ecosystems:    # see configuration/feeds
    nuget:
      feeds:
        - name: strict
          upstream: "https://api.nuget.org/v3/index.json"
          cacheTtl: "00:10:00"
          rules:
            - type: minAgeDays
              days: "14"
  observability:
    metrics:
      path: "/metrics"
    audit:
      enabled: true

Serilog:         # see configuration/logging
  MinimumLevel:
    Default: "Information"
    Override:
      "Microsoft": "Warning"
```

Every subsection is documented on its own page:

- [Server](server.md) — listen address, public URL, proxy trust.
- [Cache](cache.md) — L1/L2 strand options and TTLs.
- [Feeds](feeds.md) — per-feed upstream URLs and rule lists.
- [Logging](logging.md) — Serilog and audit logging.

Configuration is bound with `ValidateOnStart()`, so misconfigured fields fail
loudly at startup. The most common culprit is `publicBaseUrl` — it must be a
non-empty absolute URL, since registration responses use it to rewrite `@id`
values (see [Server](server.md)).
