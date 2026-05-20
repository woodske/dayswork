# U-10 — Domain Entities

All entities in this unit are technology-agnostic records/classes unless noted. Stardew/SMAPI types are referenced only in the Mod project (`Dayswork`); Core entities (`Dayswork.Core`) have no game-engine dependencies.

---

## Core entities (Dayswork.Core)

### ShiftPhase *(enum)*
```
WaitingForSpawn | Working | Depositing | Exiting | Done
```
Tracks the high-level shift lifecycle. `Done` is terminal.

---

### ShiftIntent *(discriminated union — one active record per Working/Depositing/Exiting state)*

| Intent record | Carries | Active during |
|---|---|---|
| `IntentMoveToTile` | `TileCoord destination` | Working |
| `IntentPerformTaskAt` | `TileCoord tile`, `TaskKind task` | Working |
| `IntentDepositInShippingBin` | *(none)* | Depositing |
| `IntentExitFarm` | *(none)* | Exiting |

The state machine carries at most one active intent at a time. `WaitingForSpawn` and `Done` carry no intent.

---

### ShiftStateMachine
```
Properties:
  Phase           : ShiftPhase           (current phase)
  CurrentIntent   : ShiftIntent?         (null when WaitingForSpawn or Done)

Methods:
  Transition(newPhase, intent?)          (validates legal transition; throws on illegal)
  SetIntent(intent)                      (replaces current intent within the same phase)
```

**Legal transitions**:
- `WaitingForSpawn → Working`
- `Working → Depositing`
- `Depositing → Exiting`
- `Exiting → Done`
- No transition out of `Done`

All other transitions are illegal and throw.

---

### WorkItem *(immutable record)*
```
TileCoord  Tile
TaskKind   Task
```
One entry in the ordered work list. Represents "go to this tile and perform this task."

---

### ShiftContext *(mutable, owned by ShiftOrchestrator)*
```
ContractId      ContractId
int             ShiftStartTime    (game-minutes from midnight; always 360)
int?            ShiftEndTime      (null until tasks complete or 8pm fires)
List<WorkItem>  WorkList          (ordered; items are removed as completed)
ItemBuffer      Buffer
ToolSnapshot    ToolSnapshot
ShiftStateMachine StateMachine
```
Aggregates all mutable shift state. Created at spawn, discarded at `Done`.

---

### ItemBuffer
```
Methods:
  Add(itemId: string, quantity: int)
  TakeAll() : IReadOnlyList<(string itemId, int quantity)>
  Snapshot() : IReadOnlyList<(string itemId, int quantity)>   (non-destructive; for PBT)
  IsEmpty : bool
```
Simple append-only collection. No routing logic. No knowledge of item types.  
Hay never enters the buffer (routed by the orchestrator before Add is called).

---

### ToolSnapshot *(already defined in U-07; referenced here for completeness)*
```
ToolLevel  AxeLevel
ToolLevel  PickaxeLevel
ToolLevel  WateringCanLevel
ToolLevel  ScytheLevel
```
`ToolLevel` is an int in range [0, 4]. 0 = tool not owned.  
Immutable; locked for the shift at spawn.

---

### ShiftRecord *(produced at shift end for refund calculation)*
```
int   StartTime          (game-minutes)
int   EndTime            (game-minutes)
int   HoursWorked        (= (EndTime - StartTime) / 60)
int   HourlyRate         (g/hr, from contract)
int   Deposit            (g, from contract)
int   Refund             (g, clamped to [0, Deposit])
```
Pure value record; passed to the pricing layer for refund computation.

---

### ContractSchedulerEntry *(internal to RecurringContractScheduler)*
```
ContractId  ContractId
bool        FiredToday    (deduplication guard)
```
Tracks whether a contract has been activated for the current day. Reset at day rollover.

---

## Mod entities (Dayswork — game-engine-dependent)

### FarmhandNpc *(extends Stardew `NPC`)*
Placeholder sprite. No custom fields beyond what the state machine + orchestrator reference.  
Overrides: damage hooks (return 0 damage — invulnerability lands in U-13), `update()` (delegates movement to PathFindControllerAdapter).

### PathFindControllerAdapter
Wraps the game's `PathFindController`. Accepts a `TileCoord` destination and exposes:
```
IsNavigating : bool
HasArrived   : bool
StartNavigation(TileCoord destination, GameLocation location)
```
Orchestrator polls `HasArrived` each `UpdateTicked` frame to detect when to advance the intent.

### ToolLevelReader
Stateless. One method:
```
ReadSnapshot(player: Farmer) : ToolSnapshot
```
Reads upgrade levels directly from the `Farmer` object. Returns level 0 for any tool not present in the player's inventory.
