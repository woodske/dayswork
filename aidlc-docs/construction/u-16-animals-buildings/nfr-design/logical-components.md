# U-16 — Logical Components

**Unit**: U-16 — Animals & Buildings

NFR design decisions applied: NFR-DES-Q1=A (constructor-injected seams), NFR-DES-Q2=A (LogLevel.Warn for building-skip), NFR-DES-Q3=A (IndoorWorkScanner stateless). TS-U16-01..10 apply throughout.

---

## Component Map

```
ModEntry (wiring root)
  │
  ├── ShiftOrchestrator  [EXTENDED]
  │     ├── BuildingWorkNavigator  [NEW — Mod/Orchestration]
  │     ├── IndoorWorkScanner      [NEW — Mod/Orchestration]
  │     ├── AnimalTaskHandler      [NEW — Mod/Orchestration]
  │     ├── ChestResolver          [existing]
  │     ├── StuckDetector          [existing — reused for animals]
  │     ├── WorkerMovementDriver   [existing]
  │     └── MailDispatcher         [existing]
  │
  └── Dayswork.Core  [extended with pure types only]
        ├── WorkBatch              [NEW — pure record]
        ├── BatchKind              [NEW — enum]
        ├── AnimalWorkItem         [NEW — pure record]
        ├── AnimalRef              [NEW — pure record]
        ├── AnimalProductKind      [NEW — enum]
        └── WorkItem.LocationName  [NEW field on existing record]
```

---

## LC-U16-01 — BuildingWorkNavigator

**Layer**: Mod / `Dayswork/Orchestration/`  
**Injection**: Constructor-injected into `ShiftOrchestrator` by `ModEntry` (NFR-DES-Q1=A).

**Responsibilities**:
1. **Locate the door tile** — resolve the human-door approach tile from the `Building` footprint (`Building.humanDoor` / warp tile, confirmed at Code Generation per TS-U16-03).
2. **Approach the door** — hand off to `WorkerMovementDriver` to path the worker on the farm to the door tile.
3. **Execute the enter-warp** — remove the worker NPC from the farm's `characters`; add it to the building interior's `characters`; set `worker.Position` to the interior entry tile; set `worker.currentLocation` to the interior `GameLocation`.
4. **Execute the exit-warp** — reverse: remove from interior `characters`, add to farm `characters`, set `Position` to the exterior exit tile, set `currentLocation` to `Game1.getFarm()`.
5. **Deposit-time enter/exit** — same warp logic, invoked from the deposit loop for any building-interior chest trip (TS-U16-10).
6. **Skip on failure** — if the building is absent/demolished, the door tile is unreachable, or the interior `GameLocation` is null: log `log.building.skipped` at `LogLevel.Warn` (NFR-DES-Q2=A) and return a failure outcome (bool / discriminated result) without throwing.

**Failure contract** (PAT-U16-01):  
Returns `false` (or `BuildingNavOutcome.Skipped`) on any failure. The caller (`ShiftOrchestrator`) advances to the next batch without touching the buffer.

**Not responsible for**:
- Scanning the interior (that is `IndoorWorkScanner`).
- Animal task execution (that is `AnimalTaskHandler`).
- Deposit chest resolution (that is `ChestResolver`, called after warp into the building).

---

## LC-U16-02 — IndoorWorkScanner

**Layer**: Mod / `Dayswork/Orchestration/`  
**Injection**: Constructor-injected into `ShiftOrchestrator` by `ModEntry` (NFR-DES-Q1=A).  
**Statefulness**: **Stateless** — `Scan()` returns the `WorkBatch` to the caller and retains nothing (NFR-DES-Q3=A).

