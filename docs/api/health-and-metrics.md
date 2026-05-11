---
sidebar_position: 2
---

# Health and metrics

Heimdall exposes three endpoints aimed at orchestrators, load balancers, and
Prometheus.

## Liveness — `GET /healthz`

Returns `200 OK` with body `ok` as long as the process is able to handle
HTTP requests. There is no real check behind it — by definition a process
that can answer this endpoint is alive.

Use this for the **liveness probe** in Kubernetes / Docker. If the body or
status code stops responding, the orchestrator should restart the pod.

```sh
curl -fsSL http://localhost:8080/healthz
# ok
```

## Readiness — `GET /readyz`

Runs every health check registered with `services.AddHealthChecks()` and
returns a structured JSON report. The HTTP status code is `200` when every
check reports healthy, `503` otherwise.

```sh
curl -fsSL http://localhost:8080/readyz | jq
```

```json
{
  "status": "Healthy",
  "entries": {
    "upstream": {
      "status": "Healthy",
      "description": "https://api.nuget.org/v3/index.json reachable"
    }
  }
}
```

The bundled `upstream` check is `UpstreamReadinessCheck` — it probes each
configured feed's upstream URL. When the upstream is unreachable, the
endpoint returns `503` and an entry describing why. Use this for the
**readiness probe** so the load balancer takes Heimdall out of rotation
during upstream outages.

## Metrics — `GET /metrics`

Prometheus exposition via
[prometheus-net](https://github.com/prometheus-net/prometheus-net). The path
defaults to `/metrics` and is configurable:

```yaml
heimdall:
  observability:
    metrics:
      path: "/metrics"
```

`UseHttpMetrics()` (`prometheus-net.AspNetCore`) automatically publishes the
standard ASP.NET Core HTTP counters:

| Metric | Type | Notes |
|---|---|---|
| `http_requests_received_total` | counter | Per method/path/status. |
| `http_request_duration_seconds` | histogram | Latency buckets. |
| `http_requests_in_progress` | gauge | Concurrent requests. |
| `process_*` | gauges | Standard process and GC metrics. |
| `dotnet_collection_count_total` | counter | GC collections by generation. |

Scrape config example:

```yaml
scrape_configs:
  - job_name: heimdall
    metrics_path: /metrics
    static_configs:
      - targets: ["heimdall.internal:8080"]
```

Key dashboards are sketched in [Monitoring](../operations/monitoring.md).
