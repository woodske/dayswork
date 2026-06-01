# Code Generation Plan — u-t09-animal-ordering (TODO-09 Per-Building Animal Work Ordering)

**Single source of truth for TODO-09 code generation.** Brownfield: modify existing files in place (never create `*_new.cs`). Application code at workspace root; markdown summaries under `aidlc-docs/construction/u-t09-animal-ordering/code/`.

**Stories**: refinement of S-23 (animal buildings). **Requirements**: FR-T09-01..08, NFR-T09-01..06. **Rules**: BR-T09-01..11, properties P-T09-1..6.

**Design recap**: `ShiftPlanBuilder` emits, per building, an `AnimalBuilding` batch immediately followed (when non-feed animal tasks enabled) by an `OutdoorAnimals` batch scoped to that building's home key; after all buildings, one `FarmForage` batch (new `BatchKind`) for the whole-farm truffle sweep when `CollectAnimalProducts` is enabled. `ShiftOrchestrator` fills the per-building grazing pass from a single-home set and the forage pass from a whole-farm scan, and moves the late-truffle rescan onto `FarmForage`.

---

## Business Logic Generation (Core)

- [x] **Step 1 — Add `FarmForage` to `BatchKind`.** In `Dayswork.Core/Shifts/WorkBatch.cs`, insert `FarmForage` between `OutdoorAnimals` and `Greenhouse`. *(domain-entities; BR-T09-06)*

- [x] **Step 2 — Rework `ShiftPlanBuilder.BuildBatchPlan` ordering.** In `Dayswork.Core/Shifts/ShiftPlanBuilder.cs`:
  - For each selected building (existing `OrderBy(LocationName).ThenBy(Tier)`), add the `AnimalBuilding` skeleton, then — when `outdoorAnimalTasks.Count > 0` — add an `OutdoorAnimals` skeleton with `locationName = building.LocationName` and `tasks = outdoorAnimalTasks`.
  - After the building loop, when `outdoorAnimalTasks.Contains(TaskKind.CollectAnimalProducts)`, add one `FarmForage` skeleton (`locationName = "Farm"`, `tasks = new[] { TaskKind.CollectAnimalProducts }`).
  - Remove the old single trailing `OutdoorAnimals("Farm")` batch.
  - Greenhouse / OutdoorCrops / OutdoorClearing blocks unchanged. *(BR-T09-01..04, -06, -09, -10)*

## Business Logic Unit Testing (Core) — `Dayswork.Tests`

- [x] **Step 3 — Update `ShiftPlanBuilderTests`.**
  - Rename + rewrite `MixedScopes_AreOrdered...` to expect the interleaved order `[AnimalBuilding(Barn), OutdoorAnimals(Barn), AnimalBuilding(Coop), OutdoorAnimals(Coop), Greenhouse, OutdoorCrops, OutdoorClearing]` (the existing mixed test enables Feed/Pet/Harvest/ClearWeeds, no Collect → **no** `FarmForage`).
  - Rewrite the PBT property `AnyScopeShape_ProducesBoundedOrderedSkeletons`: drop the Kind-monotonic check; assert P-T09-1 (each `AnimalBuilding` immediately followed by an `OutdoorAnimals` with the same `LocationName` when non-feed animal tasks enabled), P-T09-2 (AnimalBuilding `LocationName`s == sorted buildings), P-T09-3 (≤1 `FarmForage`, only when Collect enabled, positioned after all animal batches and before greenhouse/crops/clearing), P-T09-4 (`OutdoorAnimals` count == building count when non-feed animal tasks enabled else 0), P-T09-5 (bounded greenhouse/crops/clearing), P-T09-6 (empty skeletons).

- [x] **Step 4 — Add focused example tests** (new file `Dayswork.Tests/Shifts/PerBuildingAnimalOrderingTests.cs` or extend `ShiftPlanBuilderTests`): EX-T09-1..5 from business-rules.md (Feed/Pet two buildings; +Collect adds `FarmForage`; one building Collect-only; Feed-only ⇒ no grazing/forage; mixed with greenhouse/outdoor). Assert both `Kind` sequence and `LocationName` sequence.

## Runtime Generation (Mod)

- [x] **Step 5 — `ShiftOrchestrator.BuildInitialBatches` switch** (`Dayswork/Orchestration/ShiftOrchestrator.cs` ~line 604):
  - `OutdoorAnimals` case: build **per-building** grazing animal work via `BuildAnimalWork(farm, new HashSet<string>(StringComparer.Ordinal){ batch.LocationName }, batchTasks)`; set `TileWork = Array.Empty<WorkItem>()` (no forage here).
  - Add `FarmForage` case: `TileWork = ScanWholeLocation(farm, {CollectAnimalProducts}, snapshot, _farmExitTile, OutputScopeProvenance.AnimalBuilding(string.Empty))`; `AnimalWork = Array.Empty<AnimalWorkItem>()`.
  - *(BR-T09-03, -04, -07)*

