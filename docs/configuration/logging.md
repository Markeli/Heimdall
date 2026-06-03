---
sidebar_position: 5
---

# Logging

Heimdall logs structured JSON to stdout via [Serilog](https://serilog.net/).
There is also an optional audit log channel for filtering decisions.

## Serilog

The Serilog block at the top level of `config.yml` is read by
`UseSerilog(... ReadFrom.Configuration(ctx.Configuration))` in `Program.cs`.
The default is:

```yaml
Serilog:
  MinimumLevel:
    Default: "Information"
    Override:
      "Microsoft": "Warning"
      "Heimdall.Audit": "Information"
```

Most settings supported by
[`Serilog.Settings.Configuration`](https://github.com/serilog/serilog-settings-configuration)
work — minimum levels, per-source overrides, enrichers. The output is always
JSON to console; that is wired in code, not configuration, because container
log scraping expects a single, predictable stream.

Common operator changes:

- Raise verbosity for one source while leaving the rest alone:

  ```yaml
  Serilog:
    MinimumLevel:
      Default: "Information"
      Override:
        "Heimdall.Ecosystems.NuGet": "Debug"
  ```

- Suppress noise from a specific component:

  ```yaml
  Serilog:
    MinimumLevel:
      Override:
        "Microsoft.AspNetCore.HttpLogging": "Warning"
  ```

`HEIMDALL_*` environment variables also override Serilog config — useful for
flipping debug logging in production without redeploying.

A structured entry is also emitted per HTTP request (method, path, status,
elapsed); that behaviour is fixed in code and needs no configuration.

## Audit log

The audit log (source `Heimdall.Audit`) records filtering decisions — every
version Heimdall served or denied, the feed it came from, and the reason
attached to deny verdicts.

Toggle via configuration:

```yaml
heimdall:
  observability:
    audit:
      enabled: true   # default
```

When disabled, no audit lines are emitted; the per-request log entries are
unaffected.
