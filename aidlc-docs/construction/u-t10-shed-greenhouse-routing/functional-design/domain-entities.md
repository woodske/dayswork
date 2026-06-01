# Domain Entities - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Stage**: Functional Design

## Core Route Entities

| Entity | Layer | Purpose |
|---|---|---|
| `ExpansionRouteId` | Core | Stable identifier for one source-grounded route definition. |
| `ExpansionRoutePurpose` | Core | Describes why the route is needed: work entry, deposit entry, return-to-farm, or route-shape discovery. |
| `ExpansionRouteRequest` | Core | Pure lookup request containing farm signature, source location, target location, and route purpose. |
| `ExpansionRouteDefinition` | Core | Pure ordered route data for one supported farm signature and purpose. |
| `ExpansionRouteHop` | Core | One explicit step from a source location and approach tile to a target location and arrival tile. |

### ExpansionRouteDefinition Fields

| Field | Meaning | Rule |
|---|---|---|
| `Id` | Stable route id | Unique within a profile. |
| `FarmSignature` | Supported live farm map signature | Required for SVE farm-map-specific route tables. |
| `SourceLocationName` | Expected starting location for the route | Usually `Farm`, `Custom_GrandpasShed`, or `Custom_GrandpasShedGreenhouse`. |
| `TargetLocationName` | Final route destination | Work route target is `Custom_GrandpasShedGreenhouse`; deposit route target may be greenhouse or main shed. |
| `Purpose` | Work, deposit, return, or discovery shape | Prevents an unrelated route from being reused accidentally. |
| `Hops` | Ordered non-empty hop list | Must be executed in order; no duplicate implied execution. |

### ExpansionRouteHop Fields

| Field | Meaning | Rule |
|---|---|---|
| `Ordinal` | Position in the route | Must be contiguous from first to last route hop. |
| `FromLocationName` | Location where the worker walks before transition | Must resolve during shift readiness validation. |
| `ApproachTile` | Tile the worker walks to before transition | Must be within the source map and reachable during shift readiness validation. |
| `ToLocationName` | Location entered by the hop transition | Must resolve during validation. |
| `ArrivalTile` | Tile where the worker appears after transition | Must be within the target map and usable as a worker tile. |

## Location and Discovery Entities

| Entity | Layer | Purpose |
|---|---|---|
| `ExpansionLocationDescriptor` | Core | Pure metadata for an expansion location that may participate in scope or deposit discovery. |
| `ExpansionLocationRole` | Core | Classifies a descriptor as `GreenhouseWork` or `DepositOnly`. |
| `ExpansionDestinationEligibility` | Core or Mod adapter | Declares which selected work location may use a deposit location. |
| `ExpansionDiscoveryAvailability` | Mod adapter | Result of route-shape discovery validation for UI surfaces. |
| `BuildingOutline` | Existing UI DTO | Virtual outline used to let `ZoneDrawMenu` select the shed greenhouse. |
| `ChestEntry` | Existing UI DTO | Destination entry for chests in shed greenhouse or main shed locations. |

### ExpansionLocationDescriptor Fields

| Field | Meaning | TODO-10 values |
|---|---|---|
| `LocationName` | Stable game location name | `Custom_GrandpasShedGreenhouse` or `Custom_GrandpasShed`. |
| `DisplayName` | UI-friendly label source | Shed greenhouse or Grandpa's Shed. |
| `Role` | Work/deposit role | Greenhouse work for shed greenhouse; deposit-only for main shed. |
| `EligibleWorkLocationName` | Work scope that can use this descriptor for deposits | `Custom_GrandpasShedGreenhouse`. |
| `RequiresRouteShape` | Whether discovery validates a route shape first | True for TODO-10 descriptors. |

## Validation Entities

| Entity | Layer | Purpose |
|---|---|---|
| `ExpansionRouteValidationResult` | Mod adapter boundary | Total success/failure result returned by the compat bridge. |
| `ValidatedExpansionRoute` | Mod | Route definition plus live validated hop references. |
| `ValidatedExpansionRouteHop` | Mod | One hop with live source/target locations and validated tiles. |
| `ExpansionRouteFailure` | Core or Mod boundary | Typed failure data for logging and policy mapping. |
| `ExpansionRouteFailureReason` | Core or Mod boundary | Machine-readable reason category. |

### Failure Reason Categories

