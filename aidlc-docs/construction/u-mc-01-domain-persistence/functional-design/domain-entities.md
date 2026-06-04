# U-MC-01 Domain Entities

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation  
**Stage**: CONSTRUCTION - Functional Design  
**Status**: Review required

## Namespace Placement

Production code should keep the same project boundary used by the existing pure model:

| Entity group | Project | Suggested namespace |
|---|---|---|
| Domain records and enums | `Dayswork.Core` | `Dayswork.Core.Domain` or `Dayswork.Core.Crops` |
| Pure planner services | `Dayswork.Core` | `Dayswork.Core.Crops` |
| DTOs | `Dayswork.Core` | `Dayswork.Core.Persistence.Dto` |
| Serializer mapping | `Dayswork.Core` | `Dayswork.Core.Persistence` |

No U-MC-01 type depends on SMAPI or Stardew runtime types.

## Contract Extensions

### Contract

Add a crop-plan field with an empty default at call sites:

```csharp
public sealed record Contract(
    ContractId Id,
    IReadOnlySet<TaskKind> EnabledTasks,
    IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations,
    ContractSchedule Schedule,
    ContractStatus Status,
    GameDate HireDate,
    ContractScopeSelection ScopeSelection,
    ContractTermsSnapshot TermsSnapshot,
    EnergyTier Tier,
    IReadOnlyList<TaskCategory> CategoryPriority,
    CropPlan CropPlan);
```

Implementation can use an overloaded constructor or static factory if that reduces churn at existing call sites. The functional requirement is that old call sites can create contracts with `CropPlan.Empty`.

### ContractScopeSelection

`ContractScopeSelection` remains the authored location/task scope surface and can expose managed crop zones as needed for preview and scope classification:

```csharp
public IReadOnlyList<CropZoneAssignment> ManagedCropZones { get; }
```

If this is redundant once `Contract.CropPlan` exists, implementation may keep the selected zones only on `CropPlan` and project directly from the contract. The invariant is single authority: the durable crop assignments must not diverge between two collections.

### WorkScopeSet

Add a peer runtime scope:

```csharp
public ManagedCropWorkScope? ManagedCrops { get; }
```

`ManagedCrops` is null when `CropPlan` is empty or disabled.

## CropPlan

```csharp
public sealed record CropPlan(
    IReadOnlyList<CropZoneAssignment> Assignments,
    StorePreference StorePreference,
    bool ClearDebrisBeforeTilling,
    bool ClearDeadPlants)
{
    public static CropPlan Empty { get; }
    public bool IsEnabled => Assignments.Count > 0;
}
```

Rules:

- `Assignments` are independent. Two assignments may share identical choices but remain separate zones.
- `StorePreference` defaults to `Either`.
- `ClearDebrisBeforeTilling` defaults to true.
- `ClearDeadPlants` defaults to true.
- Empty plan preserves all existing non-Manage-Crops behavior.

## CropZoneAssignment

```csharp
public sealed record CropZoneAssignment(
    Zone Zone,
    CropAssignmentMode Mode,
    IReadOnlyDictionary<Season, SeasonCropChoice> SeasonalChoices,
    SeasonCropChoice? SeasonAgnosticChoice,
    ChestRef? OutputChest);
```

### CropAssignmentMode

```csharp
public enum CropAssignmentMode
{
    Seasonal,
    SeasonAgnostic
}
```

Rules:

- `Seasonal` assignments use `SeasonalChoices` and may contain up to Spring, Summer, Fall, and Winter choices.
- `SeasonAgnostic` assignments use `SeasonAgnosticChoice` and ignore `SeasonalChoices`.
- The `Zone.LocationName` identifies whether a future Mod unit treats the zone as farm, greenhouse, or shed greenhouse.
- `OutputChest` is optional; null means future output-chest fallback.

## SeasonCropChoice

```csharp
public sealed record SeasonCropChoice(
    string SeedItemId,
    string? FertilizerItemId,
    bool AutoReplant,
    bool IsMultiSeasonLocked,
    Season? OriginSeason);
```

Rules:

- `SeedItemId` and `FertilizerItemId` are opaque qualified item-ID strings.
- Null `FertilizerItemId` means no fertilizer is required for that choice.
- `IsMultiSeasonLocked` marks derived choices created by the resolver.
- `OriginSeason` is set for locked derived choices so UI/runtime can identify the player-authored source.

## StorePreference

```csharp
public enum StorePreference
{
    Either,
    Pierre,
    Joja
}
```

Store preference is a planning input only. Live store opening, stock, price, and wallet mutation remain outside U-MC-01.

## ManagedCropWorkScope

```csharp
public sealed record ManagedCropWorkScope(
    IReadOnlyList<CropZoneAssignment> Assignments);
```

Rules:

- It is a runtime projection of an enabled crop plan.
- It carries enough provenance for future shift/runtime code to prevent duplicate general crop work on managed tiles.
- It does not carry live inventory or map state.

