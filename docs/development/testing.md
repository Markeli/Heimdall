---
sidebar_position: 2
---

# Testing

Two test projects, one fast and one realistic. Every change that affects
behaviour ships with a test — see
[`CONTRIBUTING.md` §7](https://github.com/Markeli/Heimdall/blob/main/CONTRIBUTING.md#7-tests)
for the contract.

## `Heimdall.UnitTests`

Pure logic. No HTTP, no clock-of-the-wall, no filesystem.

- **Rules** — `MinAgeDaysRule`, `AllowDenyRule`, `GlobMatcher`,
  `RuleEvaluator`. Time-sensitive rules take `NowUtc` from
  `RuleContext`, so tests pass a fixed `DateTimeOffset` and assert
  deterministically.
- **Filters** — `VersionListFilter`, `SingleVersionGate`.
- **Cache plumbing** — `ConfigSnapshotProvider`, `FeedConfigLookup`,
  `ConfigGeneration`.
- **Transformers** — `NuGetV3MetadataTransformer`, `NuGetV3UrlRewriter`,
  `NuGetV3MetadataProjection`.

Run:

```sh
dotnet test tests/Heimdall.UnitTests
```

These tests run in milliseconds and are the right place to grow coverage
when fixing a logic bug.

## `Heimdall.IntegrationTests`

End-to-end controller flows over a real `WebApplicationFactory<Program>`
hosting Heimdall in-process, with
[WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) standing in
for the upstream NuGet feed.

Covered scenarios include:

- Service index returns Heimdall-rewritten URLs.
- Versions list reflects the configured filter rules (allow / deny by
  `minAgeDays` and `allowDeny`).
- Download gate allows on a clean version, returns `403 ProblemDetails`
  with the right `ruleName` on a denied one.
- `/healthz` returns 200; `/readyz` reflects upstream reachability.
- Configuration hot-reload — write to the config file mid-test, observe
  the new behaviour on the next request.

Run:

```sh
dotnet test tests/Heimdall.IntegrationTests
```

These are slower (hundreds of ms each) but catch wiring bugs that pure
logic tests cannot. The full suite still finishes in seconds.

## `Heimdall.SmokeTests`

Black-box checks against a **real, running Heimdall** talking to the **live**
`api.nuget.org` — the last line of defence that the shipped container actually
proxies and filters. Unlike the integration suite, there is no WireMock here:
the assertions hit the network.

The suite is intentionally **kept out of `Heimdall.sln`**, so
`dotnet cake --target=Test` does *not* run it — it only runs against a real
container in the `release` pipeline. The tests target the URL in
`HEIMDALL_SMOKE_BASEURL` (default `http://localhost:8080`) and exercise the
feeds defined in
[`tests/Heimdall.SmokeTests/config.smoke.yml`](https://github.com/Markeli/Heimdall/blob/main/tests/Heimdall.SmokeTests/config.smoke.yml):

- `relaxed` (`minAgeDays: 1`) — read-path: service index, listing, registration,
  download all work end-to-end.
- `allow-newtonsoft` (`allowDeny: Newtonsoft.*`) — allow-list admits
  `Newtonsoft.*`, blocks an unrelated well-aged package (Dapper).
- `deny-newtonsoft` (`allowDeny: !Newtonsoft.*`) — deny-pattern rejects
  Newtonsoft, lets others through.
- `age-locked` — `minAgeDays` is computed at run time from a real publication
  anchor (Newtonsoft.Json 12.0.3) so the rule deterministically blocks that
  version and every younger one.

**When it runs.** In the `release` workflow only: it builds the image, renders
the dynamic age threshold into the smoke config, runs the container, executes
the suite against it, and — as a higher-fidelity check — does a real
`dotnet restore` of [`samples/nuget-consumer`](https://github.com/Markeli/Heimdall/tree/main/samples/nuget-consumer)
through Heimdall. The image is pushed to GHCR **only after smoke passes**.

**Run it locally** against a container you started yourself:

```sh
docker run --rm -p 8080:8080 \
  -v $(pwd)/tests/Heimdall.SmokeTests/config.smoke.yml:/app/config.yml heimdall:dev
# in another shell, with the container up:
dotnet test tests/Heimdall.SmokeTests/Heimdall.SmokeTests.csproj
```

(The `age-locked` feed uses an `${AGE_LOCK_DAYS}` placeholder the release
pipeline substitutes; locally either render it or skip that feed's assertions.)

## Patterns to follow

- **Time** — never read `DateTime.UtcNow` in a rule. Use the `NowUtc`
  field of `RuleContext` so tests can pin the clock.
- **Network** — never hit real upstreams. All HTTP in integration tests
  goes through WireMock.Net mocks declared in the test setup.
- **Config** — integration tests run with a self-contained `config.yml`
  in `TestContext` workspace, so they can mutate it to exercise
  hot-reload.

## What gets run in CI

`ci/build` runs:

```sh
dotnet cake --target=Test --configuration=Release
```

which is exactly the same command you should run locally before pushing
(see [Building](building.md)). There are no test-isolation differences
between local and CI — if it passes one place it passes the other.

## When fixing a bug

Per `CONTRIBUTING.md`, add the failing test first, watch it fail, then
write the fix. Commit them together; reviewers should be able to see both
in the same diff.
