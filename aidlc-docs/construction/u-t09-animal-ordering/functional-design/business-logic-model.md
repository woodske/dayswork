# Functional Design — Business Logic Model — u-t09-animal-ordering

## Purpose
Re-order the worker's animal work so that **all of a single animal building's animal work is performed before moving to the next building**, while keeping farm-wide truffle forage as a single final pass. Implements FR-T09-01..08.

## Current Model (baseline, verified in source)
`ShiftPlanBuilder.BuildBatchPlan` emits, for enabled animal tasks:
1. One `AnimalBuilding` batch **per** selected building (ordered by `LocationName` ordinal, then `Tier`).
2. A **single** trailing `OutdoorAnimals` ("Farm") batch that does **both** (a) pet/collect on every selected building's grazing animals **and** (b) a whole-farm scan for ground forage (truffles) via `CollectAnimalProducts`.

At runtime, `ShiftOrchestrator` enters each building interior (feed/pet/collect housed animals), then runs the one outdoor pass — refreshing grazing animal work + forage at batch start and re-scanning late truffles before completion.

**Problem**: with buildings spread across the farm, the worker crosses the farm once for all interiors, then again for the combined outdoor pass — backtracking.

## Target Model
For each selected building, emit a **contiguous pair**:
1. `AnimalBuilding` batch (enter interior; feed/pet/collect housed animals) — unchanged.
2. `OutdoorAnimals` batch **scoped to that one building** (pet/collect that building's grazing animals on the Farm) — emitted only when non-feed animal tasks are enabled.

After all building pairs, emit a single **`FarmForage`** batch (new `BatchKind`) that performs the whole-farm ground-forage sweep (truffles) — emitted only when `CollectAnimalProducts` is enabled. This isolates the farm-wide, non-building-attributed forage from the per-building grazing work.

### Batch sequence (illustrative)
```
AnimalBuilding(Barn-A)      ── enter interior, feed/pet/collect housed
OutdoorAnimals(Barn-A)      ── that barn's grazing animals (pet/collect)
AnimalBuilding(Coop-B)
OutdoorAnimals(Coop-B)
FarmForage(Farm)            ── farm-wide truffle sweep (once, last), if Collect enabled
Greenhouse / OutdoorCrops / OutdoorClearing   ── unchanged, after animal work
```

## Key Workflows

### W1 — Build batch plan (pure, `ShiftPlanBuilder`)
1. Compute `animalTasks` (enabled animal-service tasks, priority-ordered) and `outdoorAnimalTasks = animalTasks \ {FeedAnimals}`.
2. If `animalTasks` is non-empty, for each building in the existing deterministic order:
   - Add an `AnimalBuilding` skeleton (`feedBuilding = animalTasks contains FeedAnimals`).
   - If `outdoorAnimalTasks` is non-empty, add an `OutdoorAnimals` skeleton whose `LocationName` is **that building's** `LocationName` and whose tasks are `outdoorAnimalTasks`.
3. After the loop, if `outdoorAnimalTasks` contains `CollectAnimalProducts`, add one `FarmForage` skeleton (`LocationName = "Farm"`, tasks = `{CollectAnimalProducts}`).
4. Greenhouse / OutdoorCrops / OutdoorClearing batches are appended afterwards, unchanged.

### W2 — Per-building grazing pass (runtime, `ShiftOrchestrator`)
- When an `OutdoorAnimals` batch begins, build/refresh its animal work for the **single** home key `{ batch.LocationName }` (not all buildings). No ground-forage scan here.
- Grazing animals are matched to the building via the existing `AnimalTaskHandler` home keys (`homeInterior.NameOrUniqueName` + legacy/type-name fallbacks). The worker routes to those animals wherever they currently roam on the farm (FR-T09-04: never skipped for distance).

### W3 — Farm-wide forage pass (runtime, `ShiftOrchestrator`)
- When the `FarmForage` batch begins, scan the whole farm for ground forage (`CollectAnimalProducts`) tile work; no animal pet/collect work.
- Retain the late pre-completion rescan (truffles keep spawning through the day) on this batch (FR-T09-06).

## Data Flow
- Input: `WorkScopeSet` (selected animal buildings, greenhouse, outdoor) + enabled `TaskKind` set.
- `ShiftPlanBuilder` → ordered `IReadOnlyList<WorkBatch>` skeletons (empty tile/animal work).
- `ShiftOrchestrator.BuildInitialBatches` fills skeletons: `AnimalBuilding` (interior, filled on entry), `OutdoorAnimals` (per-building grazing animal work), `FarmForage` (whole-farm forage tile work).
- No persisted data; nothing leaves the shift runtime.

## Edge Cases
- **Single selected building**: one `AnimalBuilding` + one `OutdoorAnimals(building)` (+ `FarmForage` if Collect enabled). Behavior equivalent in coverage to today.
- **Only `FeedAnimals` enabled**: no `OutdoorAnimals` and no `FarmForage` (grazing/forage need pet/collect). Only per-building interior feed batches — unchanged from today.
- **Only `PetAnimals` enabled (no Collect)**: per-building `OutdoorAnimals` grazing pet passes, but **no** `FarmForage` (truffles are collect-type).
- **Legacy type-name selection (pre-TODO-08)**: two same-type buildings share a `LocationName`; each building's grazing pass matches all that type's grazing animals. Re-servicing is **idempotent** (`ShouldPet` checks `wasPet`; collect clears `currentProduce`), so no double-work or double-output. Documented, not a regression.
- **Animal housed during its building's indoor pass**: serviced indoors; not re-serviced outdoors (not in `farm.Animals` at grazing time, and pet/collect idempotent). No double-service.
