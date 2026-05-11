---
sidebar_position: 1
---

# Deployment

Heimdall is distributed as a container image. This page walks through
running it with `docker run`, with Docker Compose, and points at the knobs
you most commonly tune at deploy time.

## Single container

```sh
docker run --rm -p 8080:8080 \
  -v $(pwd)/config.yml:/app/config.yml:ro \
  ghcr.io/markeli/heimdall:latest
```

| Argument | Purpose |
|---|---|
| `-p 8080:8080` | Publish the listener. |
| `-v $(pwd)/config.yml:/app/config.yml:ro` | Provide configuration. The container looks for `config.yml` next to `Heimdall.Api.dll` at `/app/`. |
| `:ro` | Recommended — Heimdall hot-reloads but never writes config. |

Pin to a tag (`:0.1.0`) in production rather than `:latest`. Tagged images
are immutable and reproducible.

## Docker Compose

```yaml
services:
  heimdall:
    image: ghcr.io/markeli/heimdall:0.1.0
    ports:
      - "8080:8080"
    volumes:
      - ./config.yml:/app/config.yml:ro
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "wget", "-qO-", "http://localhost:8080/readyz"]
      interval: 30s
      timeout: 5s
      retries: 3
```

`ASPNETCORE_ENVIRONMENT` picks up the `config.{Environment}.yml` overlay —
see [Configuration overview](../configuration/overview.md).

## Configuration via environment variables

Every key under `heimdall.*` is overridable via an environment variable with
the `HEIMDALL_` prefix and double-underscores for nesting:

```sh
export HEIMDALL_HEIMDALL__SERVER__LISTEN="http://0.0.0.0:9090"
export HEIMDALL_HEIMDALL__SERVER__PUBLICBASEURL="https://nuget.internal.example"
```

This is the recommended way to flip per-environment toggles (audit on/off,
log level) without baking them into the image.

## Behind a reverse proxy

If Heimdall sits behind nginx / Traefik / a load balancer:

1. Set `heimdall.server.publicBaseUrl` to the **external** URL clients hit,
   not Heimdall's bind address.
2. List the proxy IPs under `heimdall.server.forwardedHeaders.knownProxies`
   (or networks under `knownNetworks`). Without this, Heimdall does not
   trust `X-Forwarded-*` and request logs will show the proxy IP, not the
   client.
3. Pass through `Host` and `X-Forwarded-Proto`.

```yaml
heimdall:
  server:
    listen: "http://0.0.0.0:8080"
    publicBaseUrl: "https://nuget.internal.example"
    forwardedHeaders:
      knownNetworks:
        - "10.0.0.0/24"
```

## Probes

- Liveness — `GET /healthz`.
- Readiness — `GET /readyz` (returns 503 when the upstream is unreachable).

See [Health and metrics](../api/health-and-metrics.md) for the response
shapes.

## Resource sizing

Heimdall is single-instance in the MVP. A modest pod is sufficient:

| Resource | Suggested |
|---|---|
| CPU | 250 m – 1 vCPU |
| Memory | 256 MiB – 512 MiB |

Memory usage grows with the L1 cache size (`heimdall.cache.l1.maxEntries`).
A 50 000-entry L1 with registration documents typically sits well under 200
MiB.
