# `spec/` — draft research for future Heimdall ecosystems

This directory holds **drafts**, not contracts. Each document is a concept
study of one upstream package ecosystem, written to answer one question:
*can Heimdall's filtering model — `minAgeDays` + `allowDeny`, with a
download gate that returns `403 ProblemDetails` — be applied to this
ecosystem cleanly?*

The MVP (NuGet v3) is the reference architecture. Where another
ecosystem requires a meaningfully different shape, the difference is
called out explicitly.

## Status

| Document | Status | Tracking issue |
|---|---|---|
| [ecosystems-overview.md](ecosystems-overview.md) | draft | [#8](https://github.com/Markeli/Heimdall/issues/8) |
| [npm.md](npm.md) | draft | [#11](https://github.com/Markeli/Heimdall/issues/11) |
| [python.md](python.md) | draft | [#12](https://github.com/Markeli/Heimdall/issues/12) |
| [go.md](go.md) | draft | [#13](https://github.com/Markeli/Heimdall/issues/13) |
| [maven.md](maven.md) | draft | [#14](https://github.com/Markeli/Heimdall/issues/14) |

A "draft" here means: the protocol surface is mapped, the filtering
strategy is sketched, the bypass surface is enumerated. It does **not**
mean the design is approved or that implementation can start. Each
ecosystem will get its own implementation task with a fresh design
review at that point.

## How to read these documents

Every per-ecosystem document follows the same outline. Some
ecosystems insert one extra section for an ecosystem-specific
concern that does not fit the canonical layout
([go.md](go.md) §7 covers the checksum database;
[maven.md](maven.md) §7 covers integrity verification). The outline
is otherwise:

1. **Goal & non-goals.** What this ecosystem brings into Heimdall, and
   what is explicitly out.
2. **Protocol surface.** Endpoints the proxy must answer.
3. **`minAgeDays`: where the timestamp lives.** The single most
   ecosystem-specific question. NuGet has it for free in
   `catalogEntry.published`; the others do not, to varying degrees.
4. **Filter integration points.** Listing-level filter (decorative)
   vs. download gate (enforcing). Both are needed; only the second is
   load-bearing.
5. **Status codes when a version is filtered.** Each ecosystem has its
   own client semantics; the answer is not always "403".
6. **Client behaviour on restore.** What the canonical CLI(s) do —
   parallelism, lockfile handling, fallback to upstream.
7. **Bypass surface.** Where the client can sidestep the proxy entirely
   (mirror chains, `direct` fallback, vendored sources, absolute URLs
   in lockfiles).
8. **Out of MVP.** Endpoints we deliberately do not proxy in v1 of that
   ecosystem.
9. **Open questions.** Decisions deferred to the implementation issue.
10. **Implementation sketch.** Project name, DI wiring, controllers —
    enough for the implementation issue to inherit a layout.
11. **Acceptance criteria.** What "MVP done" means for that ecosystem.

## Project structure assumed

The MVP's split (`Heimdall.Core` / `.Infrastructure` /
`.Ecosystems.NuGet` / `.Api`) is preserved. Each new ecosystem ships as
a new `Heimdall.Ecosystems.<Name>` project under `src/` plus controller
routes mounted under `/<name>/<feed>/...`. The filter pipeline
(`IRule`, `RuleEvaluator`, `VersionListFilter`, `SingleVersionGate`)
lives in `Heimdall.Core` and is reused unchanged. No rule code needs to
move; only ecosystem-specific projections and clients are added.
