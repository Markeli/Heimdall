---
sidebar_position: 2
---

# Monitoring

Heimdall ships three observability surfaces: Prometheus metrics, structured
Serilog JSON logs, and an audit log channel for filtering decisions.

## Prometheus

Scrape `/metrics` (or whatever `heimdall.observability.metrics.path` is set
to). The exposition is the standard
[prometheus-net.AspNetCore](https://github.com/prometheus-net/prometheus-net.AspNetCore)
set:

```yaml
scrape_configs:
  - job_name: heimdall
    metrics_path: /metrics
    static_configs:
      - targets: ["heimdall.internal:8080"]
```

### What to watch

| Metric | Why |
|---|---|
| `http_requests_received_total{code=~"5.."}` | 5xx rate. Heimdall's own 4xx (403 deny, 404 unknown feed) are expected; 5xx is not. |
| `http_request_duration_seconds_bucket{path="/nuget/.../flatcontainer/.../index.json"}` | Listing latency. Cache hits are sub-millisecond; misses depend on upstream. |
| `http_requests_received_total{code="403"}` | Deny rate. A sudden spike usually means a new package showed up that the allow-list excluded. |
| `process_resident_memory_bytes` | Memory. Grows with L1 cache fill. |
| `dotnet_collection_count_total{generation="Gen2"}` | Gen-2 GC frequency — proxy for cache thrash. |

### Suggested alerts

- **Upstream unreachable** — `/readyz` 503 for > 1 minute.
- **5xx rate** — `rate(http_requests_received_total{code=~"5.."}[5m]) > 0.01`.
- **Sustained deny burst** — `rate(http_requests_received_total{code="403"}[5m]) > N`
  for your normal `N`. Often means a config change introduced an
  unintentionally strict rule.

## Logs

`UseSerilogRequestLogging()` emits one JSON entry per request. Sample:

```json
{
  "@t": "2026-05-11T12:34:56.789Z",
  "@l": "Information",
  "@mt": "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms",
  "RequestMethod": "GET",
  "RequestPath": "/nuget/strict/v3/flatcontainer/newtonsoft.json/index.json",
  "StatusCode": 200,
  "Elapsed": 5.31,
  "ConnectionId": "0HMSO...",
  "RequestId": "0HMSO...:00000001"
}
```

Ship to your log backend of choice; everything is plain JSON over stdout.
See [Logging](../configuration/logging.md) to raise verbosity for specific
sources.

## Audit log

When `heimdall.observability.audit.enabled = true`, every filtering decision
is logged under the `Heimdall.Audit` source — version, feed, decision, and
deny reason. This is the source of truth for "which package was blocked,
when, and why":

```json
{
  "@t": "...",
  "@l": "Information",
  "@mt": "filter {Decision} {PackageId} {PackageVersion} on feed {Feed} ({RuleName}: {Reason})",
  "Decision": "Deny",
  "PackageId": "Some.Internal.Lib",
  "PackageVersion": "1.0.0",
  "Feed": "strict",
  "RuleName": "allowDeny",
  "Reason": "package does not match any allow pattern"
}
```

Filter by source `Heimdall.Audit` to isolate the channel from the request
log.
