# Code Summary — U-MC-05 Shift Crop Behavior

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — Code Generation
**Status**: Complete; review required (in-game playtest)

## Summary

U-MC-05 wires the pure U-MC-01 crop planners into the live shift runtime so the farmhand
actually prepares, plants, and self-heals a contract's managed crop zones each shift on the
**open farm**. Supply comes from the **input chest only** (town shopping is U-MC-06); harvest
settles via the existing output-chest/overflow pipeline (per-zone routing + greenhouse/shed are
U-MC-07).

The managed-crop path runs as its own `BatchKind.ManagedCrops` batch (ordered before the general
outdoor crop/clearing passes) that builds a per-zone ordered `TileAction` plan from the live field
state and executes each action as its own paced beat — **harvest → clear debris → till → fertilize
→ seed → water** — gated by capability and energy, exactly as the pure planner orders it.

## Created files

**Core (`Dayswork.Core`)**
- `Crops/ManagedCropActionMap.cs` — pure total map: action (+ live debris tool) → `WorkActionKind`
  / `WorkerTool` / is-tool-gated.
- `Crops/ManagedZoneTileSet.cs` — pure `IsInManagedZone(location, tile, assignments)` coexistence
  predicate.

**Mod (`Dayswork`)**
- `Orchestration/ManagedCropFieldReader.cs` — live `GameLocation` → pure `FieldState`/`TileState`
  (HoeDirt/crop/dead/debris/watered/`Diggable`); bare non-diggable tiles excluded.
- `Orchestration/ShiftOrchestrator.ManagedCrops.cs` — managed-crop execution partial: input-chest
  supply read, per-assignment `CropShiftPlanner.Plan` (null shop stock), ordered `TileAction` queue
  with debris/dead-plant toggle filtering, per-tile beats (nav → applicability re-check →
  capability gate → animate → mutate → spend energy), input-chest consumption, harvest buffering,
  fertilizer-unavailable/tool-skip notices.
- `Integration/CropHudNotifier.cs` — per-shift-deduped i18n HUD notices (tool-skip,
  fertilizer-unavailable).

**Tests (`Dayswork.Tests`)**
- `ManageCrops/ManagedCropActionMapTests.cs` — mapping table, ClearDebris-by-tool, totality +
  determinism over every action×tool, not-tool-gated for plant/fertilize/harvest.
- `ManageCrops/ManagedZoneTileSetTests.cs` — inside/edge/outside/other-location/empty + FsCheck
  "managed iff inside the rectangle" property.
- `Shifts/ManagedCropBatchPlanTests.cs` — managed batch emitted for farm; ordered before
  OutdoorCrops/OutdoorClearing; empty plan emits none.

## Modified files

- `Core/Energy/WorkActionKind.cs` — `+HoeSwing, PlantSeed, ApplyFertilizer`.
- `Core/Config/ConfigDefaults.cs` — default costs `HoeSwing=2, PlantSeed=1, ApplyFertilizer=1`
  (flow automatically through `ModConfig` → `RuntimeConfigSnapshotMapper` → factory).
- `Core/Domain/WorkerTool.cs` — `+Hoe`.
- `Core/Shifts/WorkBatch.cs` — `BatchKind +ManagedCrops`.
- `Core/Shifts/ShiftPlanBuilder.cs` — emit one `ManagedCrops` batch per open-farm managed location,
  ordered before general outdoor batches.
- `Core/Shifts/ShiftIntent.cs` — `+IntentPerformManagedCropAction(TileAction)`.
- `Orchestration/ShiftOrchestrator.cs` — dispatch the new intent; intercept `ManagedCrops` batch in
  `BeginCurrentBatch`; pass-through case in `BuildInitialBatches`; thread managed farm zones into
  the outdoor scans (coexistence); reset managed state in `StartShift`/`ClearWorker`; reset HUD
  notifier per shift.
- `Orchestration/ShiftOrchestrator.Movement.cs` — managed-crop branches for nav-failure (skip) and
  arrival (perform action).
- `Orchestration/WorkAreaScanner.cs` — `ScanZones` `excludedZones` param skips managed-zone tiles
  (FR-MC-28 coexistence).
- `Integration/GMCMRegistrar.cs` — `WorkActionKey` cases for the three new costs.
- `i18n/default.json` — HUD notice keys, GMCM cost labels, toggle label keys.

