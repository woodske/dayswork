# U-16 — Animals & Buildings: Domain Entities

**Unit**: U-16 — Animals & Buildings
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=B, FD-Q4=A (+ hopper refinement), FD-Q5=B, FD-Q6=A, FD-Q7=A, FD-Q8=B, FD-Q9=A

This file defines the data shapes U-16 introduces or extends. The unit's central change is making the shift **multi-location**: today every type implicitly lives on `Game1.getFarm()`. SMAPI/Stardew classes (`GameLocation`, `Farm`, `Building`, `AnimalHouse`, `FarmAnimal`, `Chest`) are named only to anchor the model; they stay behind the new orchestration helpers. See [business-logic-model.md](business-logic-model.md) for flows and [business-rules.md](business-rules.md) for enforceable rules.

---

## Existing types reused (no change)

| Type | Role in U-16 |
|---|---|
| `Contract` | Carries `Zones` (now including building zones), `EnabledTasks`, `TaskDestinations`. The three animal tasks (`FeedAnimals`, `PetAnimals`, `CollectAnimalProducts`) already exist in `EnabledTasks` / `TaskKind`. |
| `Zone(LocationName, TopLeft, BottomRight)` | A **building zone** uses the building's *interior* `GameLocation` name with the `(0,0)..(999,999)` whole-interior placeholder (set by `HiringFlowCoordinator` building-select). An **outdoor zone** uses `"Farm"` with real tile bounds. |
| `TaskKind` (enum) | Already contains `FeedAnimals`, `PetAnimals`, `CollectAnimalProducts`; U-16 makes them executable. No new enum members. |
| `DestinationKey` / `ChestDestination` / `ShippingBinDestination` / `MailDestination` | `CollectAnimalProducts` output routes through these unchanged. `ChestDestination.Ref` may point at a building-interior chest. |
| `ChestResolver` | Already resolves a `ChestRef` cross-location via `Game1.getLocationFromName` — reused as-is for in-building chests. |
| `DepositPlanner` / `DepositPlan` / `DepositTrip` | Pure, location-agnostic. Plans trips by destination key; **logic unchanged**. Only the orchestrator's *execution* of the plan becomes multi-location (FD-Q8=B). |
| `ItemBuffer` | `Add(itemId, qty, sourceTask)` buffers animal products tagged `CollectAnimalProducts`, same as outdoor drops. |
| `ShiftContext` | Carries the work queue, buffer, overflow/settlement list, refund math, state machine. Extended below to hold the multi-location batch plan. |
| `ShiftStateMachine` / `ShiftPhase` | Phases unchanged (`WaitingForSpawn, Working, Stuck, Recovering, Depositing, Exiting, Done`). Cross-location warps are a **navigation detail inside `Working`/`Depositing`** — deliberately *not* a new phase, to preserve the PBT-03 state-machine invariants. |
| `ToolSnapshot` / `CapabilityEvaluator` | Reused. May record milk-pail/shears presence for completeness, but those tools are **un-tiered** and the worker performs milk/shear regardless of player ownership (DEV-U16-01, consistent with DEV-U15-03). |
| `WorkItem(NavTile, TaskTile, Task)` | Extended with a location (below). |

---

## New types

### `WorkBatch` (Core — `Dayswork.Core/Shifts/`)

One self-contained unit of work for a single `GameLocation` (FD-Q1=A location-batching).

```
WorkBatch
  LocationName : string            // "Farm" or a building interior name
  Kind         : BatchKind         // AnimalBuilding | Interior | OutdoorFarm
  TileWork     : IReadOnlyList<WorkItem>     // greedy-NN ordered tile tasks in this location
  AnimalWork   : IReadOnlyList<AnimalWorkItem> // pet/collect for animals currently in this location
  FeedBuilding : bool              // true for an AnimalBuilding batch whose animals should be fed
```

