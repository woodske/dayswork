# Plan — Time-aware wrap-up (don't start far work near the cap)

**Status:** proposed 2026-07-07, not started. Item #5 in
[architecture-review-index.md](architecture-review-index.md). Sequence after
[work-activity-abstraction.md](work-activity-abstraction.md) — both touch the same dispatch/gate
code. The measurement phase (DevLog only) can start any time.

## Problem

`ShouldWrapUpBeforeNextUnit` gates on energy and a pending stop reason, never on the clock. The
worker will start travel to a distant shed at 7:55pm, arrive at ~7:59, and get pulled home by the
8pm hard cap — a wasted round trip that burns real minutes of the player's evening watching it.
Travel consumes clock but not energy, so nothing in the energy ledger prevents this.

## Design sketch

### Core: `ShiftClockEstimator` (pure, unit-tested)

Inputs: current `timeOfDay`, the hard cap (8pm = 2000), walk speed (pixels/tick from the pacing
profile), and a route length in tiles. Output: estimated in-game minutes for the walk, and a
`bool ShouldStart(...)` that reserves headroom for *arrive + at least one work beat + the walk
home*.

Conversion chain (each constant **verified against the decompile before use — hard rule 7**, then
recorded in `docs/`):

- ticks per tile = 64 px / `WalkPixelsPerTick`;
- in-game minutes per tick from the game's 10-minute interval length (vanilla default 7000 ms ≈
  420 ticks per 10 in-game minutes at 60 tps — **verify**, including whether location/weather
  modifiers apply).

Estimates are deliberately coarse; round pessimistically.

### Gate points (v1 — coarse-grained only)

1. **Batch-entry travel** (`WorkEntry` in `ShiftOrchestrator.Travel.cs`): before starting, estimate
   travel legs (tiles are known from the plan; cross-location legs get a per-hop constant) + walk
   home from the destination. If it doesn't fit before the cap ⇒ `QueueWrapUpNow` with a new
   `ShiftStopReason` (e.g. `DayEndingSoon`) so the HUD/log can say *why* the worker went home early.
2. **Idle-loop re-entry** (`TryEnterIdleLoop` / `ContinueIdleWaitTick`): don't leave the door for a
   machine round that can't complete a single service + return before the cap.

Explicitly **not** gating per work item within a batch in v1 — intra-batch hops are short and the
check would fire constantly for no benefit.

### Phase 0 — measure first

Ship the estimator wired to DevLog only: log `would-have-skipped` decisions with the estimate and
the actual outcome (did the 8pm cap truncate the batch?). One or two play-days of data calibrates
the headroom constant before the gate goes live. Cheap insurance against an over-eager worker who
goes home at 6pm.

## What must NOT change

- The 8pm hard cap (`TimeChanged`) stays exactly as-is — this gate is an optimization in front of
  the backstop, never a replacement.
- Energy accounting and pricing are untouched: skipping a late batch spends nothing, refunds
  nothing.
- Wrap-up still routes through the normal deposit path (items are never lost).
- A `WarpToDestination`-policy travel already in flight is never interrupted by the clock gate.

## Testing

- Core: unit tests on the estimator (conversion math, headroom boundary cases, degenerate
  inputs: zero-length route, already-past-cap).
- In-game: contrive a save with a far shed batch late in the plan; confirm the worker wraps up
  instead of starting the trip, with the correct HUD/stop reason; confirm normal early-day
  behavior is unchanged.

## Open questions

- Config-exposed headroom (GMCM slider "quit early when the day is nearly over") or a fixed
  constant? Leaning fixed until someone asks.
- Should deposit-trip *starts* also be gated, or does the terminal deposit's existing overflow
  safety make that pointless? (Leaning: pointless — deposits must run regardless; overflow already
  handles truncation.)
