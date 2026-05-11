---
sidebar_position: 1
---

# Installation

There are two supported ways to run Heimdall: pull the container image
published on every tagged release, or build it from source.

## Container image (recommended)

The release workflow pushes two tags to GitHub Container Registry on every
`vX.Y.Z` Git tag:

```sh
docker pull ghcr.io/markeli/heimdall:latest
docker pull ghcr.io/markeli/heimdall:0.1.0
```

The image listens on port `8080` and looks for `config.yml` next to
`Heimdall.Api.dll` at `/app/`. Mount one in:

```sh
docker run --rm -p 8080:8080 \
  -v $(pwd)/config.yml:/app/config.yml \
  ghcr.io/markeli/heimdall:latest
```

You should see Heimdall log a startup line and accept connections on
`http://localhost:8080`. Probe it with:

```sh
curl -fsSL http://localhost:8080/healthz
# ok
```

If your environment requires explicit listen settings, override them via
configuration ([`heimdall.server.listen`](../configuration/server.md)) or
environment variables (`HEIMDALL_HEIMDALL__SERVER__LISTEN=...`); see
[Configuration overview](../configuration/overview.md).

## From source

You need the .NET 10 SDK (matches `global.json`) and a checkout of the repo.

```sh
git clone https://github.com/Markeli/Heimdall.git
cd Heimdall
dotnet tool restore
dotnet cake --target=Test
dotnet run --project src/Heimdall.Api
```

The dev server binds to `http://localhost:8080` and loads
`src/Heimdall.Api/config.yml`. See [Building](../development/building.md) for
the Cake targets and [Configuration overview](../configuration/overview.md)
for the layered config files.

## What you should see

The first request to the service index returns Heimdall-rewritten URLs:

```sh
curl -fsSL http://localhost:8080/nuget/strict/v3/index.json | jq '.resources[].type'
"RegistrationsBaseUrl/3.6.0"
"PackageBaseAddress/3.0.0"
"SearchQueryService"
```

Every `@id` in that document points back at Heimdall — clients never address
the upstream directly. From here, head over to the [quick
start](quick-start.md) to wire up a NuGet client.
