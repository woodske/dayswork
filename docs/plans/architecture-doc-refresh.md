# Plan — Architecture doc refresh (tick-throttle drift)

**Status:** DONE 2026-07-07. Item #0 in
[architecture-review-index.md](architecture-review-index.md).

## Correction — the plan's original premise was wrong

The review claimed `docs/architecture.md`'s "throttled to every 4th tick" was stale drift and that
"no such throttle exists". **On inspection the throttle *does* exist.**
`ShiftOrchestrator.OnUpdateTicked` (`ShiftOrchestrator.cs:874`) has an explicit
`if (++Session.TickCount % 4 != 0) return; // tick throttle`, backed by `ShiftSession.TickCount`.
The reviewer grepped for SMAPI's `IsMultipleOf` helper and missed the raw `% 4` modulo — so the
"no `IsMultipleOf` anywhere" observation is technically true but led to the wrong conclusion.

What is actually true (verified against the code 2026-07-07):

- **Every tick** (before the throttle): `_toolAnimator.Update`, `_nav.Update()` (the worker walks
  smoothly pixel-by-pixel), and `ProcessPendingDebrisSweeps()`.
- **Every 4th tick** (after the `% 4` early-return): progress sampling / stuck detection, hit
  reaction, the shopping/idle wait loops, travel handling, and the intent-dispatch `switch`.

So the doc's original claim was substantively correct; it was merely *imprecise* about the split
between the per-tick movement/animation path and the throttled intent dispatch.

## Change (as applied)

1. `docs/architecture.md` "Shift execution loop" now spells out both rates explicitly: tool
   animation + movement driver + debris pump run every tick; intent dispatch is throttled to every
   4th tick via `Session.TickCount % 4` (noting it's a raw modulo, not `IsMultipleOf`, so future
   greps don't repeat the miss). Gate on `Game1.shouldTimePass(false)` also recorded.
2. Spot-checked adjacent claims: the `ShiftPhase` list (`WaitingForSpawn → Working → Stuck →
   Recovering → Depositing → Exiting → Done`) matches `Dayswork.Core/Shifts/ShiftPhase.cs`; spawn
   flow and stop-condition claims match the code. No other drift found.
3. `AGENTS.md` does not repeat the throttle statement (it defers to architecture.md) — no change
   needed.

## Downstream impact on the other plans

- **#5 (time-aware wrap-up):** "walk speed is per-tick" is confirmed correct — `_nav.Update()` runs
  every tick. The tick→minute conversion still needs its own verification, but the per-tick walk
  assumption is sound.
- **#2 (passability cache):** the BFS work-selection runs on the throttled (every-4th) ticks, not
  literally every tick. The perf argument is unchanged — each *selection* still pays the full
  whole-map probe; throttling only caps selection frequency, it doesn't cheapen a selection.

## Testing

None beyond a re-read — docs only.
