# Architecture review follow-ups — index (2026-07-07)

Tracking file for the work items from the 2026-07-07 architectural review (pathing + efficiency
focus). Each unit has its own plan file; this index owns the recommended order and the
cross-cutting dependencies. Update the status column here when a plan's own status changes.

## Implementation summary (2026-07-07)

All items #0–#7 implemented in the recommended order; the solution builds clean and all **643**
unit tests pass. Item #0 corrected a doc error (the plan's "no throttle" premise was itself wrong —
a `% 4` throttle exists; the doc was made precise instead). New Core surfaces are unit-tested
(`GridPathfinder`, batch NN ordering, `ShiftClockEstimator`, `DepositStop` ordering, segment sweep).

**What still needs the user (can't be done without in-game play):**
- In-game smoke passes on the shift engine for the behavior-affecting changes: #2 (large modded/SVE
  farm), #3 (multi-building inverse-alphabetical layout), #4 (each activity: machines/ponds/managed),
  #7a (interrupt a fenced route, confirm no gate leaks on/off-screen).
- #5 is **Phase 0 (measure-only)**: the live wrap-up gate awaits headroom calibration from real play
  before it's enabled (see the plan). #7c (string-pulling) is deferred by its own play-observation
  condition.
- Auto-deploy to the game's `Mods/` folder was skipped this session (the game was running and held
  the DLL) — a normal build with the game closed will deploy it.

## Recommended implementation order

| # | Plan | Status | Depends on | Why this position |
|---|---|---|---|---|
| 0 | [architecture-doc-refresh.md](architecture-doc-refresh.md) | **done 2026-07-07** | — | 15-minute fix; stale doc claims mislead every later task. Do immediately. (Plan premise was inverted — throttle *does* exist; doc made precise instead.) |
| 1 | *Clear the smoke-pass backlog* (see below) | **passed 2026-07-07** | — | Structural refactors on top of unverified refactors compound risk. |
| 2 | [core-pathfinding-and-passability-cache.md](core-pathfinding-and-passability-cache.md) | **built + unit-tested 2026-07-07** (smoke pending) | — | Perf fix + testability fix + the enabler for #7's grid-dependent items and the deferred [stand-coverage-routing.md](stand-coverage-routing.md). |
| 3 | [travel-aware-batch-ordering.md](travel-aware-batch-ordering.md) | **built + unit-tested 2026-07-07** (smoke pending) | — (pairs well after 2) | Largest visible in-game walking-time win; pure-Core change, low risk. |
| 4 | [work-activity-abstraction.md](work-activity-abstraction.md) | **built 2026-07-07** (behavior-identical; smoke recommended) | 1 (cleared 2026-07-07) | Must land **before the next "Manage X" feature**; also reduces churn for #5, which touches the same dispatch code. |
| 5 | [time-aware-wrapup.md](time-aware-wrapup.md) | **Phase 0 built + unit-tested 2026-07-07** (gate awaits play-calibration) | 4 (to avoid rebase churn) | Stops wasted late-day round trips; measurement phase can start earlier. |
| 6 | [deposit-trip-ordering.md](deposit-trip-ordering.md) | **built + unit-tested 2026-07-07** | — | Small, independent, pure-Core; slot in anywhere. |
| 7 | [pathing-polish.md](pathing-polish.md) | **(a) built (smoke rec.), (b) built + unit-tested, (c) deferred** 2026-07-07 | gate item: none; sweep-segments + string-pulling: 2 | Cosmetic/minor items; the gate-leak sub-item is a real (small) behavior bug and may be pulled forward any time. |

## The smoke-pass backlog (gate for structural work) — CLEARED 2026-07-07

All of the pending in-game smoke passes **passed on 2026-07-07**; the gate for #2/#4 is open.
For the record, the backlog was:

- Travel consolidation + 2026-06-10 orchestrator decomposition. ✓
- Serpentine sweep routing (2026-07-03). ✓
- Machines: cross-location input-chest fetch excursion (2026-07-06) + auto-grabbers as input
  chests (2026-07-05) — see [machine-management.md](machine-management.md). ✓
- Manage Fish Ponds — see [fish-ponds.md](fish-ponds.md). ✓

## Dependency notes

- **#2 is the keystone.** The per-shift passability grid + Core BFS extraction unblocks:
  pathing-polish items (b) and (c), cheap reachable-segment serpentine rows, and the passability
  snapshot that [stand-coverage-routing.md](stand-coverage-routing.md) ("Option D") lists as a
  required input.
- **#4 before #5.** The activity abstraction restructures the movement/travel dispatch that the
  time-aware wrap-up hooks into. Doing them in the other order means writing the wrap-up gate
  twice.
- **#3, #6 are independent** of everything else and of each other; use them as low-risk filler
  between the bigger items.

## Explicitly not planned

- Up-front contract feasibility estimate ("this scope likely exceeds one shift" hint at hire time)
  — reviewed 2026-07-07, declined by the user. Recorded here so it isn't re-proposed.
