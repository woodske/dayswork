# Archived: Time-aware wrap-up (parked, not completed)

Item **#5** of the 2026-07-07 architecture review — stop the worker starting a trip it cannot
finish before the 8pm hard cap. **The headline deliverable was never built.** Only the measure-only
Phase 0 (an estimator plus a DevLog line) shipped, on 2026-07-07; the live skip-gate was never
enabled. Parked by decision on **2026-09-07** — not abandoned as a bad idea, just unscheduled.

Original plan: `time-aware-wrapup.md`.
Game-content reference: [`docs/game-data/time-and-pacing.md`](../../game-data/time-and-pacing.md) —
the verified clock constants the estimator is built on.

> Unlike the other artifacts in this folder, this one **keeps the design sketch**. The archive
> normally drops the approach because the code is the record of what was built; here there is no
> such code, so the sketch is the only record. It is preserved verbatim in intent so the plan can be
> resumed rather than re-derived.

---

## Why the work was proposed

`ShouldWrapUpBeforeNextUnit` gates on energy and on a pending stop reason, **never on the clock**.
So the worker will start walking to a distant shed at 7:55pm, arrive around 7:59, and be yanked home
by the 8pm hard cap — a wasted round trip. Travel burns clock but not energy, so nothing in the
energy ledger prevents it, and the player watches it happen in real minutes.

## What actually shipped (2026-07-07) — still live in the tree

This code is **in main today** and is not dead weight to strip; it is the resume point.

- **`Dayswork.Core/Shifts/ShiftClockEstimator.cs`** (pure): `EstimateWalkMinutes(tiles,
  walkPixelsPerTick)`, `AddInGameMinutes`, `FitsBeforeCap`. Rounds pessimistically; ignores
  location slow-down (errs safe). Conversion constants verified against the decompile per hard
  rule 7 and recorded in `docs/game-data/time-and-pacing.md`.
- **`Dayswork.Tests/Shifts/ShiftClockEstimatorTests.cs`** — conversion, ceil, hour rollover,
  early/late/degenerate/boundary fit cases.
- **`ShiftStopReason.DayEndingSoon`** — declared, distinct from `HardCap`, and **passed to
  `QueueWrapUpNow` nowhere**. No exhaustive switch consumes the enum, so it is inert.
- **`ShiftOrchestrator.MeasureWrapUpFit`** (`ShiftOrchestrator.Travel.cs`) — logs one
  `[Dayswork][wrapup-measure]` line per `WorkEntry` travel start with the estimate and the
  would-skip decision, using the passability cache for the outbound leg and Manhattan for the
  homebound leg. Gated by `DevLog.Enabled` (absent from release) and **behavior-neutral**.
- **`WrapUpWorkHeadroomMinutes = 10`** — a provisional constant that was never calibrated.

## Why it stopped where it did

Phase 0 was deliberate insurance, not a shortcut: the failure mode of an uncalibrated gate is a
worker who quits at 6pm, which is worse than the wasted trip it prevents. The plan required a
play-day or two of `[Dayswork][wrapup-measure]` lines to confirm `would-skip` fires only for
genuinely-doomed late trips before the headroom constant could be trusted. **That play data was
never collected**, so the gate never had grounds to go live. Nothing was found wrong with the
design; it simply never cleared its measurement gate.

## What was decided

- **Coarse-grained gating only.** Two gate points: batch-entry travel (`WorkEntry` in
  `ShiftOrchestrator.Travel.cs`) and idle-loop re-entry (`TryEnterIdleLoop` /
  `ContinueIdleWaitTick`, so the worker doesn't leave the door for a machine round it can't
  complete). Explicitly **not** per work item within a batch — intra-batch hops are short and the
  check would fire constantly for no benefit.
- **The gate sits in front of the 8pm hard cap, never replaces it.** `TimeChanged` stays exactly
  as-is; this is an optimization ahead of a backstop that must survive it being wrong.
- **A distinct stop reason.** `DayEndingSoon` rather than reusing `HardCap`, so the HUD and log can
  say *why* the worker went home early.
- **Estimates round pessimistically** and ignore location slow-down — both errors point the safe
  direction (over-estimating the trip makes the worker more willing to go, not less).
- **Measure before gating.** Ship the estimator wired to DevLog first, calibrate, then replace the
  log with the gate.

## What must not change if this is resumed

- The 8pm hard cap (`TimeChanged`) stays as-is.
- Energy accounting and pricing are untouched — skipping a late batch spends nothing and refunds
  nothing.
- Wrap-up still routes through the normal deposit path (hard rule 4: items are never lost).
- A `WarpToDestination`-policy travel already in flight is never interrupted by the clock gate.

## Open questions, unresolved

- Config-exposed headroom (a GMCM "quit early when the day is nearly over" slider) or a fixed
  constant? Leaning fixed until someone asks.
- Should deposit-trip *starts* be gated too? Leaning no — deposits must run regardless, and the
  terminal deposit's overflow safety already handles truncation.

## To resume

Unchanged from where it stopped, and none of it is a build task first:

1. Play with `DevLog.Enabled` and collect `[Dayswork][wrapup-measure]` lines over a day or two;
   confirm `would-skip` fires only for genuinely-doomed late trips, not at 6pm.
2. Calibrate `WrapUpWorkHeadroomMinutes` from that data.
3. Replace the measurement log with the real gate: on `!fits`, `QueueWrapUpNow(DayEndingSoon)` at
   the `WorkEntry` point and the idle-loop re-entry point, plus HUD/stop-reason surfacing.
4. In-game check: contrive a save with a far shed batch late in the plan; confirm the worker wraps
   up instead of starting the trip, and that normal early-day behavior is unchanged.

Verify the tree still matches this description before trusting it:
`grep -rn "MeasureWrapUpFit" Dayswork/Orchestration/` (Phase 0 present) and
`grep -rn "DayEndingSoon" Dayswork/` (no hits ⇒ the gate is still not live).
