# Components Addendum - TODO-10 SVE Grandpa's Shed Greenhouse

## Purpose
This addendum extends the existing SVE compatibility design for TODO-10. It defines the high-level components needed to support source-grounded multi-hop routes, shed greenhouse selection, shed/main-shed deposit destinations, and route-failure handling.

Detailed business rules stay in Functional Design.

## Core Components

### C-T10-01 Expansion Route Model
- **Location**: `Dayswork.Core/Compat/`
- **Purpose**: Represent explicit expansion routes as pure data.
- **Responsibilities**:
  - Model route identity, supported farm-map signature, target location, route purpose, and ordered hops.
  - Model each hop using primitive location names and tile coordinates only.
  - Stay free of SMAPI, Stardew, and live map references.
  - Support deterministic example tests and FsCheck route-model properties.
- **Key collaborators**: `IExpansionProfile`, `SveExpansionProfile`, `ExpansionCompatService`.

### C-T10-02 Expansion Location Descriptor
- **Location**: `Dayswork.Core/Compat/`
- **Purpose**: Describe expansion locations that should appear in work-scope or deposit-destination discovery.
- **Responsibilities**:
  - Identify `Custom_GrandpasShedGreenhouse` as a greenhouse-work and deposit-capable location.
  - Identify `Custom_GrandpasShed` as a deposit-only location for shed-greenhouse output.
  - Keep `Custom_GrandpasShedOutside` and `Custom_GrandpasShedRuins` out of the work-location set.
  - Provide enough pure metadata for Mod-side UI/discovery adapters to create virtual building outlines and chest entries.
- **Key collaborators**: `SveExpansionProfile`, `ExpansionCompatService`, `ChestResolver`, `LegacyScopeBootstrapper`.

### C-T10-03 IExpansionProfile Route Extension
- **Location**: `Dayswork.Core/Compat/IExpansionProfile.cs`
- **Purpose**: Extend the existing profile seam so route and expansion-location data stay centralized.
- **Responsibilities**:
  - Expose pure route-definition lookup by farm signature, target location, and route purpose.
  - Expose expansion location descriptors for work and deposit discovery.
  - Preserve the Vanilla Null-Object behavior by returning no route and no expansion locations.
- **Key collaborators**: `VanillaExpansionProfile`, `SveExpansionProfile`, `ExpansionProfileSelector`.

### C-T10-04 SveExpansionProfile Route Data
- **Location**: `Dayswork.Core/Compat/SveExpansionProfile.cs`
- **Purpose**: Hold TODO-10 SVE identifiers and route data in the existing single source of SVE truth.
- **Responsibilities**:
  - Define route IDs for the shed greenhouse on Immersive Farm 2 Remastered, Grandpa's Farm, and Frontier Farm.
  - Define route hops for farm-to-shed-complex-to-greenhouse travel using SVE source-grounded location names and tile coordinates.
  - Define work/deposit location descriptors for `Custom_GrandpasShedGreenhouse` and `Custom_GrandpasShed`.
  - Avoid a generic Content Patcher graph scan.
- **Key collaborators**: `ExpansionRouteModel`, `ExpansionLocationDescriptor`.

## Mod Components

### M-T10-01 ExpansionCompatService Route Bridge
- **Location**: `Dayswork/Compat/ExpansionCompatService.cs`
- **Purpose**: Bridge pure route/location data to live game validation.
- **Responsibilities**:
  - Compute the live farm-map signature and request the active profile's route definition.
  - Validate that required locations exist and that hop approach/arrival tiles can be used by the worker.
  - Return a total validation result instead of throwing.
  - Expose virtual expansion work-location and deposit-destination entries to discovery code.
  - Log or surface maintainer-facing route-unavailable reasons for the orchestrator.
- **Key collaborators**: active `IExpansionProfile`, `WorkerMovementDriver`, `Game1.getLocationFromName`, `ChestResolver`, `ShiftOrchestrator`.

### M-T10-02 CrossLocationRouteNavigator
- **Location**: `Dayswork/Orchestration/`
- **Purpose**: Execute an already-validated ordered route across multiple locations.
- **Responsibilities**:
  - Move the worker to each hop approach tile using existing walking/navigation primitives.
  - Perform the location transition for the current hop using existing warp/location-transition primitives.
  - Report progress/failure to `ShiftOrchestrator` without owning shift phase transitions.
  - Reuse `BuildingWorkNavigator` primitives where a hop matches existing building-entry/exit behavior.
- **Key collaborators**: `WorkerMovementDriver`, `BuildingWorkNavigator`, `FarmhandNpc`, `ShiftOrchestrator`.

### M-T10-03 Expansion Scope and Destination Discovery
- **Location**: `Dayswork/Integration/ChestResolver.cs`, `Dayswork/UI/LegacyScopeBootstrapper.cs`
- **Purpose**: Make the shed greenhouse selectable and its valid chests discoverable without broad name heuristics.
- **Responsibilities**:
  - Add virtual building-outline entries for expansion greenhouse-work locations exposed by the compat service.
  - Add chest entries from expansion deposit locations exposed by the compat service.
  - Let the existing single `GreenhouseSelection(LocationName)` model select `Custom_GrandpasShedGreenhouse`.
  - Keep `Custom_GrandpasShed` deposit-only, not a selectable work location.
- **Key collaborators**: `ExpansionCompatService`, `BuildingOutline`, `ChestEntry`, `GreenhouseSelection`.

### M-T10-04 ShiftOrchestrator Route Policy
- **Location**: `Dayswork/Orchestration/ShiftOrchestrator.cs`
- **Purpose**: Own route-failure policy and state-machine decisions.
- **Responsibilities**:
  - Request a route for shed greenhouse work/deposit when the active batch or chest destination requires it.
  - Map work-route validation failure to skip-and-continue.
  - Map deposit-route validation or execution failure to existing undelivered-item/overflow handling.
  - Preserve vanilla and standard greenhouse paths when no expansion route is needed.
- **Key collaborators**: `ExpansionCompatService`, `CrossLocationRouteNavigator`, `BuildingWorkNavigator`, `DepositPlanner`.

## Component Compliance

| Requirement | Component coverage |
|---|---|
| Explicit route provider, no generic graph scan | C-T10-01, C-T10-03, C-T10-04 |
| Runtime route validation | M-T10-01 |
| Multi-hop route execution | M-T10-02 |
| Single greenhouse selection | M-T10-03 |
| Shed greenhouse/main shed deposit support | C-T10-02, M-T10-03, M-T10-04 |
| Skip/continue and item safety | M-T10-04 |
| Pure route-model tests | C-T10-01, C-T10-03, C-T10-04 |
