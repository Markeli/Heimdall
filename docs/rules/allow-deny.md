---
sidebar_position: 3
---

# `allowDeny`

Glob-based allow / deny on the **package identifier** (not the version).
Deny matches always win.

## Configuration

```yaml
- type: allowDeny
  patterns: "Microsoft.*;System.*;Newtonsoft.*;!*.Internal"
```

The patterns string is a semicolon-separated list. Blank entries are
ignored. Each entry is either an **allow** pattern or, when prefixed with
`!`, a **deny** pattern. Whitespace around individual patterns is trimmed.

## Glob syntax

- `*` matches zero or more characters.
- `?` matches exactly one character.
- All other characters match literally; regex metacharacters in package IDs
  are matched as-is.
- Matching is **case-insensitive**, matching NuGet's id case-folding.

Internally each glob is compiled to a `Regex` with a 50 ms timeout — long
enough for any sane id, short enough to block pathological patterns.

## Semantics

The rule walks the patterns once per version and applies these rules in
order:

1. **Deny wins.** If any deny pattern matches, the version is rejected
   immediately. Allow patterns are not consulted.
2. **Deny-only configuration.** If there are no allow patterns, every
   non-denied version is allowed.
3. **Allow-list mode.** If at least one allow pattern is present, the
   version must match at least one allow pattern; otherwise it is rejected.

### Worked examples

```yaml
patterns: "Microsoft.*;System.*"
```
Allow list. Only IDs starting with `Microsoft.` or `System.` pass.

```yaml
patterns: "!Internal.*"
```
Deny only. Everything passes except IDs starting with `Internal.`.

```yaml
patterns: "Microsoft.*;System.*;!Microsoft.Internal.*"
```
Allow `Microsoft.*` and `System.*`, but reject `Microsoft.Internal.*`. Deny
wins, so `Microsoft.Internal.Tools` is rejected even though it matches the
`Microsoft.*` allow pattern.

```yaml
patterns: ""
```
Empty — the rule allows everything. Equivalent to omitting the rule
entirely.

## Deny reasons

When an allow-list excludes a version:

```json
{
  "ruleName": "allowDeny",
  "detail": "package does not match any allow pattern"
}
```

When a deny pattern matches:

```json
{
  "ruleName": "allowDeny",
  "detail": "package matches deny pattern '!Internal.*'"
}
```

## Errors at config time

A pattern of just `!` is rejected at startup:

```
ArgumentException: deny pattern cannot be empty after '!'
```

Empty / blank patterns are silently dropped, so list trimming is forgiving.

## What it does **not** do

- It matches on the **id**, not the version string. To gate by SemVer
  range, layer this rule with a future version-bound rule, or combine with
  [`minAgeDays`](min-age-days.md).
- It is not a CVE filter — `allowDeny` says nothing about safety, only
  about whether the id is on your list.