| Reason | Meaning | Runtime mapping |
|---|---|---|
| `UnsupportedProfile` | Active profile has no expansion route support | No UI entry, or skip selected route safely. |
| `UnsupportedFarmSignature` | SVE route table has no route for the live farm signature | No UI entry or skip safely. |
| `RouteNotDefined` | Route lookup failed for source, target, and purpose | Skip work or mark deposit undelivered. |
| `SourceLocationMissing` | Required source location is not loaded | Skip or fail deposit safely. |
| `TargetLocationMissing` | Required target location is not loaded | Skip or fail deposit safely. |
| `ApproachTileInvalid` | Approach tile is off-map or blocked for worker use | Skip or fail deposit safely. |
| `ApproachTileUnreachable` | Worker cannot path to the approach tile | Skip or fail deposit safely. |
| `ArrivalTileInvalid` | Arrival tile cannot safely receive the worker | Skip or fail deposit safely. |
| `NavigationFailed` | Movement failed after route validation succeeded | Skip or fail deposit safely. |

## Policy Entities

| Entity | Layer | Purpose |
|---|---|---|
| `ExpansionRoutePolicyDecision` | Core or Mod adapter | Pure mapping from validation/navigation outcome to shift action. |
| `ExpansionRoutePolicyAction` | Core or Mod adapter | `Proceed`, `SkipWorkBatch`, or `MarkDepositUndelivered`. |
| `ExpansionRouteWarning` | Mod | Maintainer-facing warning payload with route id, target, purpose, failing hop, and reason. |

Policy mapping is owned by `ShiftOrchestrator`, but the mapping can be represented with pure values so Code Generation can test it without live SMAPI state.

## Existing Entities Reused

| Entity | Reuse |
|---|---|
| `FarmMapSignature` | Selects farm-map-specific SVE route data. |
| `TileCoord` | Represents approach and arrival tiles in pure route data. |
| `GreenhouseSelection` | Persists the selected shed greenhouse without schema changes. |
| `ContractScopeSelection` | Remains authoritative for runtime scope. |
| `WorkBatch` and `BatchKind.Greenhouse` | Carries shed greenhouse crop work into the existing batch loop. |
| `ChestRef`, `ChestEntry`, `ChestDestination` | Represent expansion chest destinations. |
| `DepositTrip` | Carries expansion deposit work through existing deposit planning. |
| `OverflowItem` and `OverflowReason.NotDelivered` | Preserve items when route or deposit execution fails. |

## Entity Relationships

| Relationship | Cardinality | Rule |
|---|---|---|
| Profile to route definitions | One profile to many routes | Vanilla profile has zero; SVE profile owns TODO-10 routes. |
| Route definition to hops | One route to one or more hops | Hop order is significant and property-tested. |
| Work descriptor to route | One descriptor to one or more routes | Route varies by farm signature and purpose. |
| Deposit descriptor to eligible work location | Many deposit descriptors to one shed greenhouse work location | Main shed is deposit-only for shed greenhouse output. |
| Validation result to policy action | One result to one action | Failure maps to skip or undelivered; success maps to proceed. |

## Testable Properties

| Property id | Entity focus | Category | Property |
|---|---|---|---|
| P-T10-ENT-01 | `ExpansionRouteDefinition` | Invariant | Hop ordinals are contiguous and execution order equals definition order. |
| P-T10-ENT-02 | `ExpansionRouteRequest` | Invariant | Same request and same profile data always produce the same lookup result. |
| P-T10-ENT-03 | `ExpansionLocationDescriptor` | Invariant | `DepositOnly` descriptors never become greenhouse work selections. |
| P-T10-ENT-04 | `ExpansionRouteValidationResult` | Invariant | Every validation attempt produces exactly one success or one typed failure. |
| P-T10-ENT-05 | `ExpansionRoutePolicyDecision` | Invariant | Work failures map to `SkipWorkBatch`; deposit failures map to `MarkDepositUndelivered`. |
| P-T10-ENT-06 | `DepositTrip` item mapping | Invariant | Undelivered mapping preserves item id, quantity, task source, and provenance. |

## Extension Compliance

| Extension | Status | Functional Design result |
|---|---|---|
| Security Baseline | Disabled | Skipped per TODO-10 configuration. |
| Property-Based Testing | Enabled - Partial | Compliant. Entity invariants cover PBT-03; domain-generator needs are explicit for PBT-07; FsCheck/PBT-08/PBT-09 obligations carry forward. PBT-02 remains N/A unless Code Generation introduces a reversible transform. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
