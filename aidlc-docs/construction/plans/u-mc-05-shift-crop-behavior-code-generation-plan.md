# Code Generation Plan — U-MC-05 Shift Crop Behavior

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — Code Generation
**Package order**: `Dayswork.Core` → `Dayswork` → `Dayswork.Tests`

Boundary (recap): open farm only; supply from the **input chest** only (shopping = U-MC-06);
harvest via the existing output-chest/overflow pipeline (per-zone routing + greenhouse =
U-MC-07).

## Part 2 — Generation steps

### Core (`Dayswork.Core`)
- [x] **S1** `WorkActionKind` += `HoeSwing`, `PlantSeed`, `ApplyFertilizer`.
- [x] **S2** `ConfigDefaults` += default costs `HoeSwing=2`, `PlantSeed=1`, `ApplyFertilizer=1`.
- [x] **S3** `WorkerTool` += `Hoe` (no `ForTask` change; managed actions use the new map).
- [x] **S4** `BatchKind` += `ManagedCrops`.
- [x] **S5** New pure `ManagedCropActionMap` (Crops): `EnergyKind(kind, debrisTool?)`,
      `Tool(kind, debrisTool?)`, `IsToolGated(kind)` — total, deterministic.
- [x] **S6** New pure `ManagedZoneTileSet` (Crops): `IsInManagedZone(location, tile, assignments)`
      + `Tiles(assignments, location)` helper.
- [x] **S7** `ShiftPlanBuilder.BuildBatchPlan` — emit one `ManagedCrops` batch per open-farm
      managed location from `WorkScopeSet.ManagedCrops`, ordered before `OutdoorCrops`/
      `OutdoorClearing`. (Managed batch carries no `TaskKind` tile work; the runtime holds the
      assignments out-of-band.)

### Mod runtime (`Dayswork`)
- [x] **S8** `ManagedCropFieldReader` — live `GameLocation` → pure `FieldState`/`TileState`
      (HoeDirt/crop/dead/debris/watered/`Diggable`) for in-zone tiles.
- [x] **S9** `ShiftOrchestrator.ManagedCrops.cs` partial — managed-crop execution:
      load supply snapshot from the input chest; build per-assignment `ManagedCropShiftPlan`
      via `CropShiftPlanner` (null stock); concat ordered `TileAction` queue; fertilizer-
      unavailable notice; sequential per-tile beats (nav → applicability check → capability
      gate → animate → mutate → spend energy → advance).
- [x] **S10** New intent `IntentPerformManagedCropAction(TileAction)` + dispatch in
      `OnUpdateTicked`; `HandleManagedCropAction` beat handler.
- [x] **S11** `HandleMovement` — managed branch: on arrival set the managed action intent; on
      nav failure skip the action and continue.
- [x] **S12** `BeginCurrentBatch`/`CompleteCurrentBatch` — route `BatchKind.ManagedCrops` to the
      runner; managed batch completion advances the batch index.
- [x] **S13** Managed world mutations: `InvokeManagedTill` (`makeHoeDirt`),
      `InvokeManagedFertilize`/`InvokeManagedSeed` (`HoeDirt.plant`, consume 1 from input
      chest, guarded), reuse `InvokeHarvest`/`InvokeWater` and clear invokers for ClearDebris
      (tool resolved from live tile; dead crop → `destroyCrop`). Guard `Game1.player` state via
      the existing `InvokeTaskActionGuarded` pattern for harvest.
- [x] **S14** Coexistence: thread managed zones into `WorkAreaScanner.ScanZones` for the farm and
      skip tiles inside any managed zone (general `WaterCrops`/`HarvestCrops` exclusion).
- [x] **S15** `CropHudNotifier` (minimal) — i18n HUD notices: tool-skip, fertilizer-unavailable;
      add i18n keys.
- [x] **S16** GMCM + `ConfigSnapshotFactory`/`ModConfig` — surface the three new action costs;
      add i18n labels. Verify config round-trips the new keys.
- [~] **S17** `ManageCropsMenu`/`CropPlanDraft` toggle checkboxes — **DEV-MC-05-01: deferred.**
      The implemented `CropPlan` carries only `Assignments` (no toggle fields), so persisting these
      per-plan would need a DTO/serialization change, and the spec calls them a *global* toggle.
      The runtime honors both toggles at their **default-ON** behavior this unit; the configurable
      OFF switch (global GMCM flag) is a small follow-up. i18n label keys were still added.
- [x] **S18** Worker-supply consumption helper reads/decrements the input chest; harvest output
      flows through `_ctx.Buffer` → existing deposit/overflow (output-chest fallback).

### Tests (`Dayswork.Tests`)
- [x] **S19** FsCheck: `ManagedCropActionMap` totality/determinism; `ManagedZoneTileSet` disjoint
      partition (a tile excluded iff in a managed zone).
- [x] **S20** xUnit: `ShiftPlanBuilder` emits a `ManagedCrops` batch ordered before outdoor
      crop/clearing; empty plan emits none.
- [x] **S21** xUnit: action map mapping table (each kind → expected tool/energy/gated); GMCM cost
      defaults present.

### Verify + close
- [x] **S22** `dotnet build /p:EnableModDeploy=false` 0/0; `dotnet test` green.
- [x] **S23** `dotnet build` deploy to Mods; write code-summary; update state + audit; present
      playtest checklist (stop at playtest gate).

## Extension Compliance
- Security Baseline: N/A (disabled).
- PBT full mode: S19 carries the blocking pure properties; reused U-MC-01 planner properties
  remain green; runtime adapters example-covered (S20/S21).
