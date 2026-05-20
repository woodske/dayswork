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
[ ] Create `ShiftPhase` enum: `WaitingForSpawn, Working, Depositing, Exiting, Done`.  
*Stories*: S-07 (worker lifecycle), S-19 (pure Core).

---

### Step 2 — `Dayswork.Core/Shifts/ShiftIntent.cs`
[ ] Create `ShiftIntent` as an abstract record (sealed hierarchy):
- `IntentMoveToTile(TileCoord Destination)`
- `IntentPerformTaskAt(TileCoord Tile, TaskKind Task)`
- `IntentDepositInShippingBin`
- `IntentExitFarm`

*Stories*: S-07, S-19 (pure Core — no Stardew types).

---

### Step 3 — `Dayswork.Core/Shifts/IShiftStateMachine.cs` + `ShiftStateMachine.cs`
[ ] Interface: `Phase`, `CurrentIntent`, `Transition(ShiftPhase, ShiftIntent?)`, `SetIntent(ShiftIntent)`.  
[ ] Implementation: enforce legal linear sequence only
(`WaitingForSpawn→Working→Depositing→Exiting→Done`); throw `InvalidOperationException`
on any non-successor transition; `Done` is terminal.  
`WaitingForSpawn` and `Done` must have null intent; active states must have non-null intent.  
*Stories*: S-07, S-19 (PBT-U10-01, PBT-U10-02).

---

### Step 4 — `Dayswork.Core/Shifts/WorkItem.cs`
[ ] Create immutable record: `WorkItem(TileCoord Tile, TaskKind Task)`.  
*Stories*: S-07, S-08.

---

### Step 5 — `Dayswork.Core/Shifts/ShiftContext.cs`
[ ] Create mutable class holding shift business state:
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
[ ] Interface: `Add(string itemId, int quantity)`, `TakeAll()`, `Snapshot()`, `IsEmpty`.  
[ ] Implementation: `List<(string itemId, int quantity)>` internally.
`TakeAll()` clears and returns all entries. `Snapshot()` returns a copy without clearing.  
*Stories*: S-10, S-19 (PBT-U10-03, PBT-U10-04).

---

### Step 7 — Modify `Dayswork.Core/Domain/ContractStatus.cs`
[ ] Add `Executed` value to the enum (after `Cancelled`).  
Serializer uses `ToString()`/`Enum.Parse()` — no other changes needed.  
*Stories*: S-07 (deduplication guard — one-time contract fires exactly once).

---

### Step 8 — `Dayswork.Tests/Generators/ItemBufferGen.cs`
[ ] Create shared FsCheck generator `ItemBufferGen` in the existing `Generators` namespace.  
Generates: random lists of `(itemId, quantity)` pairs (itemId = arbitrary non-empty string,
quantity = positive int). Exposes `Gen<IReadOnlyList<(string, int)>>` and a helper
that populates an `ItemBuffer` from a generated list.  
*Stories*: S-19 (PBT-U10-05 — shared generator for downstream units).

---

### Step 9 — `Dayswork.Tests/Shifts/ShiftStateMachineTests.cs`
[ ] **PBT-U10-01** — terminal state property: for all paths through the legal sequence,
once `Phase == Done`, any call to `Transition()` always throws.  
[ ] **PBT-U10-02** — illegal transition property: for all `(currentState, targetState)` pairs
where `targetState` is not the direct legal successor, `Transition(targetState)` always throws.  
Use FsCheck `Property.ForAll` + xUnit `[Fact]` wrappers per U-02 seed-logging convention.  
*Stories*: S-19 (PBT-U10-01, PBT-U10-02).

---

### Step 10 — `Dayswork.Tests/Inventory/ItemBufferTests.cs`
[ ] **PBT-U10-03** — snapshot round-trip: for all generated item lists, `Snapshot()` returns
the same items as a subsequent `TakeAll()`. Buffer still non-empty after `Snapshot()`.  
[ ] **PBT-U10-04** — count conservation: for all generated sequences of `Add()` calls,
`TakeAll().Sum(qty) == sum of all added quantities`.  
*Stories*: S-19 (PBT-U10-03, PBT-U10-04).

---

