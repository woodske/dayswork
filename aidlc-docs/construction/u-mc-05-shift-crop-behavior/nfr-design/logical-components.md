# U-MC-05 Logical Components

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — NFR Design
**Status**: Review required

## New components

| Component | Project | Responsibility |
|---|---|---|
| `ManagedCropActionMap` (static) | `Dayswork.Core/Crops` | Pure, total map: `ManagedCropActionKind` (+ debris tool) → `WorkActionKind`/`WorkerTool`/is-tool-gated. |
| `ManagedZoneTileSet` (static helper) | `Dayswork.Core/Crops` | Pure `IsInManagedZone(location, tile, assignments)` predicate for coexistence. |
| `ManagedCropFieldReader` | `Dayswork/Orchestration` | Live `GameLocation` → pure `FieldState`/`TileState` (HoeDirt/crop/debris/watered/`Diggable`). |
| `ManagedCropShiftRunner` (or `ShiftOrchestrator.ManagedCrops.cs` partial) | `Dayswork/Orchestration` | Drives per-tile beats: supply load, plan build, navigate, capability gate, animate, mutate, spend energy, notify, settle leftovers. |
| `CropHudNotifier` (minimal slice, M-29) | `Dayswork/UI` or `Dayswork/Integration` | i18n HUD notices: tool-skip, fertilizer-unavailable. |

## Extended components

| Component | Change |
|---|---|
| `WorkActionKind` | + `HoeSwing`, `PlantSeed`, `ApplyFertilizer`. |
| `WorkerTool` | + `Hoe`. |
| `BatchKind` | + `ManagedCrops`. |
| `ShiftPlanBuilder.BuildBatchPlan` | Emit `ManagedCrops` batch from `WorkScopeSet.ManagedCrops` (open-farm), ordered before general outdoor batches. |
| `ShiftOrchestrator` | Recognize/dispatch `ManagedCrops` batch to the runner; honor boundary/cap/stamina-stop per beat; carry the batch's `CropZoneAssignment`s. |
| `WorkAreaScanner` | Exclude managed-zone tiles for the location (coexistence) via `ManagedZoneTileSet`. |
| `ConfigDefaults` / `IConfigSnapshot` / GMCM | + default + configurable costs for the three new action kinds. |
| `CropPlanDraft` / `ManageCropsMenu` | Surface `ClearDebrisBeforeTilling` / `ClearDeadPlants` toggle checkboxes. |

## Reused (unchanged)

`CropShiftPlanner`, `PlantingViabilityCalculator`, `CropSupplyPlanner`, `StoreResolver`,
`FieldState`, `TileState`, `TileAction`, `ManagedCropShiftPlan`, `SupplyInventory`,
`CropDescriptor`, `CropZoneAssignment`, `CropPlan`, `ManagedCropWorkScope` (U-MC-01);
`WorkerEnergyLedger`, `WorkUnitBoundaryClassifier`, `ToolSwapAnimator`,
`WorkerMovementDriver`, `CapabilityEvaluator`/`CapabilityMatrix`, deposit/overflow pipeline,
input chest + `ChestResolver` (U-MC-02).

## Dependency notes

- No new project references; no new NuGet dependency.
- No persistence schema change.
- Forward seams (U-MC-06 store trip, U-MC-07 per-zone routing / greenhouse) attach to the
  existing `SupplyInventory`/`PurchaseLine` and deposit-step contracts without rework.

## Extension Compliance
| Extension | Status |
|---|---|
| Security Baseline | N/A (disabled). |
| Property-Based Testing | Compliant, full — pure `ManagedCropActionMap`/`ManagedZoneTileSet` + reused planners carry blocking properties; adapters example-covered. |
