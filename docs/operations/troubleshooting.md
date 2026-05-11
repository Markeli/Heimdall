---
sidebar_position: 3
---

# Troubleshooting

The most common failure modes and where to look first.

## `dotnet restore` says the package was not found

The flat-container listing pruned every version. Either the package is on a
deny pattern, the allow-list doesn't include it, or every version is younger
than `minAgeDays`.

1. Inspect the listing directly:
   ```sh
   curl -fsSL http://heimdall/nuget/strict/v3/flatcontainer/somepackage/index.json
   ```
   An empty `versions` array confirms it was filtered out.
2. Check the [audit log](../configuration/logging.md#audit-log) for the
   `RuleName` that rejected it.
3. Adjust [`allowDeny`](../rules/allow-deny.md) or
   [`minAgeDays`](../rules/min-age-days.md) and save `config.yml` —
   hot-reload picks up the change on the next request.

## `403 ProblemDetails` on download

The download gate refused. The body contains the `ruleName` and a
human-readable `detail`. Same fix as above — adjust the rule or accept the
deny.

A 403 on a previously-working version is usually `minAgeDays` ratcheting up
the cooldown — the version was first served when the threshold was lower.

## Heimdall starts but `publicBaseUrl` is empty

`HeimdallOptionsValidator` rejects an empty `publicBaseUrl` at startup. If
you're seeing strange URL-rewriting bugs or registration responses that
point at the upstream instead of Heimdall, double-check that the value is
non-empty and matches the URL clients reach Heimdall on. See
[`server`](../configuration/server.md).

## Upstream timeouts / `/readyz` reports 503

`UpstreamReadinessCheck` failed to reach the upstream `index.json`. Causes:

- **Network egress is blocked** — check that the container or the
  reverse proxy in front of it can reach `https://api.nuget.org` (or
  whichever upstream you configured).
- **Upstream incident** — visit
  [status.nuget.org](https://status.nuget.org) (or your upstream's
  equivalent).
- **TLS issues** — corporate CAs in the trust store; the container image
  uses the standard Microsoft .NET cert bundle.

`UseSerilogRequestLogging` emits warnings with the failing URL; check logs.

## Hot reload doesn't pick up my change

`config.yml` is watched by the .NET configuration provider. If your editor
saves via "rename and move", make sure the new file ends up at the same
inode that was being watched — most editors get this right. Container bind
mounts behave correctly because Docker re-syncs file events from the host.

If the change is invalid (validation rejects it), Heimdall logs the error
and keeps the previous snapshot. Check stdout — you will see a clear
validation failure.

## Cache hit ratio looks low

The L1 strand has a soft cap (`heimdall.cache.l1.maxEntries`). If churn
exceeds the cap, entries are evicted before they can be re-used. Either
raise `maxEntries`, increase `cacheTtl` on the hottest feeds, or add memory
to the pod.

A real distributed L2 cache will eventually fix multi-instance cache misses
— until Redis lands, treat each Heimdall instance as its own cache shard.

## `dotnet nuget add source` writes the wrong URL

If your `publicBaseUrl` was wrong when the client first added the source,
the cached service index in the client may still point at the upstream.
Remove the source and re-add it:

```sh
dotnet nuget remove source heimdall-strict
dotnet nuget add source http://localhost:8080/nuget/strict/v3/index.json -n heimdall-strict
```
