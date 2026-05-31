# Code Summary — u-t09-animal-ordering (TODO-09 Per-Building Animal Work Ordering)

## Outcome
The worker now performs **all of a single animal building's animal work — its interior housed animals, then that building's own grazing animals — before moving to the next building**, instead of doing every interior first and then one combined outdoor pass. Farm-wide truffle forage is collected once at the end by a dedicated `FarmForage` pass. Pure re-ordering: no animal work is dropped or duplicated; no save/config/UI change.

## Files Modified
- **`Dayswork.Core/Shifts/WorkBatch.cs`** — Added `BatchKind.FarmForage` (between `OutdoorAnimals` and `Greenhouse`). Documented `OutdoorAnimals` as now per-building (keyed by `WorkBatch.LocationName`).
- **`Dayswork.Core/Shifts/ShiftPlanBuilder.cs`** — `BuildBatchPlan` now emits, per building (existing `LocationName`/`Tier` order), an `AnimalBuilding` batch immediately followed by an `OutdoorAnimals` grazing batch scoped to that building (when non-feed animal tasks are enabled); after all buildings, one `FarmForage("Farm")` batch when `CollectAnimalProducts` is enabled.
- **`Dayswork/Orchestration/ShiftOrchestrator.cs`**:
  - `BuildInitialBatches`: removed the now-unused `selectedAnimalHomes`; `OutdoorAnimals` case fills grazing animal work from the single home `{ batch.LocationName }` (no forage scan); new `FarmForage` case fills whole-farm forage tile work (no animal work).
  - `BeginCurrentBatch`: routes `OutdoorAnimals` → `RefreshBuildingGrazingWork`, `FarmForage` → `RefreshFarmForageWork`.
  - Split `RefreshOutdoorAnimalWork` into `RefreshBuildingGrazingWork` (single-building grazing rebuild) and `RefreshFarmForageWork` (whole-farm truffle rescan).
  - Re-pointed the late-truffle pre-completion rescan from `OutdoorAnimals` to `FarmForage` (renamed `TryRescanOutdoorAnimalProductsBeforeBatchComplete` → `TryRescanFarmForageBeforeBatchComplete`); updated log tags.
  - Extended the "all-outdoor-empty ⇒ no worker" guard to include `FarmForage`.
- **`Dayswork.Tests/Shifts/ShiftPlanBuilderTests.cs`** — Rewrote the mixed-scope example for the interleaved per-building order; added example tests EX-T09-1..4 (two buildings Feed/Pet; +Collect adds `FarmForage`; single building Collect-only; Feed-only ⇒ interior batches only); replaced the Kind-monotonic PBT property with `AnyScopeShape_ProducesPerBuildingGroupedPlan` asserting P-T09-1..6 (per-building pairing/contiguity, building order, single forage positioned last among animal work, grazing-batch count, bounded families, empty skeletons); added `MaxIndex`/`MinIndex` helpers.

## Files Unchanged (by design)
`AnimalTaskHandler` (grazing→home attribution + idempotent pet/collect reused as-is), `WorkAreaScanner`, `BuildingWorkNavigator`, persistence/DTOs, config/GMCM/i18n. `Dayswork.Tests/U22/ScopeDrivenRuntimeAlignmentTests.cs` needed no change (single-building Pet-only still yields `[AnimalBuilding, OutdoorAnimals]`).

## Key Decisions / Notes
- **No double-service**: an animal is either housed (`AnimalHouse.Animals`) or grazing (`Farm.Animals`) at a given instant; pet/collect are idempotent (`ShouldPet` ⇒ `wasPet`; collect clears `currentProduce`), so the legacy shared type-name selection edge re-matches safely (BR-T09-05 / P-T09-04).
- **Truffles without buildings preserved**: `FarmForage` is gated only on `CollectAnimalProducts` (independent of building selection), matching the prior combined batch's farm-wide forage availability.
- **Building order unchanged** (`LocationName` ordinal, then `Tier`); proximity routing explicitly out of scope (FR-T09-07 / TODO-09 note).

## Verification
- `dotnet build Dayswork.sln -p:EnableModDeploy=false` → **0 warnings / 0 errors**.
- `dotnet test Dayswork.sln -p:EnableModDeploy=false` → **382 passed / 1 expected skip / 0 failed** (was 378; +4 example tests, property rewritten).
- Manual in-game routing confirmation (≥2 spread-out buildings: full per-building servicing before moving on; truffles still collected) is part of the Build & Test stage.
