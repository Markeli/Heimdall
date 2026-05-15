---
sidebar_position: 2
---

# `minAgeDays`

Rejects versions younger than a configured number of days. The intent is to
let real users encounter (and report) a compromised package on the public
ecosystem before your internal builds can pull it.

## Configuration

```yaml
- type: minAgeDays
  days: "14"
  exclude: "Mindbox.*;Quoka;Abc"
```

| Field | Type | Notes |
|---|---|---|
| `days` | integer (≥ 0) | Minimum required age in whole days. Strings are accepted because YAML scalars from the configuration provider are weakly typed. |
| `exclude` | string (optional) | `;`- or newline-separated list of glob patterns matched case-insensitively against the package ID. Matching packages bypass the age check (and the missing-`published`-date safeguard). Blank entries are ignored. Omit the field for no exclusions. |

`days: "0"` is legal — it disables the rule but keeps the slot, useful for
keeping config shapes stable between environments.

### `exclude` — bypassing the age check

`exclude` lists package IDs (with `*` / `?` glob wildcards, case-insensitive)
that are trusted to skip the age requirement. Use it for first-party
packages whose publishing pipeline you already control, or for vetted
third-party IDs where the freshness check is not meaningful. The pattern
syntax matches the `allowDeny` rule — `Mindbox.*` matches every package
whose ID starts with `Mindbox.`; bare names like `Quoka` are exact matches.

A multi-line form is equivalent and is often easier to read in YAML:

```yaml
- type: minAgeDays
  days: "14"
  exclude: |
    Mindbox.*
    Quoka
    Abc
```

Note that an excluded match short-circuits the rule before the
`published`-is-null safeguard runs — Heimdall trusts the exclusion list and
will allow a matching package even if its catalog entry has no `published`
timestamp.

## Semantics

A version is allowed when:

```
now - catalogEntry.published >= days
```

The reference `now` is the request time captured in `RuleContext.NowUtc`, so
evaluation is stable across all rules within a single request. The
`published` timestamp comes from the NuGet v3 catalog entry of the upstream
registration document.

### `published` is null

If the upstream registration entry has no `published` timestamp (some
unlisted versions, corrupted metadata), the rule **denies** the version.
This is a safeguard: Heimdall cannot prove the age requirement, so it errs
on the side of blocking.

## Deny reasons

```json
{
  "ruleName": "minAgeDays",
  "detail": "version published 0.3 days ago, requires 14"
}
```

The `detail` includes the actual age to one decimal place, so operators can
quickly see whether a request was a hair short of the threshold or arrived
on day zero.

When `published` is missing:

```json
{
  "ruleName": "minAgeDays",
  "detail": "published date is missing"
}
```

## Tuning

- **Production feeds**: 7–14 days is a common floor. Long enough to catch
  most malicious-release reports, short enough that legitimate consumers
  don't notice.
- **CI for security updates**: tighter, e.g. `1` day, on a separate feed
  reserved for `Microsoft.AspNetCore.*` or other vetted namespaces.
- **Hot reload**: bumping the threshold from `14` to `30` is picked up on
  the next request without a restart; the cached registration documents
  (which carry the unfiltered version list and `published` timestamps) are
  re-evaluated against the new threshold.

## What it does **not** do

- It does **not** consult any CVE/vulnerability database.
- It does **not** distinguish between prerelease and stable channels — the
  `published` timestamp is the only signal.
- It does **not** care about SemVer ordering — a freshly-published `1.0.0`
  is treated identically to a freshly-published `2.0.0`.
- `exclude` does **not** combine with the `allowDeny` rule — they are
  independent stages of the filter pipeline. To deny a package outright,
  add an `allowDeny` rule alongside `minAgeDays`.
