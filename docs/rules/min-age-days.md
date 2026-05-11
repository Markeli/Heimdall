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
```

| Field | Type | Notes |
|---|---|---|
| `days` | integer (≥ 0) | Minimum required age in whole days. Strings are accepted because YAML scalars from the configuration provider are weakly typed. |

`days: "0"` is legal — it disables the rule but keeps the slot, useful for
keeping config shapes stable between environments.

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