- [x] **Step 6 — `BeginCurrentBatch` refresh hook** (~line 732): replace `if (batch.Kind == OutdoorAnimals) batch = RefreshOutdoorAnimalWork(...)` with:
  - `OutdoorAnimals` → `RefreshBuildingGrazingWork(batch, farm)` (single-home grazing rebuild, no forage).
  - `FarmForage` → `RefreshFarmForageWork(batch, farm)` (whole-farm forage rescan, no animal work).

- [x] **Step 7 — Split `RefreshOutdoorAnimalWork`** into the two methods above:
  - `RefreshBuildingGrazingWork`: `selectedAnimalHomes = { batch.LocationName }`; `AnimalWork = BuildAnimalWork(...)`; `TileWork = Array.Empty`.
  - `RefreshFarmForageWork`: `TileWork = ScanWholeLocation(... CollectAnimalProducts ...)`; `AnimalWork = Array.Empty`. Keep the "stale at batch start" rationale comment.

- [x] **Step 8 — Re-point late-truffle rescan.** In `TryRescanOutdoorAnimalProductsBeforeBatchComplete` (~line 901), change the kind guard from `BatchKind.OutdoorAnimals` to `BatchKind.FarmForage` (forage now owns the rescan). *(BR-T09-08)*

- [x] **Step 9 — Extend the all-outdoor-empty / no-work guard** (~line 500): add `BatchKind.FarmForage` to the kinds that count as empty-outdoor so a plan of only-empty outdoor/forage batches still resolves to "no worker spawned" / batch completes. Confirm `BatchRequiresInteriorEntry` stays `AnimalBuilding or Greenhouse` (new kinds are outdoor — correct).

## Runtime / Integration Testing

- [x] **Step 10 — Update `Dayswork.Tests/U22/ScopeDrivenRuntimeAlignmentTests.cs` if needed.** The single-Barn Pet-only test expects `[AnimalBuilding, OutdoorAnimals]` — still valid (one building → pair; no Collect → no `FarmForage`). Verify no change required; add a Collect-enabled assertion if useful (`[AnimalBuilding, OutdoorAnimals, FarmForage]`).

- [x] **Step 11 — Build & test.** `dotnet build Dayswork.sln /p:EnableModDeploy=false` (expect 0/0); `dotnet test Dayswork.sln /p:EnableModDeploy=false` (expect green, prior 378 baseline + new tests). Fix any fallout.

## Documentation

- [x] **Step 12 — Code summary.** Write `aidlc-docs/construction/u-t09-animal-ordering/code/code-summary.md` (files modified/created, decisions, verification numbers). Update `aidlc-docs/aidlc-state.md` (TODO-09 progress) and append `audit.md`. Update the TODO-09 entry note in `aidlc-state.md` Open TODOs toward CLOSED on approval.

- [x] **Step 13 — Deploy (optional, when game closed).** `dotnet build Dayswork.sln` to copy to the Mods folder for the manual in-game routing check (part of Build & Test). Manual confirmation: with ≥2 spread-out buildings, worker fully services one building (inside + its grazing animals) before walking to the next; truffles still collected.

---

## Files in scope
- **Modify**: `Dayswork.Core/Shifts/WorkBatch.cs`, `Dayswork.Core/Shifts/ShiftPlanBuilder.cs`, `Dayswork/Orchestration/ShiftOrchestrator.cs`, `Dayswork.Tests/Shifts/ShiftPlanBuilderTests.cs`, (possibly) `Dayswork.Tests/U22/ScopeDrivenRuntimeAlignmentTests.cs`.
- **Create**: `Dayswork.Tests/Shifts/PerBuildingAnimalOrderingTests.cs` (if not folded into `ShiftPlanBuilderTests`), `aidlc-docs/construction/u-t09-animal-ordering/code/code-summary.md`.
- **No changes**: `AnimalTaskHandler`, `WorkAreaScanner`, `BuildingWorkNavigator`, persistence/DTO, config/GMCM/i18n.

## Verification gates
- Build 0 warnings / 0 errors; all tests green.
- PBT properties P-T09-1..6 pass.
- Manual: per-building grouping visible in-game; truffles still collected; no animal left unserviced.