## Behavior delivered

- Per-tile dependency order with each action its own beat at `WorkerActionAnimationMs`
  (FR-MC-10); seed/fertilizer atomicity via the pure planner (FR-MC-11); harvest-first enabling
  same-shift replant + per-shift gap-fill of empty viable tiles honoring per-season `AutoReplant`
  (FR-MC-24).
- End-of-season viability gate with fertilized growth time on the open farm (FR-MC-21);
  fertilizer-entirely-unavailable → zone planting skipped + HUD notice (FR-MC-22).
- Re-till of reverted tiles, per-tile `Diggable` gating, debris clearing before tilling, dead-plant
  clearing within managed zones (FR-MC-25/26/27).
- New `WorkerTool.Hoe`; capability/energy mapping: till→Hoe/`HoeSwing`, water→Can/`WaterTile`,
  harvest→`HarvestCrop`, clear→Axe/Pickaxe/Scythe with `CapabilityMatrix` level gating; plant &
  fertilize gate only on item availability (FR-MC-30/31). Missing/under-leveled tool → skip that
  action/tile + HUD notice (FR-MC-32). New configurable non-zero energy costs (FR-MC-40/42).
- Coexistence: managed-zone tiles excluded from general Water/Harvest/clearing scans (FR-MC-28).
- Supply read from / consumed from the input chest; harvest to the office output chest via the
  existing pipeline (FR-MC-29 fallback only this unit; NFR-MC-03 item safety).

## Deviations

- **DEV-MC-05-01** — the "clear debris before tilling" / "clear dead plants" global toggles are
  honored at their **default-ON** behavior; the configurable OFF switch is deferred. The
  implemented `CropPlan` carries only `Assignments` (no toggle fields), and the spec frames these
  as a *global* toggle, so persisting them per-plan was out of proportion for this unit. i18n label
  keys were added; a follow-up can add a global GMCM flag. (FR-MC-26/27 default behavior delivered.)

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` — 0 warnings / 0 errors.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` — 454 passed / 1 expected skip / 0 failed (+19).
- `dotnet build Dayswork.sln` — deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.

## Stages skipped (with rationale)

- **Infrastructure Design** — SMAPI mod, no cloud/IaC.

## Extension compliance

- **Security Baseline**: N/A (disabled for Manage Crops).
- **Property-Based Testing (full mode)**: compliant — pure `ManagedCropActionMap` (totality +
  determinism) and `ManagedZoneTileSet` (disjoint partition) carry FsCheck/example properties; the
  reused U-MC-01 planner properties remain green; the runtime adapter is example-covered at the
  live-API boundary.

## Playtest checklist (in-game)

1. **Author + first shift**: create a contract with a managed crop group (season crop + zone on the
   open farm), stock the input chest with seeds (and fertilizer if configured), start a shift →
   farmhand walks the zone, tills bare diggable tiles, fertilizes then seeds then waters, each as a
   distinct paced beat. Verify the hoe/seed/fertilize swings and stamina drain.
2. **Harvest-first + replant**: with a mature crop and per-season Replant on, confirm the tile is
   harvested first, output reaches the office **output chest**, and (supply permitting) the tile is
   replanted the same shift.
3. **Viability gate**: late in the season, confirm the farmhand does **not** plant crops that can't
   mature before season end (fertilized growth time respected).
4. **Atomicity / fertilizer-unavailable**: configure a fertilizer but leave it out of the input
   chest → no tiles planted in that zone + a single HUD "fertilizer unavailable" notice; with
   partial fertilizer, only `min(seeds, fertilizer)` tiles complete.
5. **Capability skip**: place a Steel-gated boulder / oversized debris on a managed tile with a weak
   pickaxe → that tile is skipped (HUD notice once), the rest of the contract proceeds.
6. **Toggles (default ON)**: confirm debris is cleared before tilling and dead plants are cleared
   within the managed zone (the configurable OFF switch is deferred — DEV-MC-05-01).
7. **Coexistence**: with `WaterCrops`/`HarvestCrops` also enabled and an outdoor zone overlapping
   the managed zone, confirm managed tiles are serviced only by the managed path (no double-action).
8. **No-plan regression**: a contract with no crop plan behaves exactly as before.