### Step 11 — `Dayswork/Integration/ToolLevelReader.cs`
[ ] Stateless class. One method: `ReadSnapshot(Farmer player) : ToolSnapshot`.  
Reads `player.toolBeingUpgraded` / tool inventory for Axe, Pickaxe, WateringCan, Scythe.
Returns level 0 for any tool not found (FR-TOOL-01, FD-Q3: A).  
*Stories*: S-09.

---

### Step 12 — `Dayswork/Worker/PathFindControllerAdapter.cs`
[ ] Wraps `StardewValley.PathFindController`.  
`StartNavigation(TileCoord dest, GameLocation loc, NPC npc)`: creates a new controller, assigns to `npc.controller`.  
`HasArrived`: true when `npc.controller == null` or controller's path is complete.  
`IsNavigationFailed`: true when navigation was attempted but immediately failed (null path returned).  
*Stories*: S-07.

---

### Step 13 — `Dayswork/Worker/FarmhandNpc.cs`
[ ] Extends Stardew `NPC`. Placeholder sprite (recolored vanilla character, e.g. `Characters/Marnie`).  
Constructor: loads sprite from game content; positions at farm entrance tile; sets display name
via `I18nHelper.Get("npc.farmhand.name")`.  
Override `update(GameTime, GameLocation)`: delegate movement to vanilla NPC update (pathfinding
driven by `PathFindControllerAdapter` assigning `this.controller`).  
Override `takeDamage(...)` stub: return 0 damage (invulnerability with emote deferred to U-13).  
*Stories*: S-07.

---

### Step 14 — `Dayswork/Orchestration/RecurringContractScheduler.cs`
[ ] Subscribes to `GameLoop.DayStarted`.  
On DayStarted:
1. `MultiplayerGuard.IsMultiplayer()` → return immediately if true (REL-U10-01)
2. `ContractStore.ListActiveForDate(today)` → filter for `ContractSchedule.OneTime`
3. For each match:
   a. `ContractStore.Update(id, contract with { Status = ContractStatus.Executed })` ← write-before-spawn
   b. Call `ShiftOrchestrator.StartShift(contract)`

