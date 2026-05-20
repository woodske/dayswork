# U-10 — Minimum Worker Shift: Code Generation Plan

**Unit**: U-10 — Minimum Worker Shift  
**Stories**: S-07 (primary), S-08 (primary), S-09 (primary), S-10 (primary), S-19 (PBT obligations)  
**Phase**: CONSTRUCTION — Code Generation

---

## Unit Context

**Components owned**: C-08 ShiftStateMachine, C-10 ItemBuffer, M-09 FarmhandNpc,
M-11 PathFindControllerAdapter, M-12 ShiftOrchestrator, M-13 RecurringContractScheduler (stub),
M-19 ToolLevelReader.

**Extends**: M-01 ModEntry (wire worker singletons + DayStarted/UpdateTicked/TimeChanged events).

**Dependencies satisfied**: U-01 (scaffold), U-02 (test infra), U-03 (ConfigSnapshot),
U-04 (Zone, TileCoord, TaskKind), U-05 (RateCalculator, RefundCalculator), U-06 (ContractStore),
U-07 (ToolSnapshot, CapabilityEvaluator — snapshot only used in U-10, no filtering).

**Key design decisions**:
- Intent-carrying state machine (FD-Q1: B)
- Building pre-pass (tile-based tasks only) then nearest-first open-farm (FD-Q2 updated)
- No capability filtering in U-10; missing tool = level 0 (FD-Q3: A)
- Hay routing in orchestrator; silo full/absent = hay not collected (FD-Q4: A, corrected)
- Hours = elapsed game time, two timestamps (FD-Q5: A)
- UpdateTicked throttled every 4 ticks (N1: B)
- Task action: invoke + poll for object/state change (N2: B)

**Animal tasks scope note**: Feed Animals, Pet Animals, Collect Animal Products require iterating
`FarmAnimal` objects (not tile scanning) and separate game API interactions. These are deferred
to U-13. U-10's building pre-pass covers only tile-based tasks (crops, fruit trees, weeds, rocks,
trees) found inside building interiors (e.g. Greenhouse crops). Building entry itself is scaffolded.

**ContractStatus note**: `Executed` value must be added for the deduplication guard. The existing
serializer uses `ToString()`/`Enum.Parse()` so adding a new enum value is safe.

---

## Code Location

- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Core**: `Dayswork.Core\`
- **Mod**: `Dayswork\`
- **Tests**: `Dayswork.Tests\`
- **Docs**: `aidlc-docs\construction\u-10-minimum-worker-shift\code\`

---

## Steps

### Step 1 — `Dayswork.Core/Shifts/ShiftPhase.cs`
[x] Create `ShiftPhase` enum: `WaitingForSpawn, Working, Depositing, Exiting, Done`.  
*Stories*: S-07 (worker lifecycle), S-19 (pure Core).

---

### Step 2 — `Dayswork.Core/Shifts/ShiftIntent.cs`
[x] Create `ShiftIntent` as an abstract record (sealed hierarchy):
- `IntentMoveToTile(TileCoord Destination)`
- `IntentPerformTaskAt(TileCoord Tile, TaskKind Task)`
- `IntentDepositInShippingBin`
- `IntentExitFarm`

*Stories*: S-07, S-19 (pure Core — no Stardew types).

---

### Step 3 — `Dayswork.Core/Shifts/IShiftStateMachine.cs` + `ShiftStateMachine.cs`
[x] Interface: `Phase`, `CurrentIntent`, `Transition(ShiftPhase, ShiftIntent?)`, `SetIntent(ShiftIntent)`.  
[x] Implementation: enforce legal linear sequence only
(`WaitingForSpawn→Working→Depositing→Exiting→Done`); throw `InvalidOperationException`
on any non-successor transition; `Done` is terminal.  
`WaitingForSpawn` and `Done` must have null intent; active states must have non-null intent.  
*Stories*: S-07, S-19 (PBT-U10-01, PBT-U10-02).

---

### Step 4 — `Dayswork.Core/Shifts/WorkItem.cs`
[x] Create immutable record: `WorkItem(TileCoord Tile, TaskKind Task)`.  
*Stories*: S-07, S-08.

---

### Step 5 — `Dayswork.Core/Shifts/ShiftContext.cs`
[x] Create mutable class holding shift business state:
- `ContractId ContractId`
- `IReadOnlyList<Zone> Zones`
- `IReadOnlySet<TaskKind> EnabledTasks`
- `int DepositAmount`, `int HourlyRate`
- `int ShiftStartTime` (game-minutes)
- `int? ShiftEndTime` (null until tasks done or 8pm)
- `Queue<WorkItem> WorkList`
- `ItemBuffer Buffer`
- `ToolSnapshot ToolSnapshot`
- `ShiftStateMachine StateMachine`

*Stories*: S-07.

---

### Step 6 — `Dayswork.Core/Inventory/IItemBuffer.cs` + `ItemBuffer.cs`
[x] Interface: `Add(string itemId, int quantity)`, `TakeAll()`, `Snapshot()`, `IsEmpty`.  
[x] Implementation: `List<(string itemId, int quantity)>` internally.
`TakeAll()` clears and returns all entries. `Snapshot()` returns a copy without clearing.  
*Stories*: S-10, S-19 (PBT-U10-03, PBT-U10-04).

---

### Step 7 — Modify `Dayswork.Core/Domain/ContractStatus.cs`
[x] Add `Executed` value to the enum (after `Cancelled`).  
Serializer uses `ToString()`/`Enum.Parse()` — no other changes needed.  
*Stories*: S-07 (deduplication guard — one-time contract fires exactly once).

---

### Step 8 — `Dayswork.Tests/Generators/ItemBufferGen.cs`
[x] Create shared FsCheck generator `ItemBufferGen` in the existing `Generators` namespace.  
Generates: random lists of `(itemId, quantity)` pairs (itemId = arbitrary non-empty string,
quantity = positive int). Exposes `Gen<IReadOnlyList<(string, int)>>` and a helper
that populates an `ItemBuffer` from a generated list.  
*Stories*: S-19 (PBT-U10-05 — shared generator for downstream units).

---

### Step 9 — `Dayswork.Tests/Shifts/ShiftStateMachineTests.cs`
[x] **PBT-U10-01** — terminal state property: for all paths through the legal sequence,
once `Phase == Done`, any call to `Transition()` always throws.  
[x] **PBT-U10-02** — illegal transition property: for all `(currentState, targetState)` pairs
where `targetState` is not the direct legal successor, `Transition(targetState)` always throws.  
Use FsCheck `Property.ForAll` + xUnit `[Fact]` wrappers per U-02 seed-logging convention.  
*Stories*: S-19 (PBT-U10-01, PBT-U10-02).

---

### Step 10 — `Dayswork.Tests/Inventory/ItemBufferTests.cs`
[x] **PBT-U10-03** — snapshot round-trip: for all generated item lists, `Snapshot()` returns
the same items as a subsequent `TakeAll()`. Buffer still non-empty after `Snapshot()`.  
[x] **PBT-U10-04** — count conservation: for all generated sequences of `Add()` calls,
`TakeAll().Sum(qty) == sum of all added quantities`.  
*Stories*: S-19 (PBT-U10-03, PBT-U10-04).

---

### Step 11 — `Dayswork/Integration/ToolLevelReader.cs`
[x] Stateless class. One method: `ReadSnapshot(Farmer player) : ToolSnapshot`.  
Reads player tool inventory for Axe, Pickaxe, WateringCan.
Returns level 0 for any tool not found (FR-TOOL-01, FD-Q3: A).  
*Stories*: S-09.

---

### Step 12 — `Dayswork/Worker/PathFindControllerAdapter.cs`
[x] U-10 thin slice: uses `Game1.warpCharacter` to teleport NPC to destination each call.
`HasArrived` set true after warp; `NavigationFailed` set if tile is not passable.
Real PathFindController walking deferred to U-13.  
*Stories*: S-07.

---

### Step 13 — `Dayswork/Worker/FarmhandNpc.cs`
[x] Extends Stardew `NPC`. Placeholder sprite `Characters\Marnie`.  
Constructor: loads sprite, portrait; sets display name via `I18nHelper.Get("npc.farmhand.name")`.  
`takeDamage` override deferred to U-13 (FR-NPC-02).  
*Stories*: S-07.

---

### Step 14 — `Dayswork/Orchestration/RecurringContractScheduler.cs`
[x] Subscribes to `GameLoop.DayStarted`.  
On DayStarted:
1. `MultiplayerGuard.IsMultiplayer()` → return immediately if true (REL-U10-01)
2. `ContractStore.ListActiveForDate(today)` → filter for `ContractSchedule.OneTime`
3. For each match:
   a. `ContractStore.Update(id, contract with { Status = ContractStatus.Executed })` ← write-before-spawn
   b. Call `ShiftOrchestrator.StartShift(contract)`

Season ambiguity resolved: `Enum.Parse<Dayswork.Core.Domain.Season>(...)`.  
Stub: recurring contracts deferred to U-15.  
*Stories*: S-07.

---

### Step 15 — `Dayswork/Orchestration/ShiftOrchestrator.cs`
[x] Full shift loop implemented: work list building, UpdateTicked dispatch, task invocation,
deposit run, exit with refund.  
API corrections applied: `FruitTree.fruit.Count` (SV 1.6), `obj.Name == "Stone"` for rocks
(ore nodes/boulders with tool gates deferred to U-13), `_pendingTask` initialized in StartShift.  
*Stories*: S-07 (primary), S-08 (primary), S-09 (primary), S-10 (primary).

---

### Step 16 — Modify `Dayswork/ModEntry.cs`
[x] Added singletons: `ToolLevelReader`, `ShiftOrchestrator`, `RecurringContractScheduler`.  
[x] Wired `DayStarted`, `UpdateTicked`, `TimeChanged` events.  
*Stories*: S-07.

---

### Step 17 — Modify `Dayswork/i18n/default.json`
[x] Added key: `"npc.farmhand.name": "Farmhand"`.  
*Stories*: S-20.

---

### Step 18 — `dotnet build`
[x] Build succeeded: 0 errors, 0 warnings. Mod auto-deployed to Mods/Dayswork/.
Build errors resolved: Season ambiguity, FruitTree API, rock detection, _pendingTask init,
log operator precedence, unused variable warnings.

---

### Step 19 — `aidlc-docs/construction/u-10-minimum-worker-shift/code/code-summary.md`
[x] Generated markdown summary of all files created/modified in U-10.

---

### Step 20 — Update state + audit
[x] Marked U-10 Code Generation complete in `aidlc-state.md`.  
[x] Appended completion entry to `audit.md`.

---

## Story Traceability

| Story | Steps that implement it |
|---|---|
| S-07 Watch farmhand arrive and work | Steps 1–5, 11–17 |
| S-08 Execute tasks in priority order | Steps 4, 15 (work list + task loop) |
| S-09 Snapshot tool capabilities at spawn | Steps 11, 15 (ToolLevelReader) |
| S-10 Deposit collected items at shift end | Steps 6, 15 (ItemBuffer + deposit run) |
| S-19 Pure logic separable from SMAPI | Steps 1–10 (Core types + PBT tests) |
