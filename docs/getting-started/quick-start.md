---
sidebar_position: 2
---

# Quick start

This walks through running Heimdall locally, pointing a `dotnet nuget` client
at it, and watching a recent version disappear from the listing.

## 1. Run Heimdall

The simplest configuration declares one upstream NuGet feed with a 14-day
minimum-age rule and an allow-list. Create `config.yml`:

```yaml
heimdall:
  server:
    listen: "http://0.0.0.0:8080"
    publicBaseUrl: "http://localhost:8080"
  ecosystems:
    nuget:
      feeds:
        - name: strict
          upstream: "https://api.nuget.org/v3/index.json"
          cacheTtl: "00:10:00"
          rules:
            - type: minAgeDays
              days: "14"
            - type: allowDeny
              patterns: "Microsoft.*;System.*;Newtonsoft.*;!Internal.*"
```

Then either run from source:

```sh
dotnet run --project src/Heimdall.Api
```

Or run the container image:

```sh
docker run --rm -p 8080:8080 -v $(pwd)/config.yml:/app/config.yml \
  ghcr.io/markeli/heimdall:latest
```

`publicBaseUrl` is what Heimdall advertises in NuGet `@id` fields, so set it to
the address clients reach Heimdall on. See
[`server`](../configuration/server.md) for details.

## 2. Wire up a client

```sh
dotnet nuget add source http://localhost:8080/nuget/strict/v3/index.json \
  -n heimdall-strict
```

You now have a NuGet source named `heimdall-strict` pointing at the `strict`
feed configured above. Try installing a stable, allowed package:

```sh
dotnet new console -o demo
cd demo
dotnet add package Newtonsoft.Json --source heimdall-strict
```

The install succeeds and the `.nupkg` is streamed through Heimdall. The
metadata cache is populated for the configured TTL.

## 3. See a rule fire

Try to pull a package that does not match the allow-list:

```sh
dotnet add package Some.Internal.Lib --source heimdall-strict
```

The flat-container versions list returns 200 with an empty `versions` array
(filtered), so `dotnet` reports the package as unfound. If the client requests
the binary directly, Heimdall returns `403 ProblemDetails` and the body names
the rule:

```sh
curl -sS -o /dev/null -w '%{http_code}\n' \
  http://localhost:8080/nuget/strict/v3/flatcontainer/some.internal.lib/1.0.0/some.internal.lib.1.0.0.nupkg
# 403
```

Use `-D -` to inspect the body — it includes `ruleName` and a human-readable
`message`.

## 4. Try the age gate

Edit `config.yml` and bump `minAgeDays` to a large number (say `9999`).
The file is hot-reloaded; no restart needed (see [Configuration
overview](../configuration/overview.md)). The next request to the versions
list will return an empty array because every published version is now too
young.

You're ready to move on to the [configuration
reference](../configuration/overview.md) or [filtering
rules](../rules/overview.md).