- The shift is an **ordered `List<WorkBatch>`** (visit order from `BatchKind`, FD-Q2=A: AnimalBuilding → Interior → OutdoorFarm).
- A building's `TileWork` may be empty (a barn has no crops); an `OutdoorFarm` batch may carry `AnimalWork` (grazing animals, FD-Q5=B) and never sets `FeedBuilding`.

### `BatchKind` (enum, Core)

```
BatchKind { AnimalBuilding, Interior, OutdoorFarm }
```

Drives visit ordering (FD-Q2=A) and which work the scanner produces for the batch.

### `AnimalWorkItem` (Core — `Dayswork.Core/Shifts/`)

A pet-or-collect action targeting a specific animal. Unlike `WorkItem`, the target **moves**, so its approach tile is resolved at execution time, not at scan time.

```
AnimalWorkItem
  LocationName : string      // the animal's CURRENT location (building interior OR "Farm")
  Animal       : AnimalRef
  Task         : TaskKind    // PetAnimals | CollectAnimalProducts (never FeedAnimals — that is building-level)
```

### `AnimalRef` (Core)

A stable, save-safe handle to one farm animal.

```
AnimalRef
  Id          : long         // FarmAnimal.myID — stable identity
  HomeLocation : string      // the interior of the building it lives in (AnimalHouse)
  DisplayName : string       // for logs/diagnostics only
```

### `AnimalProductKind` (enum, Core — classification only)

Classifies *how* a product is gathered, so `AnimalTaskHandler` picks the right interaction (FD-Q3=B). Output routing does **not** branch on this (all products share the one `CollectAnimalProducts` destination, FR-OUT-07).

```
AnimalProductKind
  FloorForage     // egg-type item lying on the coop/barn floor → pick up
  ToolHarvest     // milk (milk pail) / wool (shears) → use tool on the animal
  GroundForage    // truffle on the farm ground (dug by pigs) → pick up
```

### Orchestration helpers (Mod — `Dayswork/Orchestration/`, owned by U-16)

```
BuildingWorkNavigator
  // Drives one building visit: outdoor door approach → warp in → (batch runs) → interior door → warp out.
  ApproachAndEnter(building, worker) : warps the worker into the interior, or reports failure (FD-Q7=A)
  ExitToFarm(worker)                 : returns the worker to the farm at the building door
  // Also used at deposit time to reach a building-interior chest (FD-Q8=B).

IndoorWorkScanner
  // Reuses the existing DetectTask scan over a building interior's REAL map bounds
  // (the (0,0)..(999,999) zone placeholder is interpreted as "whole interior", clamped to map size). (FD-Q6=A)
  ScanInterior(location, enabledTasks, toolSnapshot) : IReadOnlyList<WorkItem>

AnimalTaskHandler
  // Performs the three animal tasks against live FarmAnimal state.
  Feed(animalBuildingLocation)       : fill feed benches from the in-building hopper (silo-supplied) (FD-Q4=A)
  Pet(animalRef)                     : pet one not-yet-petted animal
  Collect(animalRef | floor/ground)  : gather egg/milk/wool/truffle into the buffer (FD-Q3=B)
```

### New shift intents (`Dayswork.Core/Shifts/ShiftIntent.cs`)

Follow the existing invoke-and-poll pattern; they dispatch inside `Working`/`Depositing` and do not add state-machine phases.

```
IntentWarpToLocation(TargetLocationName, EntryTile) : ShiftIntent   // enter/exit a building (FR-WORK-09)
IntentFeedBuilding(LocationName)                     : ShiftIntent   // building-level feed action
IntentPetAnimal(AnimalRef)                           : ShiftIntent
IntentCollectFromAnimal(AnimalRef)                   : ShiftIntent
```

> `IntentCollectFromAnimal` covers `ToolHarvest` (milk/wool) and a held floor product on an animal. Floor eggs and ground truffles that are world `Object`s are collected by the existing tile/forage path (an `IntentPerformTaskAt` on the object tile), so no separate intent is needed for those.

