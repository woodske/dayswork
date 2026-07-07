# Time & pacing conversion

Verified game-time constants backing `Dayswork.Core/Shifts/ShiftClockEstimator.cs` (architecture
review #5, time-aware wrap-up). Confirmed against a decompile of `Stardew Valley.dll` on 2026-07-07
(hard rule 7).

## The clock

`StardewValley.Game1` advances the in-game clock in 10-minute steps:

- `realMilliSecondsPerGameMinute = 700` → `realMilliSecondsPerGameTenMinutes = 7000`.
- Each update tick (while `shouldTimePass`) does `gameTimeInterval += time.ElapsedGameTime.Milliseconds`.
- When `gameTimeInterval > realMilliSecondsPerGameTenMinutes + (currentLocation.ExtraMillisecondsPerInGameMinute * 10)`,
  `performTenMinuteClockUpdate()` fires (`timeOfDay += 10`) and resets `gameTimeInterval`.

So **one in-game minute ≈ 700 real ms**. A location can only *slow* time (a positive
`ExtraMillisecondsPerInGameMinute` raises the threshold); nothing speeds it up. The farm's value is
0, and for a "will this trip fit before 8pm" gate, ignoring the slow-down term is the **safe**
(pessimistic) direction — the real trip finishes no later than the estimate.

## Ticks

MonoGame runs a fixed timestep at 60 updates/second, so `ElapsedGameTime.Milliseconds ≈ 16.667`
(1000/60) per tick. Therefore:

- ticks per in-game minute ≈ 700 / 16.667 ≈ **42**;
- ticks per 10 in-game minutes ≈ **420**.

The worker's movement driver (`WorkerMovementDriver.Update`) runs **every** tick (before the
`% 4` intent-dispatch throttle — see `docs/architecture.md`), so walk speed is genuinely
per-tick: `WorkPixelsPerTick` pixels/tick.

## Walk-time estimate

For a route of `N` tiles at `walkPixelsPerTick`:

```
ticks   = N * 64 / walkPixelsPerTick
minutes = ticks * (1000/60) / 700         // pessimistic ceil
```

Worked example (default 2 px/tick): 10 tiles → 320 ticks → ≈7.62 → **8 in-game minutes**.

`ShiftClockEstimator.FitsBeforeCap` adds an outbound estimate, a homebound estimate, and a
work-headroom reserve to `timeOfDay` and checks it lands at or before the 8pm cap (2000).

## Status

Phase 0 (measure-only) is wired: `ShiftOrchestrator.MeasureWrapUpFit` logs a
`[Dayswork][wrapup-measure]` line at each `WorkEntry` travel start (gated by `DevLog.Enabled`, so
absent from release; never changes behavior). The provisional headroom constant
(`WrapUpWorkHeadroomMinutes = 10`) and the live skip-gate (`ShiftStopReason.DayEndingSoon`) await
calibration from real play before being enabled — see `docs/plans/time-aware-wrapup.md`.
