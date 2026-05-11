---
sidebar_position: 1
---

# Building

Heimdall builds through [Cake](https://cakebuild.net/). Local invocations
and the [Dockerfile](https://github.com/Markeli/Heimdall/blob/main/src/Heimdall.Api/Dockerfile)
call the same `build.cake` so artifacts produced anywhere are
bit-identical.

## Prerequisites

- .NET 10 SDK (matches [`global.json`](https://github.com/Markeli/Heimdall/blob/main/global.json)).
- `dotnet tool restore` to install Cake on first checkout.

## Cake targets

| Target | Does | Use it when |
|---|---|---|
| `Restore` | `dotnet restore` for `Heimdall.sln`. | Rarely directly; other targets depend on it. |
| `Build` | Builds the solution. | Compile check. |
| `Test` | Runs every test project in the solution. | Before opening a PR — CI runs this exact command. |
| `Publish` | Publishes `Heimdall.Api` to `./artifacts/publish` (configurable via `--output`). | When you need a runnable bundle. The Dockerfile uses this. |

All targets accept `--configuration=Release` (default for CI) or
`--configuration=Debug` (default for `Build`). Cake uses MSBuild
incremental compilation; passing `NoBuild = true` between `Build` and
`Test` keeps the second pass cheap.

## Typical local loop

```sh
dotnet tool restore
dotnet cake --target=Test --configuration=Release
```

For an iterative inner loop without Cake:

```sh
dotnet build
dotnet test
dotnet run --project src/Heimdall.Api
```

The Cake script is what CI invokes — keep your inner loop with vanilla
`dotnet` if you prefer, then run Cake once before pushing.

## Publish

```sh
dotnet cake --target=Publish
ls artifacts/publish
```

Produces a framework-dependent .NET 10 publish under
`./artifacts/publish/`. `UseAppHost=false` is set so the output has no
native launcher — the image runs `dotnet Heimdall.Api.dll` directly.

## Container build

The Dockerfile is a thin shim around Cake:

```sh
docker build -f src/Heimdall.Api/Dockerfile -t heimdall:dev .
docker run --rm -p 8080:8080 -v $(pwd)/config.yml:/app/config.yml heimdall:dev
```

It copies `global.json`, NuGet config, `Directory.*` files, the Cake
script, the solution, and `src/`, then runs
`dotnet cake --target=Publish --output=/app --configuration=Release`
inside the SDK image. The runtime stage is `mcr.microsoft.com/dotnet/aspnet:10.0`.

## Releasing

Releases are tag-driven. See [`CONTRIBUTING.md` §5
"Releasing"](https://github.com/Markeli/Heimdall/blob/main/CONTRIBUTING.md#5-releasing)
for the procedure — that is the canonical reference and is intentionally
not duplicated here.

In short: promote `[Unreleased]` in `CHANGELOG.md`, merge a release PR, tag
the merge commit `vX.Y.Z` on `main`, push. The `release` workflow does the
rest (re-runs tests, builds and pushes the GHCR image, creates the GitHub
Release with notes extracted from `CHANGELOG.md`).
