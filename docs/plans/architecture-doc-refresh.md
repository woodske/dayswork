# Plan — Architecture doc refresh (tick-throttle drift)

**Status:** proposed 2026-07-07, not started. Item #0 in
[architecture-review-index.md](architecture-review-index.md) — trivial; do immediately.

## Problem

`docs/architecture.md` ("Shift execution loop") says the orchestrator dispatch runs
"Per `UpdateTicked` (**throttled to every 4th tick**)". No such throttle exists:
`ShiftOrchestrator.OnUpdateTicked` runs `_toolAnimator.Update`, `_nav.Update()`, and the intent
dispatch **every tick** — there is no `IsMultipleOf` anywhere in `Dayswork/` (verified 2026-07-07;
the only modulo pacing is the ×30 emote re-fire in the idle/shop wait loops).

Pacing assumptions read differently depending on which is true (walk px/tick, wait-tick counters,
the cost analysis in the passability-cache plan), so the doc must match the code.

## Change

1. Fix the sentence in `docs/architecture.md` to say the dispatch runs every tick, gated on
   `Game1.shouldTimePass(false)` (which it is).
2. While in there, spot-check the adjacent claims in the same section against the code (spawn
   flow, phase list, stop conditions) — 15 minutes, no deep audit. Anything else stale gets fixed
   in the same commit.
3. Check `AGENTS.md` for the same claim (it defers to architecture.md for the shift loop; confirm
   it doesn't repeat the throttle statement).

Optional follow-up (out of scope here, noted for the record): if per-tick dispatch is ever deemed
too hot, *introducing* a real throttle is a behavior change with pacing implications
(walk speed is per-tick) — that would be its own change, not a doc fix.

## Testing

None beyond a re-read — docs only.
