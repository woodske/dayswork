# U-10 — Business Logic Model

## 1. Shift Lifecycle Overview

A shift is driven by two collaborating objects:

- **ShiftStateMachine** — owns the current phase and current intent. Pure Core type; no Stardew references.
- **ShiftOrchestrator** — subscribes to SMAPI events (`DayStarted`, `UpdateTicked`, `TimeChanged`). Reads the current intent from the state machine and dispatches the appropriate game calls.

```
WaitingForSpawn
    │  trigger: DayStarted (6am), active contract exists
    ▼
Working ──────────────────────────────────────────────┐
    │  carries: IntentMoveToTile | IntentPerformTaskAt │
    │  trigger: work list exhausted OR 8pm cap         │
    ▼                                                  │
Depositing                                            │
    │  carries: IntentDepositInShippingBin             │
    │  trigger: deposit complete                       │
    ▼                                                  │
Exiting                                               │
    │  carries: IntentExitFarm                         │
    │  trigger: farm entrance reached                  │
    ▼                                                  │
Done ◄────────────────────────────────────────────────┘
```

`Done` is terminal — no transition out of Done is ever valid.

---

## 2. Work List Building

Executed once when the shift transitions `WaitingForSpawn → Working`.

### Step 1 — Building pre-pass
1. Enumerate all buildings on the farm in zone scan order.
2. For each building, collect every interior tile that has at least one applicable selected task.
3. Append those tiles to the work list in the order found (raster within the building).
4. The worker will enter each building in sequence and perform all applicable tasks before moving to the next building.

### Step 2 — Open-farm tiles
1. Scan all tiles within the contracted zone(s) that are on the open farm (not inside a building).
2. Filter to tiles that have at least one applicable selected task.
3. Sort by Manhattan distance from the worker's current position at the end of the building pre-pass.
4. Append the sorted open-farm tiles to the work list after the building tiles.

### Result
`workList: List<WorkItem>` — ordered (buildings first, then nearest-first open farm).  
Each `WorkItem` holds: `(TileCoord tile, TaskKind task)`.  
The list is built once and iterated sequentially. Completed items are not re-visited.

---

## 3. Task Execution Loop (Working state)

For each `WorkItem` in the work list:

1. **Set intent** → `IntentMoveToTile(tile)`. Orchestrator drives `FarmhandNpc` to pathfind to the tile.
2. **On arrival** → **Set intent** → `IntentPerformTaskAt(tile, task)`. Orchestrator triggers the game action (chop, water, harvest, cut grass, etc.).
3. **Collect drops**:
   - If task is **Clear Grass** and a drop is **hay**: attempt to add hay to the farm silo. If no silo exists or silo is full, hay is not collected (grass still cut). No buffer entry.
   - All other drops: add to `ItemBuffer`.
4. **Mark WorkItem complete**. Advance to the next item.

If the work list is exhausted, transition to `Depositing`.  
If `TimeChanged` fires at 8pm while still Working, immediately transition to `Depositing` (current tile's action is abandoned).

---

## 4. Hours Tracking

- **`shiftStartTime`**: recorded (in game-minutes from midnight) when the state transitions `WaitingForSpawn → Working`. Always 6am = 360 game-minutes.
- **`shiftEndTime`**: recorded when either:
  - The work list is exhausted (last WorkItem completes), or
  - `TimeChanged` fires at 8pm (= 1200 game-minutes).
- **`hoursWorked = (shiftEndTime − shiftStartTime) / 60`** (integer division, game-minutes → hours).
- Deposit-run time is **not billed** — `shiftEndTime` is captured before the state transitions to `Depositing`.

---

## 5. Deposit Run (U-10 — single trip to shipping bin)

On entering `Depositing` state:

1. Set intent → `IntentDepositInShippingBin`.
2. Orchestrator drives `FarmhandNpc` to walk to the shipping bin.
3. Dump all `ItemBuffer` contents into the shipping bin (shipping bin has no capacity limit per FR-OUT-06).
4. Transition to `Exiting`.

Multi-trip deposit to assigned chests is deferred to U-14.

---

## 6. Exit and Refund

On entering `Exiting` state:

1. Set intent → `IntentExitFarm`.
2. Orchestrator drives `FarmhandNpc` to pathfind to the farm entrance.
3. On arrival at the farm entrance:
   - Compute refund (see Business Rules BR-07).
   - Add refund to `Game1.player.Money`.
   - Remove `FarmhandNpc` from the game world.
   - Transition to `Done`.

---

## 7. RecurringContractScheduler (U-10 stub — one-time contracts only)

Fires on `DayStarted` (6am), before the shift begins:

1. Query `ContractStore` for all contracts with status `Active` and schedule `OneTime` whose `StartDate` matches today.
2. For each matching contract: spawn the shift (transition `ShiftStateMachine` to `Working`, build work list).
3. Update contract status to `Executed` immediately (deduplication guard — prevents double-fire if the event fires more than once).

Recurring contract lifecycle (daily deduction, pause/resume, can't-afford handling) is deferred to U-15.

---

## 8. ToolLevelReader

Fires at 6am spawn, before work list building:

1. Read each tool's upgrade level from `Game1.player`:  
   Axe, Pickaxe, Watering Can, Scythe (Hoe is v1 unused).
2. If the player does not own a tool (e.g., sold it), record level as **0**.
3. Produce an immutable `ToolSnapshot` record.
4. Store the snapshot on the active shift; it is locked for the shift's duration.

No filtering of the work list occurs in U-10. The snapshot is available for U-13's CapabilityEvaluator.
