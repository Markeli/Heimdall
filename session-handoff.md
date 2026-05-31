# Session Handoff

Fill this in **before ending a session** with work in flight, so the next
session restarts cold without guessing. Overwrite it each handoff; the durable
history lives in `progress.md`, `CHANGELOG.md`, and git.

> **Last Updated:** 2026-05-31
> **Current Objective:** None active — harness just bootstrapped. NuGet MVP is
> shipped; no feature is `in_progress`.
> **Recommended Next Step:** Activate one planned feature from
> `feature_list.json`, then follow the Startup Workflow in `AGENTS.md`.

## Active feature

- **Feature id:** _none_ (`activeFeatureId` is `null` in `feature_list.json`)
- **Issue:** _n/a_
- **Branch:** _n/a — none started_

## Current State

What is true right now: which step of the feature is done, which is not.
(Empty — no feature in progress.)

## Blockers

Anything stopping forward progress: failing test, unclear requirement, missing
access, decision needed from the maintainer. _(none)_

## Files

Files touched or in the middle of an edit this session, and why each matters:

- `AGENTS.md`, `feature_list.json`, `progress.md`, `init.sh`,
  `session-handoff.md` — harness scaffolding added; no application code
  touched.

## Next Session

The single first action to take, concrete enough to start without re-reading
the whole conversation:

1. Read `AGENTS.md` → "Startup Workflow".
2. Pick one planned feature in `feature_list.json`, set it `in_progress` and
   set `activeFeatureId`.
3. `git fetch origin main && git checkout -b <type>/<slug> origin/main`.
4. Write the failing test first, then implement.
5. `./init.sh` must be green before any "done" claim.
