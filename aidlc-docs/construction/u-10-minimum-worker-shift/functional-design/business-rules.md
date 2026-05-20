# U-10 — Business Rules

## State Machine Rules

### BR-01 — Legal transitions only
The state machine enforces a strictly linear transition sequence:
`WaitingForSpawn → Working → Depositing → Exiting → Done`.
Any attempt to transition to a non-successor state throws immediately.
`Done` is terminal — no transition out of `Done` is ever valid.
*Source: FD-Q1 (B), PBT-03 obligation.*

### BR-02 — Intent required for active states
The states `Working`, `Depositing`, and `Exiting` must always carry a non-null intent.
`WaitingForSpawn` and `Done` carry no intent.
Setting intent to null while in an active state is a programming error and throws.
*Source: FD-Q1 (B).*

---

## Spawn Rules

### BR-03 — Spawn trigger
The shift begins on `DayStarted` (6am) when an `Active` + `OneTime` contract exists for today's date.
The `RecurringContractScheduler` fires the spawn; the contract status is immediately set to `Executed`
as a deduplication guard before any game calls are made.
*Source: FR-WORK-01, FD-Q1 (B).*

### BR-04 — Tool snapshot locked at spawn
`ToolLevelReader.ReadSnapshot()` runs once at spawn, before work list building.
The resulting `ToolSnapshot` is immutable for the entire shift.
Tool changes made by the player mid-day do not affect the worker.
A missing tool (sold or never owned) is recorded as level 0 in the snapshot.
*Source: FR-TOOL-01, FR-TOOL-04, FD-Q3 (A).*

---

## Work List Rules

### BR-05 — Work list built once
The work list is built exactly once, at the moment the shift transitions `WaitingForSpawn → Working`.
It is not rebuilt during the shift.
*Source: NFR-PERF-02.*

### BR-06 — Building pre-pass precedes open farm
All building tiles with applicable selected tasks are added to the work list before any open-farm tiles.
Buildings are processed in zone scan order. Within each building, tiles are added in raster order.
*Source: FD-Q2 (updated — all building tasks, not just animals).*

### BR-07 — Open-farm tiles sorted nearest-first
After the building pre-pass, open-farm tiles with applicable selected tasks are sorted by Manhattan
distance from the worker's current position (exit point of the last building visited, or spawn point
if no buildings were visited). Nearest tiles first.
*Source: FD-Q2.*

### BR-08 — No re-scanning mid-shift
Once the work list is built, completed WorkItems are removed from the list.
The list is not re-scanned for new objects that appeared mid-shift (e.g., a crop that finished growing).
*Source: NFR-PERF-02.*

---

## Task Execution Rules

### BR-09 — One task per WorkItem
Each WorkItem encodes exactly one `(tile, task)` pair.
If a tile could theoretically apply to multiple tasks, it appears as multiple WorkItems (one per task).
*Source: domain model — WorkItem is a single tile + single task.*

### BR-10 — Hay routing (Clear Grass)
When the worker cuts grass and the task produces hay:
1. Attempt to deposit hay into the farm silo.
2. If no silo exists on the farm **or** the silo is already full: hay is not collected.
   The grass is still cut. No buffer entry. No drop on the ground.
The orchestrator handles this routing before any call to `ItemBuffer.Add`.
*Source: FR-TASK-09 — corrected from spec; vanilla Stardew behavior: hay silently vanishes if silo full/absent.*

### BR-11 — All other drops go to ItemBuffer
Every item drop not covered by BR-10 is added to `ItemBuffer` immediately when collected.
The buffer never sees hay.
*Source: FR-OUT-01, FD-Q4 (A).*

### BR-12 — 8pm hard cap
When `TimeChanged` fires at 8pm (game-minutes = 1200) and the shift is in `Working` state:
- The current tile's action is abandoned immediately (no partial-task completion).
- The state machine transitions to `Depositing`.
- `shiftEndTime` is recorded at this moment (1200 game-minutes).
*Source: FR-WORK-04, FR-WORK-06.*

---

## Hours and Refund Rules

### BR-13 — Hours tracked by elapsed game time
`hoursWorked = (shiftEndTime − shiftStartTime) / 60`  
`shiftStartTime` = 360 (6am in game-minutes).  
`shiftEndTime` = game-minutes when the work list exhausts OR 8pm fires — whichever comes first.  
Deposit-run time (Working → Depositing transition onwards) is not included.
*Source: FR-PAY-05, FD-Q5 (A).*

### BR-14 — Refund computation
```
refund = deposit − (hoursWorked × hourlyRate)
refund = clamp(refund, 0, deposit)
```
Integer arithmetic only. The clamp prevents floating-point leakage per NFR-SAFE-02.  
Refund is added directly to `Game1.player.Money` at the moment the worker reaches the farm entrance.
*Source: FR-PAY-05, NFR-SAFE-02.*

### BR-15 — Deposit-run time not billed
Walking to the shipping bin and back to the farm entrance does not add to `hoursWorked`.
`shiftEndTime` is frozen before the `Depositing` state begins.
*Source: FR-PAY-05 explicit exclusion.*

---

## Deposit Rules (U-10 thin)

### BR-16 — Single deposit trip to shipping bin
In U-10, all buffered items are deposited in a single trip to the shipping bin.
The shipping bin has no capacity limit (FR-OUT-06); overflow cannot occur in U-10.
Multi-destination deposit trips to assigned chests are deferred to U-14.
*Source: FR-OUT-01, FR-OUT-06, unit-of-work.md U-10 scope.*

### BR-17 — No items lost
Every item the worker collects (excluding hay handled by BR-10) must end up in the shipping bin.
`ItemBuffer.TakeAll()` empties the buffer atomically during the deposit run.
NFR-SAFE-01 holds trivially in U-10 because the shipping bin has infinite capacity.
*Source: NFR-SAFE-01.*

---

## Requirements Correction Note

> **FR-TASK-09 correction**: The requirements document states "If the silo is full, hay is dropped on
> the ground at the worker's current tile." This is incorrect for vanilla Stardew Valley behavior.
> The correct behavior (confirmed by user, 2026-05-19) is: if no silo exists or the silo is full,
> hay is simply not collected — the grass is cut but no hay item is produced or dropped.
> BR-10 above reflects the corrected rule. FR-TASK-09 in `requirements.md` should be updated.
