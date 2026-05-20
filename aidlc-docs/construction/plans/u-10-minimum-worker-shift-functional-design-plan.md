# U-10 — Minimum Worker Shift: Functional Design Plan

**Unit**: U-10 — Minimum Worker Shift  
**Stories**: S-07 (primary), S-08 (primary), S-09 (primary), S-10 (primary), S-19 (ShiftStateMachine + ItemBuffer PBT obligations)  
**Phase**: CONSTRUCTION — Functional Design

---

## Plan Checklist

- [x] FD-Q1–Q5: Collect answers to design questions
- [x] Resolve any ambiguities from answers (FD-Q2 updated; FR-TASK-09 corrected)
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [ ] Present completion message and await approval

---

## Context Summary

U-10 introduces the worker shift loop — the core mechanic that makes the mod work.
Key new components: **ShiftStateMachine** (WaitingForSpawn → Working → Depositing → Exiting → Done),
**ItemBuffer** (holds collected items for the entire shift), **FarmhandNpc** (game NPC subclass),
**PathFindControllerAdapter** (wraps the game's built-in pathfinder), **ShiftOrchestrator**
(SMAPI-event-driven coordinator), **RecurringContractScheduler** (one-time-contracts stub),
**ToolLevelReader** (reads tool upgrade levels at spawn).

**Already decided / not in scope for questions**:
- FR-WORK-03 priority order is fixed (Feed→Pet→Collect animal products→Water→Harvest→Collect fruit→Clear weeds→Clear grass→Clear rocks→Cut trees)
- 8pm hard cap triggers Depositing state; deposit runs still complete before exit (FR-WORK-04/06)
- Refund added to gold at exit: `refund = deposit − (actual hours worked × rate)` (FR-PAY-05)
- Deposit-run time is NOT billed (FR-PAY-05)
- Shipping bin is the only deposit destination in U-10 (multi-chest deferred to U-14)
- Placeholder sprite for FarmhandNpc (FR-NPC-01, Q9)
- Stuck detection and tool-swap visuals deferred to U-13
- Full skip-rule application (trellis adjacency, not-ready crops, multi-level capability checks) deferred to U-13

---

## Design Questions

### FD-Q1 — State machine vs. orchestrator responsibility split

The ShiftStateMachine tracks the high-level shift phase (WaitingForSpawn / Working / Depositing / Exiting / Done).
The ShiftOrchestrator drives the work loop (what tile to visit next, which action to perform, when to transition state).
There are two ways to model this split:

**A) Thin state machine, fat orchestrator** — The state machine tracks only the five phases.
All granular tracking (which task is active, which tile is current, what action is in progress) lives
in the orchestrator as mutable fields. The state machine is a small discriminated union with five states
and transition guards. The orchestrator calls `stateMachine.Transition(newState)` when it's ready
to change phase.

**B) Intent-carrying state machine** — The Working state carries a sub-object describing the *current intent*
(e.g., `IntentMoveToTile`, `IntentPerformTaskAt`, `IntentDepositInShippingBin`, `IntentExitFarm`).
The orchestrator steps the state machine each tick and reads the current intent to decide what SMAPI/game
calls to make. The unit-of-work.md references intent records by name, so this is partially anticipated.

Which split do you prefer?

[Answer]: 

---

### FD-Q2 — Task tile discovery and ordering within a task type

NFR-PERF-02 says tile scanning happens **once per zone entry**, not per frame. After tiles are scanned,
how should the work list be ordered within a single task type?

**A) Flat priority list, nearest-first within type** — Build one flat list grouped by task type in FR-WORK-03
priority order. Within each group, sort tiles by Manhattan distance from the worker's spawn point
(greedy start). The orchestrator walks the list sequentially.

**B) Flat priority list, scan order within type** — Same grouping, but within each group tiles are in
the order the zone scan returned them (raster order: left-to-right, top-to-bottom). Simpler to implement;
not as travel-efficient.

**C) Lazy discovery** — Only discover the next tile when the worker finishes the current one. Scan the zone
for the nearest uncompleted tile of the highest-priority remaining task type. Re-scan cost is O(zone_tiles)
per tile, which may be acceptable for typical farm sizes.

Which ordering strategy do you want?

[Answer]: 

---

### FD-Q3 — Capability checks in U-10 (thin)

The unit-of-work.md says U-10 is deliberately thin on capability: "ToolLevelReader runs at 6am and snapshot
is captured" while "full skip-rule branches" are deferred to U-13. In U-10:

**A) Snapshot-only (no checks)** — ToolLevelReader reads and stores the ToolSnapshot at spawn.
No capability filtering is applied to the tile list in U-10; the orchestrator attempts every object.
The game engine handles graceful failure (e.g., trying to chop a boulder with a basic pickaxe just
does nothing). Full skip-rule integration deferred entirely to U-13.

**B) Tool-presence checks only** — After reading the snapshot, skip any task type for which the
player has zero level (tool not owned / sold). E.g., if pickaxe level = 0, Clear Rocks tiles are
excluded from the work list entirely. Object-level granularity (can I chop THIS stump?) deferred to U-13.

**C) Full capability matrix** — Apply C-06 CapabilityEvaluator from U-07 in U-10's orchestrator,
including all object-level skip rules. U-13 would only add the Stuck/Recovering states and tool-swap
visuals, not new capability logic.

[Answer]: 

---

### FD-Q4 — Hay routing responsibility

When "Clear Grass" is active, hay must go to the silo first; if silo is full, hay drops on the
worker's current tile (per FR-TASK-09). This means hay is never shipped (no shipping-bin deposit,
no mail). Two designs:

**A) Orchestrator handles hay routing** — Before handing any collected items to ItemBuffer, the orchestrator
checks item type. Hay → attempt silo deposit → if full, drop on current tile. Everything else → ItemBuffer.
The buffer never sees hay. Buffer's only responsibility is "hold non-hay drops until deposit run."

**B) ItemBuffer-aware routing** — The orchestrator passes all collected items to ItemBuffer. ItemBuffer
has a per-item routing policy: hay is flagged as `RouteToSilo`, everything else is `RouteToShippingBin`
(for U-10). On deposit, ItemBuffer calls the silo adapter first for hay, then the shipping bin adapter
for everything else.

[Answer]: 

---

### FD-Q5 — Actual hours worked: what does the clock measure?

FR-PAY-05: `refund = deposit − (actual hours worked × hourly rate)`. Deposit-run time is excluded.
Two interpretations of "actual hours worked":

**A) Elapsed game time** — Record the game timestamp at spawn (6:00 AM = 0 game-minutes offset).
Record the game timestamp when the last task action completes (or 8pm cap fires). Divide
`(end_time − start_time)` by 60 to get hours. Deposit-run time is automatically excluded because
the time measurement stops when the last task action completes, before the worker starts walking
to the shipping bin.

**B) Task-tick accounting** — Count in-game-minute ticks actually spent performing task actions
(exclude pathfinding time). More precise but more complex to implement; pathfinding time is not
billed, reducing the refund.

Which interpretation do you prefer?

[Answer]: 

---

## Artifact output (after answers collected)

- `aidlc-docs/construction/u-10-minimum-worker-shift/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-10-minimum-worker-shift/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-10-minimum-worker-shift/functional-design/business-rules.md`