---

## Extended types

### `WorkItem` (Core) — gains a location

```
WorkItem(LocationName : string, NavTile : TileCoord, TaskTile : TileCoord, Task : TaskKind)
```

`LocationName` defaults to `"Farm"` for every existing outdoor caller (no behavior change outdoors). Indoor tile work carries the interior name.

### `ShiftContext` (Core)

| Member | Change |
|---|---|
| `Batches : IReadOnlyList<WorkBatch>` (new) | The ordered multi-location plan built at shift start. Replaces the single flat `WorkList` as the top-level structure; the per-batch tile items still feed the existing `Queue<WorkItem>` working loop one batch at a time. |
| `CurrentBatchIndex : int` (new) | Pointer into `Batches`; advanced when a batch's work is exhausted. |
| `WorkList` (existing) | Now holds **the current batch's** remaining tile/animal work, refilled on batch advance. |

### `ShiftOrchestrator` (M-12)

| Member | Change |
|---|---|
| `StartShift(...)` | Builds `Batches` (multi-location) instead of a single farm work list; begins the first batch (warp in if it is a building). |
| location references | Working/navigation use **the current batch's location** instead of hardcoded `Game1.getFarm()`. Spawn, exit, and shipping-bin still reference the farm. |
| batch advance (new) | When a batch exhausts: if it was a building, warp out to the farm; then move to the next batch (warp in if it is a building). On building nav/resolve failure, skip the batch (FD-Q7=A). |
| deposit run | Extended to warp into a building for any building-interior chest trip, then warp back (FD-Q8=B). Farm-chest and shipping-bin trips run on the farm as today. |
| animal dispatch (new) | Handles `IntentFeedBuilding` / `IntentPetAnimal` / `IntentCollectFromAnimal` via `AnimalTaskHandler`. |

### `WorkerMovementDriver` (Mod)

| Member | Change |
|---|---|
| `StartNavigation(...)` | Already takes a `GameLocation`; callers now pass the **current batch location** rather than always the farm. |
| warp handoff (new) | A helper to move the worker between locations (remove from the old location's `characters`, add to the new, set entry position). `IsTilePassableForWorker` gains an interior branch (today it special-cases `Farm` building-occupancy). |

### `C-07 TaskPriorityOrderer` (Core)

No code change required, but its animal slots (`FeedAnimals`=0, `PetAnimals`=1, `CollectAnimalProducts`=2) are now **reachable**: animal work within a batch is ordered Feed → Pet → Collect by this orderer (FD-Q2=A), while tile work stays greedy nearest-neighbour (DEV-02).

---

## New i18n keys (added to `i18n/default.json`)

| Key | Use |
|---|---|
| `log.building.entering` | SMAPI log when the worker warps into a building. |
| `log.building.skipped` | SMAPI log when a building batch is skipped (unreachable/demolished, FD-Q7=A). |
| `log.animal.fed` | SMAPI log for a building feed action (count fed / hay drawn). |
| `log.animal.no_silo` | SMAPI log when feeding is skipped for lack of a silo/hay. |

> No new **mail** strings: animal-product overflow and the refund reuse U-15's single settlement letter (`mail.sender`, `mail.overflow.*`, `mail.settlement.*`). Hay is never an output (FR-TASK-09), and Feed/Pet produce nothing.

---

## What U-16 does NOT add

- **No new save structure.** Building zones already persist in the contract; animals/feed state live in the live game. (FR-PERSIST-01, NFR-SAFE-03.)
- **No new state-machine phase.** Warps ride inside `Working`/`Depositing` to keep the PBT-03 invariants intact.
- **No pricing/estimator types.** Deposits keep `DepositHoursPolicy.FlatPreviewHours` (FD-Q9=A / DEV-U15-07).
- **No multi-worker types.** Single active contract / single worker stands (DEV-U15-01).
