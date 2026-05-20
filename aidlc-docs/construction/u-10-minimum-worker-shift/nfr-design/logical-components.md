# U-10 — Logical Components

## Component Map

```
SMAPI Events
    │
    ├─ DayStarted ──────────► RecurringContractScheduler
    │                               │
    │                               ├─ checks MultiplayerGuard
    │                               ├─ queries ContractStore (U-06)
    │                               ├─ sets contract Executed
    │                               └─ calls ShiftOrchestrator.StartShift()
    │
    ├─ UpdateTicked (÷4) ───► ShiftOrchestrator
    │                               │
    │                               ├─ reads ShiftStateMachine.CurrentIntent
    │                               │
    │                               ├─[IntentMoveToTile]
    │                               │    └─ PathFindControllerAdapter.HasArrived?
    │                               │         ├─ No  → continue (wait)
    │                               │         └─ Yes → SetIntent(IntentPerformTaskAt)
    │                               │
    │                               ├─[IntentPerformTaskAt]
    │                               │    ├─ if !_actionPending → invoke game API
    │                               │    └─ poll object/animal state
    │                               │         ├─ still present → wait
    │                               │         └─ gone → advance WorkList
    │                               │              ├─ more items → SetIntent(IntentMoveToTile)
    │                               │              └─ empty → Transition(Depositing)
    │                               │
    │                               ├─[IntentDepositInShippingBin]
    │                               │    └─ PathFindControllerAdapter.HasArrived?
    │                               │         ├─ No  → wait
    │                               │         └─ Yes → ItemBuffer.TakeAll() → deposit
    │                               │                   → Transition(Exiting)
    │                               │
    │                               └─[IntentExitFarm]
    │                                    └─ PathFindControllerAdapter.HasArrived?
    │                                         ├─ No  → wait
    │                                         └─ Yes → compute refund → add gold
    │                                                   → remove NPC → Transition(Done)
    │
    └─ TimeChanged (8pm) ───► ShiftOrchestrator
                                    └─ if Phase == Working → Transition(Depositing)
```

---

## Component Responsibilities

### RecurringContractScheduler *(Mod)*
- Subscribes to `DayStarted`.
- Applies the Deduplication Guard Pattern (Pattern 5).
- Calls `MultiplayerGuard` first (REL-U10-01).
- Creates `ShiftContext` and calls `ShiftOrchestrator.StartShift(context)`.
- In U-10: handles one-time contracts only. Recurring lifecycle deferred to U-15.

### ShiftOrchestrator *(Mod)*
- Subscribes to `UpdateTicked` (Throttled-Tick Pattern, Pattern 1) and `TimeChanged`.
- Owns the active `ShiftContext` (null when no shift is in progress).
- Reads `ShiftStateMachine.CurrentIntent` and dispatches game calls accordingly.
- Drives all state machine transitions.
- Calls `ToolLevelReader.ReadSnapshot()` at shift start before building the work list.
- Handles hay routing: intercepts grass-cut drops, attempts silo deposit, discards hay if silo unavailable or full.

### ShiftStateMachine *(Core)*
- Owns `Phase` and `CurrentIntent`.
- Enforces legal transitions only (throws on illegal).
- No Stardew references. Pure discriminated union logic.
- PBT-tested: terminal-state invariant (PBT-U10-01), illegal-transition invariant (PBT-U10-02).

### ItemBuffer *(Core)*
- Append-only collection of `(itemId, quantity)` pairs.
- `Add()`, `TakeAll()`, `Snapshot()`, `IsEmpty`.
- No routing logic. No Stardew references.
- PBT-tested: snapshot round-trip (PBT-U10-03), count conservation (PBT-U10-04).
- Shared FsCheck generator `ItemBufferGen` created for downstream use (PBT-U10-05).

### PathFindControllerAdapter *(Mod)*
- Wraps `StardewValley.PathFindController`.
- `StartNavigation(TileCoord, GameLocation)`: creates a new controller and assigns it to `FarmhandNpc`.
- `HasArrived`: true when the controller's path queue is empty or null.
- Skip-and-Continue Pattern (Pattern 4): if controller returns null path, signals failure immediately.

### FarmhandNpc *(Mod — extends `NPC`)*
- Placeholder sprite (recolored vanilla NPC).
- Added to `Game1.currentLocation.characters` at spawn; removed on `Done`.
- `update()` delegates to `PathFindControllerAdapter` for movement.
- Invulnerability override deferred to U-13.

### ToolLevelReader *(Mod)*
- Stateless. Called once at shift start by `ShiftOrchestrator`.
- Returns `ToolSnapshot` with level 0 for any missing tool (FR-TOOL-01, FD-Q3: A).

---

## Data Flow: Shift Start

```
DayStarted
  → RecurringContractScheduler
      → ContractStore.ListActiveForDate(today)          [U-06]
      → contract.Status = Executed
      → ContractStore.Save()
      → ToolLevelReader.ReadSnapshot(Game1.player)      → ToolSnapshot
      → BuildWorkList(contract, ToolSnapshot)           → Queue<WorkItem>
      → new ShiftContext(contract, workList, buffer, sm)
      → ShiftStateMachine.Transition(Working, IntentMoveToTile(workList.Peek().Tile))
      → PathFindControllerAdapter.StartNavigation(firstTile, farm)
      → FarmhandNpc added to farm.characters
```

## Data Flow: Shift End (work list exhausted)

```
UpdateTicked (÷4)
  → ShiftOrchestrator
      → WorkList.IsEmpty → true
      → shiftEndTime = Game1.timeOfDay
      → ShiftStateMachine.Transition(Depositing, IntentDepositInShippingBin)
      → PathFindControllerAdapter.StartNavigation(shippingBin.Tile, farm)

  [next target ticks]
  → HasArrived → true
      → ItemBuffer.TakeAll() → deposit all into ShippingBin
      → ShiftStateMachine.Transition(Exiting, IntentExitFarm)
      → PathFindControllerAdapter.StartNavigation(farmEntrance, farm)

  [next target ticks]
  → HasArrived → true
      → hoursWorked = (shiftEndTime - 360) / 60
      → refund = clamp(deposit - hoursWorked × rate, 0, deposit)
      → Game1.player.Money += refund
      → farm.characters.Remove(FarmhandNpc)
      → ShiftStateMachine.Transition(Done)
      → ShiftContext = null
```

---

## Extension Compliance Summary

| Rule | Status | Rationale |
|---|---|---|
| PBT-02 (blocking) | Compliant | `ItemBuffer.Snapshot()` round-trip PBT specified (PBT-U10-03) |
| PBT-03 (blocking) | Compliant | `ShiftStateMachine` terminal + illegal-transition invariants specified (PBT-U10-01/02); `ItemBuffer` count conservation (PBT-U10-04) |
| PBT-07 (blocking) | Compliant | `ItemBufferGen` shared generator specified (PBT-U10-05) |
| PBT-08 (blocking) | Compliant | Seed-logging convention inherited from U-02 (PBT-U10-06) |
| PBT-09 (blocking) | N/A | Framework already established in U-02; no new setup required |
| PBT-01/04/05/06/10 | Advisory | No action required; advisory rules noted |
| Security Baseline | N/A | Extension disabled at Requirements Analysis (Q28) |
