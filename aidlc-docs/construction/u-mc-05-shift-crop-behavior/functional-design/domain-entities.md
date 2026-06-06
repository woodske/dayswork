# U-MC-05 Domain Entities

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — Functional Design
**Status**: Review required

U-MC-05 adds **runtime** types and small extensions to existing ones. The pure crop-plan
domain and planners already exist (U-MC-01); this unit does not redefine them.

## New / extended pure types (`Dayswork.Core`)

### WorkActionKind (extend)

Add three new kinds (`Dayswork.Core/Energy/WorkActionKind.cs`):

```csharp
public enum WorkActionKind
{
    WaterTile, HarvestCrop, HarvestFruit, FeedAnimal, PetAnimal, CollectAnimalProduct,
    AxeSwing, PickaxeSwing, ScytheSwing,
    HoeSwing,          // till
    PlantSeed,         // plant seed
    ApplyFertilizer,   // apply fertilizer
}
```

- Each has a **configurable non-zero** cost in `WorkerEnergyProfile.ActionCosts`.
- Default costs added to `ConfigDefaults` (recommended starting values: `HoeSwing = 2`,
  `PlantSeed = 1`, `ApplyFertilizer = 1` — tuned via playtest), surfaced in GMCM.

### WorkerTool (extend)

Add `Hoe` (`Dayswork.Core/Domain/WorkerTool.cs`):

```csharp
public enum WorkerTool { None, WateringCan, Scythe, Pickaxe, Axe, MilkPail, Shears, Hoe }
```

`WorkerToolExtensions.ForTask(TaskKind)` is unchanged for existing task kinds. A new pure
mapping for managed-crop actions (see `ManagedCropActionMap` below) maps the till action
to `WorkerTool.Hoe`.

### BatchKind (extend)

Add `ManagedCrops` (`Dayswork.Core/Shifts/WorkBatch.cs`):

```csharp
public enum BatchKind
{
    AnimalBuilding, OutdoorAnimals, FarmForage, Greenhouse, OutdoorCrops, OutdoorClearing,
    ManagedCrops,
}
```

The managed-crop batch carries the location name (`"Farm"` this unit) and its
`CropZoneAssignment`s for the location (passed through batch construction state, not as
`TaskKind` tile work — managed crops use their own per-tile action queue, not `WorkItem`).

### ManagedCropActionMap (new pure seam)

A pure, total mapping (`Dayswork.Core/Crops/ManagedCropActionMap.cs`) from a
`ManagedCropActionKind` (+ debris tool when relevant) to its energy/tool tuple:

```csharp
public static class ManagedCropActionMap
{
    public static WorkActionKind EnergyKind(ManagedCropActionKind kind, ...);
    public static WorkerTool Tool(ManagedCropActionKind kind, ...);
    public static bool IsToolGated(ManagedCropActionKind kind);  // false for Fertilize/PlantSeed/Harvest
}
```

Deterministic and exhaustive (PBT property: total + stable). ClearDebris resolves its tool
from the live debris type at the runtime boundary; the map exposes the per-tool branch so
the gate stays pure.

### ManagedZoneTileSet (new pure seam)

A pure predicate/set used for coexistence (`Dayswork.Core/Crops/ManagedZoneTileSet.cs` or a
static helper):

```csharp
public static bool IsInManagedZone(
    string locationName, TileCoord tile, IReadOnlyList<CropZoneAssignment> assignments);
```

PBT property: a tile is excluded from general crop work **iff** it lies in some managed
zone for that location (disjoint partition).

### Reused (no change): U-MC-01 planners and records

`CropShiftPlanner`, `PlantingViabilityCalculator`, `CropSupplyPlanner`, `StoreResolver`,
`FieldState`, `TileState`, `TileAction`, `ManagedCropActionKind`, `ManagedCropShiftPlan`,
`SupplyInventory`, `CropDescriptor`, `CropZoneAssignment`, `SeasonCropChoice`,
`ManagedCropWorkScope`, `CropPlan`.

## New runtime types (`Dayswork` mod project)

### ManagedCropFieldReader (M-27 support)

Thin live-world → pure adapter (`Dayswork/Orchestration/ManagedCropFieldReader.cs`):

- `FieldState Read(GameLocation location, GameDate date, IReadOnlyList<CropZoneAssignment> assignments)`
- Snapshots each in-zone tile's HoeDirt/crop/debris/watered/`Diggable` state into
  `TileState`. No mutation. `IsSeasonAgnosticLocation = false` (open farm).

### ManagedCropShiftRunner (M-27)

The runtime executor (`Dayswork/Orchestration/ManagedCropShiftRunner.cs` or a
`ShiftOrchestrator.ManagedCrops.cs` partial). Responsibilities:

- Load input-chest supply into a carried `SupplyInventory` + worker carried items.
- Build the per-zone `ManagedCropShiftPlan`s via `CropShiftPlanner` and concatenate the
  `TileAction` queue.
- Drive per-tile beats: navigate → capability gate → animate → mutate world → spend energy.
- Emit HUD notices (tool-skip, fertilizer-unavailable) via the existing HUD path
  (`CropHudNotifier` / `Game1.addHUDMessage`).
- Route harvest output through the existing inventory→output-chest deposit pipeline.
- Settle leftover carried supply back to the input chest at end of shift.

This runner reuses, not replaces, the `ShiftOrchestrator` tick/intent loop, energy ledger,
tool-swing animator, navigation driver, and deposit pipeline.

### CropHudNotifier (M-29, minimal slice)

A thin i18n-backed HUD notifier for managed-crop runtime notices (tool-skip,
fertilizer-unavailable). Purchase/fallback/festival notices are added in U-MC-06.

## Extensions to existing runtime types

- `ShiftPlanBuilder.BuildBatchPlan` — emit the `ManagedCrops` batch from
  `WorkScopeSet.ManagedCrops` (open-farm locations), ordered before general outdoor batches.
- `ShiftOrchestrator` — recognize and dispatch the `ManagedCrops` batch to the runner;
  honor the existing boundary/cap/stamina-stop rules per beat.
- `WorkAreaScanner` (general crop scan) — exclude managed-zone tiles for the location
  (coexistence), using `ManagedZoneTileSet`.
- `ConfigSnapshot`/`ConfigDefaults`/GMCM — add the three new action costs.
- `CropPlanDraft`/`ManageCropsMenu` — surface `ClearDebrisBeforeTilling`/`ClearDeadPlants`
  toggles (small UI addition; see frontend-components.md).

## Extension Compliance

| Extension | Status | Entity impact |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant (full) | New pure seams (`ManagedCropActionMap`, `ManagedZoneTileSet`) identify total/deterministic properties; runtime types are example-covered at the live-API boundary. |