## CropDescriptor

```csharp
public sealed record CropDescriptor(
    string SeedItemId,
    string CropItemId,
    IReadOnlyList<Season> Seasons,
    int BaseGrowthDays,
    int FertilizedGrowthDays,
    bool IsAutoBuyable,
    bool IsChestSupplyOnly,
    bool IsRegrow);
```

Rules:

- `Seasons` is the pure season availability from game data.
- `FertilizedGrowthDays` is the value used by viability when fertilizer is configured.
- `IsAutoBuyable` and `IsChestSupplyOnly` are catalog-derived flags consumed by supply planning.
- U-MC-01 does not compute descriptors from `Data/Crops`; M-25 owns that live catalog conversion later.

## Planner Input and Output Types

The exact implementation can refine names, but these pure shapes are required.

```csharp
public sealed record SupplyInventory(
    IReadOnlyDictionary<string, int> ItemCounts);

public sealed record SupplyTarget(
    IReadOnlyList<PurchaseLine> Lines);

public sealed record PurchaseLine(
    string ItemId,
    int Quantity);

public sealed record ShopStockSnapshot(
    Store Store,
    IReadOnlySet<string> ItemIds);

public enum Store
{
    Pierre,
    Joja
}

public sealed record StoreResolution(
    Store? Store,
    bool UsingFallback,
    StoreClosedReason? ClosedReason);

public enum StoreClosedReason
{
    Festival,
    PreferredStoreClosed,
    AllStoresClosed,
    ItemUnavailable
}
```

```csharp
public sealed record ManagedCropShiftPlan(
    IReadOnlyList<TileAction> SupplyIndependentActions,
    IReadOnlyList<TileAction> SupplyDependentActions,
    SupplyTarget PurchaseTarget);

public sealed record TileAction(
    Zone Zone,
    TileCoord Tile,
    ManagedCropActionKind Kind,
    string? ItemId);

public enum ManagedCropActionKind
{
    Harvest,
    ClearDebris,
    Till,
    Fertilize,
    Seed,
    Water
}
```

## DTO Shape

Q1 selected additive schema-3 persistence. DTO class names may stay as `V2` until a broader cleanup, but their shape grows nullable crop-plan fields.

```csharp
public sealed class ContractDtoV2
{
    // Existing fields omitted.
    public CropPlanDtoV1? CropPlan { get; set; }
}
```

```csharp
public sealed class CropPlanDtoV1
{
    public List<CropZoneAssignmentDtoV1> Assignments { get; set; } = new();
    public string StorePreference { get; set; } = "";
    public bool ClearDebrisBeforeTilling { get; set; } = true;
    public bool ClearDeadPlants { get; set; } = true;
}

public sealed class CropZoneAssignmentDtoV1
{
    public ZoneDtoV1 Zone { get; set; } = new();
    public string Mode { get; set; } = "";
    public SortedDictionary<string, SeasonCropChoiceDtoV1> SeasonalChoices { get; set; } = new(StringComparer.Ordinal);
    public SeasonCropChoiceDtoV1? SeasonAgnosticChoice { get; set; }
    public ChestRefDtoV1? OutputChest { get; set; }
}

public sealed class SeasonCropChoiceDtoV1
{
    public string SeedItemId { get; set; } = "";
    public string? FertilizerItemId { get; set; }
    public bool AutoReplant { get; set; }
    public bool IsMultiSeasonLocked { get; set; }
    public string? OriginSeason { get; set; }
}

public sealed class ChestRefDtoV1
{
    public string LocationName { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
}
```

Rules:

- Missing `CropPlan` maps to `CropPlan.Empty`.
- Empty assignments may be omitted by serializer settings where practical.
- Season keys serialize as enum names using ordinal sorted order for deterministic output.
- DTO round-trip must be covered by FsCheck with domain-specific generators.

## Pure Services

| Component | Required methods |
|---|---|
| `PlantingViabilityCalculator` | `IsViable(CropDescriptor crop, GameDate today, int seasonLengthDays, bool seasonAgnosticLocation)` and `DaysToMaturity(CropDescriptor crop, bool fertilized)`. |
| `CropSupplyPlanner` | `ComputePurchaseTarget(...)`, `CompletableTiles(...)`, and `BothComponentsOnHand(...)`. |
| `SeasonAssignmentResolver` | `ApplyChoice(...)`, `IsSeasonLocked(...)`, and `MultiSeasonSpan(...)`. |
| `StoreResolver` | `Resolve(...)` and `StoreStocks(...)`. |
| `CropShiftPlanner` | `BuildPlan(...)` and `OrderTileActions(...)`. |
| `CropPlanSerialization` | Domain/DTO mappers for crop-plan types. |

## Extension Compliance

| Extension | Status | Entity impact |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Entity shapes identify the domain generators needed for PBT-07 and the serialization shape needed for PBT-02. |