**Responsibilities**:
1. **Resolve the interior bounds** — clamp the zone's `(0,0)..(999,999)` placeholder to the interior's real map size (TS-U16-06).
2. **Run `DetectTask`** — call the existing task detector over the interior `GameLocation` (same as the outdoor scan, but on the building's location object).
3. **Return a `WorkBatch`** — a pure value containing the detected `WorkItem` list (tile tasks) and the `AnimalWorkItem` list (animal tasks) for this location.

**Called**: Once per batch, immediately after `BuildingWorkNavigator` succeeds in entering the building (PAT-U16-03 — lazy at batch entry).

**Not called if**: `BuildingWorkNavigator` returns a failure outcome — preventing any scan of a location the worker cannot reach.

**Not responsible for**:
- Caching results (the orchestrator holds the `WorkBatch` for the duration of the batch).
- Animal eligibility re-validation (that is `AnimalTaskHandler`'s job at execution time).

---

## LC-U16-03 — AnimalTaskHandler

**Layer**: Mod / `Dayswork/Orchestration/`  
**Injection**: Constructor-injected into `ShiftOrchestrator` by `ModEntry` (NFR-DES-Q1=A).

**Responsibilities**:

**Feed (BR-FEED-01..04)**:
- Skip if the building has the deluxe auto-feed flag set (already fed automatically).
- Draw hay from the in-building hopper (silo-supplied via `Farm.piecesOfHay` / hopper API, confirmed at Code Generation per TS-U16-04).
- Place hay on unfilled feed-bench tiles up to the animal count.
- If no hay available (empty silo): log `log.animal.no_silo` at `LogLevel.Warn`; no-op (SAFE-U16-04 / BR-FEED-03).

**Pet (BR-ANIM-02, UX-U16-01)**:
- Resolve the animal's **live** current tile from the live `FarmAnimal` object.
- Hand off to `WorkerMovementDriver` for approach navigation (re-targeting on each movement tick — PAT-U16-04).
- Call `FarmAnimal.pet(...)` — grants full vanilla friendship/mood gain (NFR-Q1=A / TS-U16-04).
- If unreachable within the `StuckDetector` window: skip this animal, log at Debug, continue to the next (PAT-U16-02 / REL-U16-02).
- Re-validate "still needs petting" against live state immediately before calling `pet` (REL-U16-03).

**Collect (BR-PROD-01..06)**:
- Re-validate "still has product" against live `FarmAnimal` state immediately before collecting (REL-U16-03).
- Route by `AnimalProductKind`:
  - **`FloorForage`** (eggs): pick up floor items identified as animal products in the building interior.
  - **`ToolHarvest`** (milk / wool): invoke the vanilla milk-pail / shears harvest path regardless of whether the player owns those tools (DEV-U16-01 / TS-U16-05).
  - **`GroundForage`** (truffles): picked up in the outdoor-farm batch via the existing forage-pickup path (not in this handler directly — the outdoor batch scanner detects them as forage).
- Collected items go to `ShiftContext`'s buffer (existing mechanism, SAFE-U16-01).
- Only collect animal-caused products — never arbitrary placed or dropped items (SAFE-U16-03).

**Not responsible for**:
- Navigation to the building (that is `BuildingWorkNavigator`).
- Scanning which animals need care (that is `IndoorWorkScanner` / `BuildShiftPlan`).
- Deposit (that is the existing deposit loop, unchanged in logic).

---

## LC-U16-04 — ShiftOrchestrator (Extended)

**Layer**: Mod / `Dayswork/Orchestration/`  
**Existing component** — gains new batch-loop logic and multi-location deposit warp.

**New responsibilities**:

**`BuildShiftPlan(zones)`** — pure method (or thin wrapper calling a Core helper):
- Partitions the contract's zones into `WorkBatch` list.
- Assigns each zone to a `BatchKind`: animal building zones → `AnimalBuilding`; non-animal interior zones → `Interior`; outdoor farm zones → `OutdoorFarm`.
- Orders batches: `AnimalBuilding` first, then `Interior`, then `OutdoorFarm` (FD-Q2=A / BR-LOC-01/02).
- Stores result as `ShiftContext.Batches`; `CurrentBatchIndex` starts at 0.
- Pure; testable without SMAPI (PBT-U16-01).

**Batch execution loop** (inside existing `Working` phase, no new state-machine phase — MAINT-U16-02):
1. For each batch:
   a. `BuildingWorkNavigator.TryEnter(batch)` — skip batch on failure (PAT-U16-01).
   b. `IndoorWorkScanner.Scan(interior)` — produces tile + animal work lists (PAT-U16-03).
   c. Execute animal tasks via `AnimalTaskHandler` (Feed → Pet → Collect order per BR-LOC-03).
   d. Execute tile tasks via existing greedy-NN loop (unchanged).
   e. `BuildingWorkNavigator.Exit(batch)`.
2. 8pm cap check: if cap fires mid-batch, stop batch work and exit to farm before deposit (REL-U16-05 / PAT-U16-01).

**Extended deposit run** (inside existing `Depositing` phase):
- For building-interior chest trips: `BuildingWorkNavigator.TryEnter` → `ChestResolver.Deposit` → `BuildingWorkNavigator.Exit` (TS-U16-10 / PAT-U16-07).
- Farm-side chests and shipping bin run on the farm as before.

**Extended `ClearWorker` / `StopForSleepAndSettle`**:
- Remove worker from `worker.currentLocation` (not hardcoded `Game1.getFarm()`) — PAT-U16-06 / SAFE-U16-02.

---

## LC-U16-05 — Core Pure Types (Extended)

**Layer**: `Dayswork.Core`  
**Rule**: Core receives **only** pure data types — no Stardew/SMAPI references (MAINT-U16-01).

| Type | Kind | Purpose |
|---|---|---|
| `WorkBatch` | sealed record | One location's work: `LocationName`, `Kind`, `TileWork`, `AnimalWork`, `FeedBuilding` |
| `BatchKind` | enum | `AnimalBuilding`, `Interior`, `OutdoorFarm` |
| `AnimalWorkItem` | sealed record | One animal task: `LocationName`, `Animal: AnimalRef`, `Task: TaskKind` |
| `AnimalRef` | sealed record | Stable animal identity: `Id: long`, `HomeLocation: string`, `DisplayName: string` |
| `AnimalProductKind` | enum | `FloorForage`, `ToolHarvest`, `GroundForage` |
| `WorkItem.LocationName` | string field (new) | Location context for deposit routing; `"Farm"` by default for existing tile work |

`ShiftContext` gains:
- `Batches: IReadOnlyList<WorkBatch>` — the shift plan for the day.
- `CurrentBatchIndex: int` — which batch is executing.

---

## LC-U16-06 — ModEntry (Wiring)

**Existing component** — wires the three new helpers alongside the existing seams at mod startup.

```csharp
// Illustrative (exact constructor signatures determined at Code Generation)
var buildingNavigator = new BuildingWorkNavigator(monitor, workerMovementDriver);
var indoorScanner     = new IndoorWorkScanner(monitor);
var animalHandler     = new AnimalTaskHandler(monitor, stuckDetector, shiftContext);

var orchestrator = new ShiftOrchestrator(
    monitor,
    chestResolver,
    stuckDetector,
    mailDispatcher,
    buildingNavigator,   // NEW
    indoorScanner,       // NEW
    animalHandler        // NEW
);
```

No new SMAPI event subscriptions needed beyond what `ShiftOrchestrator` already registers. No new Harmony patches (MAINT-U16-03).

---

## Interaction Summary (per batch)

```
ShiftOrchestrator.ExecuteNextBatch()
  │
  ├── BuildingWorkNavigator.TryEnter(batch)
  │     ├── WorkerMovementDriver (approach door tile)
  │     └── Warp handoff (farm → interior characters + position)
  │     [FAILURE → skip batch, log Warn, return]
  │
  ├── IndoorWorkScanner.Scan(interior)
  │     └── DetectTask (existing) → WorkBatch
  │
  ├── AnimalTaskHandler.Feed(batch)     // Feed → Pet → Collect order
  ├── AnimalTaskHandler.Pet(animals)
  │     └── WorkerMovementDriver + FarmAnimal.pet + StuckDetector
  ├── AnimalTaskHandler.Collect(animals)
  │     └── FloorForage / ToolHarvest / GroundForage paths
  │
  ├── Tile-task greedy-NN loop          // existing, unchanged
  │
  └── BuildingWorkNavigator.Exit(batch)
        └── Warp handoff (interior → farm characters + position)
```
