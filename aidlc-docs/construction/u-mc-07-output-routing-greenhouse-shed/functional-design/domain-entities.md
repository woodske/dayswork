# Domain Entities - U-MC-07 Output Routing + Greenhouse/Shed

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - Functional Design  
**Status**: Review required

U-MC-07 mostly extends existing Core and Mod types. It should avoid a save-schema bump by reusing `CropZoneAssignment.Mode`, `CropZoneAssignment.OutputChest`, `Zone.LocationName`, and existing destination DTOs.

## Extended Pure Types

### `OutputScopeFamily`

Add a managed-crop family so output routing can distinguish managed-zone harvest from ordinary `HarvestCrops`:

```csharp
public enum OutputScopeFamily
{
    Unknown,
    Outdoor,
    AnimalBuilding,
    Greenhouse,
    ManagedCrop,
}
```

### `OutputScopeProvenance`

Add a factory for stable managed-crop assignment provenance:

```csharp
public static OutputScopeProvenance ManagedCrop(string assignmentKey) =>
    new(OutputScopeFamily.ManagedCrop, assignmentKey);
```

The assignment key is stable within a persisted plan and should be built from:

- `CropZoneAssignment.GroupId` when present;
- `Zone.LocationName`;
- zone top-left and bottom-right tile coordinates.

### Managed-Crop Destination Map

A pure destination map is derived at shift/deposit planning time:

```csharp
IReadOnlyDictionary<OutputScopeProvenance, DestinationKey> ManagedCropDestinations
```

Entries are added only for assignments with a non-null `OutputChest`:

- key: `OutputScopeProvenance.ManagedCrop(assignmentKey)`
- value: `new ChestDestination(assignment.OutputChest)`

Assignments with no `OutputChest` intentionally have no map entry, allowing automatic output fallback.

### `DepositPlanner` Extension

Extend the pure planner to accept managed-crop provenance destinations before task-level destinations:

```csharp
DepositPlan Plan(
    IReadOnlyList<BufferedItem> snapshot,
    IReadOnlyDictionary<TaskKind, DestinationKey> taskAssignments,
    IReadOnlyDictionary<OutputScopeProvenance, DestinationKey> provenanceAssignments,
    TileCoord shippingBinTile,
    TileCoord workerStart,
    Func<TileCoord, TileCoord, int> distance);
```

The existing overload can delegate with an empty provenance map to preserve all current callers.

## Extended Crop Domain Types

### `CropGroupDraft`

Add group-level managed-crop location/mode state for authoring:

```csharp
public string LocationName { get; set; } = "Farm";
public CropAssignmentMode Mode { get; set; } = CropAssignmentMode.Seasonal;
public SeasonSlotDraft SeasonAgnosticSlot { get; }
```

Projected assignments:

- `Mode == Seasonal`: current four-season projection.
- `Mode == SeasonAgnostic`: one `SeasonCropChoice` carrier using a stable placeholder season; the planner ignores the season for this mode.

Hydration:

- `CropAssignmentMode.Seasonal` hydrates existing seasonal slots.
- `CropAssignmentMode.SeasonAgnostic` hydrates the single year-round slot and the group location from the assignment's zone.

### `CropZoneAssignment`

No persisted shape change is required. Existing fields already cover U-MC-07:

- `Zone.LocationName` identifies `Farm`, `Greenhouse`, or `Custom_GrandpasShedGreenhouse`.
- `Mode` identifies seasonal vs season-agnostic behavior.
- `Choices` carries the crop/fertilizer/replant choice.
- `OutputChest` carries per-zone output destination.
- `GroupId` helps build stable managed-crop provenance keys.

## Extended Runtime Types

### `TileAction`

Carry managed-crop provenance for harvest actions:

```csharp
public sealed record TileAction(
    string LocationName,
    TileCoord Tile,
    ManagedCropActionKind Kind,
    string? ItemId = null,
    bool RequiresDiggable = false,
    OutputScopeProvenance? OutputProvenance = null);
```

Only harvest actions need managed-crop provenance for deposit routing; other actions may leave it null.

### `ManagedCropFieldReader`

Extend the reader from farm-only to live-location aware:

```csharp
FieldState Read(
    GameLocation location,
    GameDate date,
    IReadOnlyList<CropZoneAssignment> assignments,
    bool isSeasonAgnosticLocation);
```

Responsibilities:

- use `location.NameOrUniqueName` for `FieldState.LocationName`;
- read `Diggable` from the live map;
- include existing crops and debris inside the selected zone;
- skip empty non-diggable tiles;
- set `IsSeasonAgnosticLocation` from the caller's location classification.

### `ShiftPlanBuilder`

Emit `BatchKind.ManagedCrops` for every distinct managed-crop location:

- `Farm`;
- `Greenhouse`;
- supported expansion greenhouse locations.

The batch remains a skeleton; the live runner builds its own per-tile action queue.

### `ShiftOrchestrator.ManagedCrops`

Extend the runner state so the active managed-crop batch is location-aware:

- resolve current batch location;
- enter vanilla or expansion greenhouse before planning;
- build managed-crop destination map for deposit planning;
- set `_pendingOutputProvenance` from the current tile action before harvest;
- re-enter the active non-farm managed-crop location after shopping deposits supplies;
- return to farm before global deposit/exit.

## Existing Types Reused Without Shape Change

- `ChestRef`
- `DestinationKey`, `ChestDestination`, `AutomaticOutputDestination`, `ShippingBinDestination`
- `BufferedItem`, `RoutedItemStack`, `DepositTrip`, `DepositPlan`
- `CropCatalog`, `CropCatalogProvider`
- `SveExpansionProfile` and expansion route descriptors
- `ExpansionCompatService`

## Testable Properties

| Entity | Property category | Property |
|---|---|---|
| `OutputScopeProvenance.ManagedCrop` | Invariant | Same assignment key creates equal provenance; different assignment keys create different provenance. |
| Managed-crop destination map | Invariant | Assignments with `OutputChest` produce exactly one chest destination entry; assignments without it produce no entry. |
| `DepositPlanner` provenance overload | Invariant | Provenance destination takes precedence over task destination only for matching managed-crop items. |
| `CropGroupDraft` season-agnostic projection | Round-trip | Project -> hydrate preserves location, crop, fertilizer, replant, output chest, and zones. |
| `ManagedCropFieldReader` | Invariant | `IsSeasonAgnosticLocation` is caller-controlled and tile inclusion remains deterministic for the same live snapshot. |
| `ShiftPlanBuilder` managed-crop locations | Invariant | Distinct assignment locations are de-duplicated into stable ordered batches. |

## Extension Compliance

| Extension | Status | Entity impact |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | PBT-01 property identification is complete for the new/extended pure entities. |

