# U-10 — NFR Requirements

## Performance

### PERF-U10-01 — UpdateTicked throttle (N1: B)
The `ShiftOrchestrator` subscribes to `UpdateTicked` but only executes its logic every **4 ticks** (~15 Hz at 60fps). Non-target ticks are skipped with an early return and a modulo check on the tick counter.
- Per-frame cost on skipped ticks: one integer modulo comparison — negligible.
- Detection latency for tile arrival: ≤ 67ms. Imperceptible to the player.
*Satisfies NFR-PERF-01 (worker update < 1ms per frame).*

### PERF-U10-02 — Work list built once
The work list (building pre-pass + nearest-first open-farm sort) is built exactly once at shift start, on the tick that processes `DayStarted`. No re-scanning during the shift.
- Worst-case input: Standard Farm (~5,200 open tiles), ~10% with tasks = ~520 WorkItems. One O(n log n) sort of 520 items ≈ microseconds. Acceptable as a one-time cost.
*Satisfies NFR-PERF-02 (tile scanning once per zone entry, not per frame).*

### PERF-U10-03 — Task action: invoke + poll (N2: B)
When performing a task at a tile, the orchestrator calls the game's tool-use API once, then holds the `IntentPerformTaskAt` intent and polls (every 4 ticks) for the target object's removal from the game world. The intent advances to the next WorkItem only after the object is confirmed gone.
- Handles multi-hit objects (trees, boulders) correctly without tracking hit counts.
- Polling cost: one `GameLocation.getObjectAtTile()` call every 4 ticks — negligible.

---

## Safety & Data Integrity

### SAFE-U10-01 — No items lost (NFR-SAFE-01)
All items collected by the worker must reach the shipping bin. In U-10, the shipping bin has infinite capacity (FR-OUT-06), so `ItemBuffer.TakeAll()` empties completely into the bin — no overflow path exists.
Hay is the only exception: handled by BR-10 (silo attempt → discard; never enters the buffer).

### SAFE-U10-02 — No gold lost beyond billed hours (NFR-SAFE-02)
Refund is computed with integer arithmetic only:
```
refund = clamp(deposit − (hoursWorked × hourlyRate), 0, deposit)
```
The clamp prevents floating-point leakage. `hoursWorked` is derived from game-minute integers. All values fit in `int` for any realistic shift duration.

### SAFE-U10-03 — Deduplication guard (contract fires exactly once)
`RecurringContractScheduler` sets contract status to `Executed` before spawning the worker. If `DayStarted` fires more than once in a session (edge case), the status check prevents double-spawn.

---

## Reliability

### REL-U10-01 — Multiplayer guard in scheduler
`RecurringContractScheduler` must call `MultiplayerGuard.IsMultiplayer()` at the top of its `DayStarted` handler and no-op immediately if true. This is the third and final required callsite per the cross-cutting concern in [unit-of-work.md](../../../../inception/application-design/unit-of-work.md).

### REL-U10-02 — 8pm cap always transitions to Depositing
`TimeChanged` at 8pm must always transition the state machine to `Depositing`, even if the current `IntentPerformTaskAt` action is mid-execution. The partially-worked tile is abandoned cleanly; items already in the buffer are preserved.

### REL-U10-03 — Unreachable tile: skip and continue
If `PathFindController` cannot produce a path to a WorkItem's tile (FR-WORK-08: unreachable tiles silently skipped), the orchestrator detects the pathfinding failure and advances to the next WorkItem without error. The failed tile is marked complete and not retried.

---

## Property-Based Testing Obligations (PBT Extension — Partial Enforced)

The following are **blocking** for U-10 per the enabled PBT extension (PBT-02, PBT-03, PBT-07, PBT-08 enforced):

### PBT-U10-01 — ShiftStateMachine: terminal state invariant (PBT-03 blocking)
Property: for all reachable states, once `Phase == Done`, no call to `Transition()` succeeds — it always throws.

### PBT-U10-02 — ShiftStateMachine: illegal transition invariant (PBT-03 blocking)
Property: for any state `s` and any non-successor state `t`, `Transition(t)` always throws.

### PBT-U10-03 — ItemBuffer: Snapshot non-destructive round-trip (PBT-02 blocking)
Property: for all generated item collections, `Snapshot()` returns the same item set as a subsequent `TakeAll()`. Calling `Snapshot()` does not remove items from the buffer.

### PBT-U10-04 — ItemBuffer: Add → TakeAll count conservation (PBT-03 blocking)
Property: for all generated sequences of `Add(item, qty)` calls, `TakeAll().Sum(qty) == sum of all added quantities`.

### PBT-U10-05 — ItemBuffer shared generator (PBT-07 blocking)
A shared FsCheck generator `ItemBufferGen` must be created in `Dayswork.Tests/Generators/` for use by U-14 (DepositPlanner PBTs) and any future unit testing ItemBuffer interactions.

### PBT-U10-06 — Seed logging convention (PBT-08 blocking)
All new PBT tests in U-10 follow the seed-logging convention established in U-02: on failure, both the FsCheck replay seed and the shrunk minimal failing input are logged to the test output.

---

## Maintainability

### MAINT-U10-01 — Core purity (NFR-MAINT-03)
`ShiftStateMachine` and `ItemBuffer` live in `Dayswork.Core` and must have zero references to Stardew Valley or SMAPI assemblies. Verified by the `Dayswork.Core` csproj reference list (Core only references Newtonsoft.Json).

### MAINT-U10-02 — i18n (NFR-MAINT-02 / NFR-UX-02)
U-10 introduces no new user-visible strings. No new `i18n/default.json` keys are required in this unit.