Stub: recurring contracts (daily deposit, pause/resume, can't-afford) deferred to U-15.  
*Stories*: S-07.

---

### Step 15 — `Dayswork/Orchestration/ShiftOrchestrator.cs`
[ ] The core of U-10. Private fields: `_context : ShiftContext?`, `_tickCount : int`,
`_actionPending : bool`, `_navAdapter : PathFindControllerAdapter`, `_farmhand : FarmhandNpc?`.

**`StartShift(Contract contract)`**:
- Read tool snapshot via `ToolLevelReader`
- Build work list (Steps A–D below)
- Spawn `FarmhandNpc` at farm entrance
- Create `ShiftContext`; transition state machine `WaitingForSpawn→Working` with first intent
- Wire the `FarmhandNpc` into the game world (`Game1.getFarm().characters.Add`)

**Work list building**:
A. Building pre-pass: for each `Building` in `Game1.getFarm().buildings` with a non-null `indoors`:
   scan all tiles of the indoor location for applicable tile-based tasks (crops, fruit trees,
   weeds, rocks, trees — NOT animals). Add `WorkItem(tile, task)` in raster order.
B. Open-farm: for each `Zone` in contract.Zones, enumerate tiles in `[TopLeft..BottomRight]`
   on `Game1.getFarm()`. Filter for applicable tasks. Sort by Manhattan distance from
   current position (end of building pre-pass = farm entrance). Append to work list.
C. Task applicability check per tile per enabled task:
   - Water Crops: `HoeDirt` at tile, `crop != null`, `state.Value != HoeDirt.watered`
   - Harvest Crops: `HoeDirt` at tile, `crop != null`, `crop.fullyGrown.Value || (crop.currentPhase.Value >= crop.phaseDays.Count - 1 && !crop.dead.Value)`
   - Collect Fruit: `FruitTree` at tile, `fruitsOnTree.Value > 0`
   - Clear Weeds: object at tile is a `Weed`
   - Clear Grass: terrain feature at tile is `Grass`
   - Clear Rocks: object at tile is a stone/ore object (check `obj.IsBreakableRock()` or equivalent)
   - Cut Trees: terrain feature at tile is `Tree` (not `FruitTree`)

**`OnUpdateTicked(sender, args)` (throttled: `_tickCount++ % 4 != 0 → return`)**:
- If `_context == null` or `Phase == Done` → return
- Dispatch on `CurrentIntent`:
  - `IntentMoveToTile(dest)`: if `HasArrived` → set `IntentPerformTaskAt`; if `NavigationFailed` → skip WorkItem (Pattern 4 — Skip-and-Continue)
  - `IntentPerformTaskAt(tile, task)`: if `!_actionPending` → invoke game action, set `_actionPending = true`; else poll for completion; on complete → collect items → advance WorkList or transition to Depositing
  - `IntentDepositInShippingBin`: if `HasArrived` → `TakeAll()` → deposit into `Game1.getFarm().getShippingBin()` → transition to Exiting
  - `IntentExitFarm`: if `HasArrived` → compute refund → `Game1.player.Money += refund` → remove NPC → transition Done

**Task action invocation per task type**:
- Water Crops: set `hoeDirt.state.Value = HoeDirt.watered`; completion: immediate (same tick); no items
- Harvest Crops: call `crop.harvest(tileX, tileY, hoeDirt, farmer: null, junimoHarvester: true)`;
  collect dropped items from location debris; add to buffer; completion: `hoeDirt.crop == null`
- Collect Fruit: call `fruitTree.shake(tileX, tileY, false)`;
  collect dropped fruit from location debris; add to buffer; completion: `fruitTree.fruitsOnTree == 0`
- Clear Weeds: `weed.performToolAction(scythe, 0, tile)`;
  collect debris; add to buffer (hay special-case: attempt silo, discard if unavailable);
  completion: object gone from tile
- Clear Grass: `grass.performToolAction(scythe, 0, tile)`;
  hay special-case per BR-10; completion: terrain feature gone from tile
- Clear Rocks: `rock.performToolAction(pickaxe, 0, tile)`;
  collect debris; add to buffer; completion: object gone from tile
- Cut Trees: `tree.performToolAction(axe, 0, tile)`;
  collect dropped items from debris; add to buffer; completion: terrain feature gone from tile

*Stories*: S-07 (primary), S-08 (primary), S-09 (primary, snapshot captured), S-10 (primary).

---

### Step 16 — Modify `Dayswork/ModEntry.cs`
[ ] Add singletons: `ToolLevelReader`, `RecurringContractScheduler`, `ShiftOrchestrator`.  
[ ] Wire `helper.Events.GameLoop.DayStarted += scheduler.OnDayStarted`.  
[ ] Wire `helper.Events.GameLoop.UpdateTicked += orchestrator.OnUpdateTicked`.  
[ ] Wire `helper.Events.GameLoop.TimeChanged += orchestrator.OnTimeChanged`.  
*Stories*: S-07.

---

### Step 17 — Modify `Dayswork/i18n/default.json`
[ ] Add key: `"npc.farmhand.name": "Farmhand"`.  
*Stories*: S-20 (all NPC display strings through i18n).

---

### Step 18 — `dotnet build`
[ ] Run `dotnet build` in workspace root. Target: 0 errors, 0 warnings.  
Fix any build issues before proceeding.

---

### Step 19 — `aidlc-docs/construction/u-10-minimum-worker-shift/code/code-summary.md`
[ ] Generate markdown summary of all files created/modified in U-10.

---

### Step 20 — Update state + audit
[ ] Mark U-10 Code Generation complete in `aidlc-state.md`.  
[ ] Append completion entry to `audit.md`.

---

## Story Traceability

| Story | Steps that implement it |
|---|---|
| S-07 Watch farmhand arrive and work | Steps 1–5, 11–17 |
| S-08 Execute tasks in priority order | Steps 4, 15 (work list + task loop) |
| S-09 Snapshot tool capabilities at spawn | Steps 11, 15 (ToolLevelReader) |
| S-10 Deposit collected items at shift end | Steps 6, 15 (ItemBuffer + deposit run) |
| S-19 Pure logic separable from SMAPI | Steps 1–10 (Core types + PBT tests) |
