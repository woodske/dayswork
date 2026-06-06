# U-MC-05 Tech Stack Decisions

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — NFR Requirements

## Decision: reuse the existing stack and seams (no new dependencies)

| Concern | Decision |
|---|---|
| Language / runtime | C# / .NET 6 targeting Stardew Valley 1.6 + SMAPI (unchanged). |
| Pure decision logic | `Dayswork.Core` — reuse U-MC-01 `CropShiftPlanner`/`PlantingViabilityCalculator`/`CropSupplyPlanner`/`StoreResolver`; add pure `ManagedCropActionMap` + `ManagedZoneTileSet` seams. |
| Runtime execution | Reuse `ShiftOrchestrator` tick/intent state machine, `WorkerMovementDriver`, `ToolSwapAnimator` pacing, `WorkerEnergyLedger`, deposit/overflow pipeline. Add `BatchKind.ManagedCrops` + a `ManagedCropShiftRunner` (partial/adapter). |
| Field intake | New thin `ManagedCropFieldReader` over live `GameLocation`/`HoeDirt`/`Crop`/Back-layer `Diggable` — no new library. |
| Capability/energy | Reuse `CapabilityEvaluator`/`CapabilityMatrix`; extend `WorkerTool` (`Hoe`) and `WorkActionKind` (`HoeSwing`/`PlantSeed`/`ApplyFertilizer`) with config/GMCM costs. |
| Chests / inventory | Reuse U-MC-02 input chest + `ChestResolver`; read supply from the input chest, settle leftovers back. |
| Config / GMCM | Extend `ConfigDefaults`/`IConfigSnapshot`/GMCM with the three new action costs (existing config plumbing). |
| Persistence | None — reads existing V3 `CropPlan`; no schema change. |
| Testing | xUnit + FsCheck.Xunit (already referenced) — PBT-09 compliant. |

## Rationale
This unit is integration of already-built pure logic into the established runtime, plus
three small Core extensions (action kinds, tool, batch kind) and two thin adapters (field
reader, runner). No async pipeline, job system, or external dependency is warranted; adding
one would violate NFR-MC-09 and the project's pure-Core/thin-adapter convention.
