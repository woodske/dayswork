# Component Methods Addendum - TODO-10 SVE Grandpa's Shed Greenhouse

## Purpose
This file captures high-level method contracts for TODO-10. Exact signatures may be refined during Functional Design and Code Generation, but component ownership should remain stable.

## Core Method Contracts

### C-T10-01 Expansion Route Model

Suggested pure types:

```csharp
public readonly record struct ExpansionRouteId(string Value);
public enum ExpansionRoutePurpose { Work, Deposit }
public sealed record ExpansionRouteRequest(
    FarmMapSignature FarmSignature,
    string TargetLocationName,
    ExpansionRoutePurpose Purpose);
public sealed record ExpansionRouteDefinition(
    ExpansionRouteId Id,
    FarmMapSignature FarmSignature,
    string TargetLocationName,
    IReadOnlyList<ExpansionRouteHop> Hops);
public sealed record ExpansionRouteHop(
    string FromLocationName,
    TileCoord ApproachTile,
    string ToLocationName,
    TileCoord ArrivalTile);
```

High-level expectations:
- `ExpansionRouteDefinition.Hops` is ordered.
- Route definitions contain no live `GameLocation` or SMAPI objects.
- Empty or unavailable route results are represented by lookup failure or validation result, not exceptions.

### C-T10-02 Expansion Location Descriptor

Suggested pure types:

```csharp
public enum ExpansionLocationRole
{
    GreenhouseWork,
    DepositOnly
}

public sealed record ExpansionLocationDescriptor(
    string LocationName,
    string DisplayName,
    ExpansionLocationRole Role,
    ExpansionRouteId? RouteId);
```

High-level expectations:
- `GreenhouseWork` descriptors may become virtual building-outline entries and greenhouse selections.
- `DepositOnly` descriptors may contribute chest entries but not work-scope selections.
- TODO-10 descriptors are limited to `Custom_GrandpasShedGreenhouse` and `Custom_GrandpasShed`.

### C-T10-03 IExpansionProfile Route Extension

Candidate interface additions:

```csharp
bool TryGetExpansionRoute(
    ExpansionRouteRequest request,
    out ExpansionRouteDefinition route);

IReadOnlyList<ExpansionLocationDescriptor> GetExpansionLocations();
```

High-level expectations:
- `VanillaExpansionProfile` returns `false` and an empty location list.
- `SveExpansionProfile` returns only source-grounded explicit route data.
- Callers never inspect SVE identifiers outside the profile or compat bridge.

## Mod Method Contracts

### M-T10-01 ExpansionCompatService Route Bridge

Candidate methods:

```csharp
bool TryResolveExpansionRoute(
    GameLocation farm,
    string targetLocationName,
    ExpansionRoutePurpose purpose,
    out ValidatedExpansionRoute route,
    out string reason);

IReadOnlyList<BuildingOutline> GetExpansionBuildingOutlines(Farm farm);

IReadOnlyList<ChestEntry> GetExpansionChestEntries(Farm farm);

bool IsExpansionGreenhouseAlternative(string locationName);
```

Suggested validation result:

```csharp
public sealed record ValidatedExpansionRoute(
    ExpansionRouteDefinition Definition,
    IReadOnlyList<ValidatedExpansionRouteHop> Hops);

public sealed record ValidatedExpansionRouteHop(
    GameLocation FromLocation,
    TileCoord ApproachTile,
    GameLocation ToLocation,
    TileCoord ArrivalTile);
```

High-level expectations:
- Validation is total and non-throwing.
- The `reason` string is maintainer-facing and not player mail text.
- Route validation checks live location existence and passable worker tiles at the component boundary.
- Discovery methods return empty lists when SVE is absent or locations are not loaded.

### M-T10-02 CrossLocationRouteNavigator

Candidate methods:

```csharp
bool TryStart(
    FarmhandNpc worker,
    ValidatedExpansionRoute route,
    out string reason);

CrossLocationRouteStatus Tick(FarmhandNpc worker);

bool IsActive { get; }

void Reset();
```

Suggested status:

```csharp
public enum CrossLocationRouteStatus
{
    NotActive,
    WalkingToHop,
    AwaitingHopTransition,
    Complete,
    Failed
}
```

High-level expectations:
- The navigator executes movement/transition mechanics only.
- It does not choose skip/continue, mail, overflow, or shift phase transitions.
- `ShiftOrchestrator` owns interpretation of `Complete` or `Failed`.

### M-T10-03 Expansion Scope and Destination Discovery

Candidate `ChestResolver` additions:

```csharp
internal List<ChestEntry> GetExpansionChests(Farm farm);
internal List<BuildingOutline> GetExpansionBuildingOutlines(Farm farm);
```

Candidate `LegacyScopeBootstrapper` behavior:
- Existing `IsGreenhouseLocation` can classify `Custom_GrandpasShedGreenhouse` because the name contains `Greenhouse`.
- Expansion greenhouse outlines must use `LocationName = "Custom_GrandpasShedGreenhouse"`.
- Main shed deposit entries must not be converted into `AnimalBuildingSelection` or work scope.

### M-T10-04 ShiftOrchestrator Route Policy

Candidate responsibilities and call sites:
- Before a `BatchKind.Greenhouse` batch that targets an expansion route, call `TryResolveExpansionRoute(..., Work, ...)`.
- If work-route validation fails, log and mark that batch complete/skipped while continuing later batches.
- For a chest destination in an expansion route location, call `TryResolveExpansionRoute(..., Deposit, ...)`.
- If deposit route validation/execution fails, call the existing undelivered-item path rather than dropping items.
- Keep the existing `BuildingWorkNavigator` path for vanilla buildings and standard greenhouse locations.

## Method Design Notes
- This stage deliberately avoids detailed pathfinding, tile-order, and retry rules. Those belong in Functional Design.
- The public save DTO and `ContractScopeSelection` shape are unchanged.
- SVE route IDs and location names remain centralized behind Core profile data and the Mod-side compat bridge.
