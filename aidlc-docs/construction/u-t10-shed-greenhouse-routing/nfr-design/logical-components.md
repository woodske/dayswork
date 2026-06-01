# Logical Components - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Change**: TODO-10 SVE Grandpa's Shed greenhouse routing
**Stage**: Construction / NFR Design

## Component Map

| Component | Layer | NFR role | Patterns |
|---|---|---|---|
| `ExpansionRouteId` / route purpose / route request | Core | Stable pure identity for route lookup and test generation. | P-T10-NFR-01, P-T10-NFR-09 |
| `ExpansionRouteDefinition` and ordered hops | Core | Source-grounded route data with contiguous hop order and no generic graph scan. | P-T10-NFR-01, P-T10-NFR-04, P-T10-NFR-09 |
| `ExpansionLocationDescriptor` | Core | Pure metadata for greenhouse work and deposit-only expansion locations. | P-T10-NFR-01, P-T10-NFR-07 |
| `IExpansionProfile` route and descriptor APIs | Core | Profile seam for expansion route data and location discovery. | P-T10-NFR-01, P-T10-NFR-07 |
| `VanillaExpansionProfile` | Core | Null-object behavior for vanilla invariance; exposes no TODO-10 routes or shed locations. | P-T10-NFR-01, P-T10-NFR-07 |
| `SveExpansionProfile` | Core | Single home for SVE supported farm signatures, route ids, route hops, location descriptors, and destination eligibility. | P-T10-NFR-01 |
| `ExpansionCompatService` | Mod adapter | Live route-shape discovery and per-attempt shift readiness validation against live game state. | P-T10-NFR-02, P-T10-NFR-03 |
| `CrossLocationRouteNavigator` | Mod runtime | Executes validated ordered hops through existing movement and location-transition primitives. | P-T10-NFR-04 |
| `ShiftOrchestrator` | Mod orchestration | Owns route failure policy, work-batch skip, deposit failure mapping, warning emission, and no-player-error behavior. | P-T10-NFR-05, P-T10-NFR-06, P-T10-NFR-08 |
| `ChestResolver` / scope discovery bridge | Mod/UI integration | Adds virtual shed greenhouse outlines and expansion chest entries only when discovery and draft eligibility allow. | P-T10-NFR-02, P-T10-NFR-07 |
| `LegacyScopeBootstrapper` | UI integration | Projects the virtual shed greenhouse outline into the existing single `GreenhouseSelection(LocationName)` model. | P-T10-NFR-07 |
| `OutputDestinationsMenu` | UI integration | Presents only draft-eligible shed greenhouse and main-shed chest destinations. | P-T10-NFR-07 |
| TODO-10 example tests | Test | Pin supported route lookup, unsupported route skip, deposit item preservation, vanilla invariance, destination filtering, and no direct shortcut scenarios. | P-T10-NFR-09 |
| TODO-10 FsCheck generators and properties | Test | Generate route definitions, requests, failures, policy values, descriptors, and item stacks for invariant coverage. | P-T10-NFR-09 |
| Manual SVE playtest checklist | Build and Test | Verifies the live multi-hop shed greenhouse route and no new player-facing route-error UI. | P-T10-NFR-10 |

## Runtime Flow Responsibilities

| Flow | Component responsibility |
|---|---|
| Work-scope discovery | `ExpansionCompatService` asks the active profile for shed greenhouse descriptors and validates route shape. `ChestResolver` or the scope discovery bridge appends a virtual greenhouse outline only when discovery availability succeeds. |
| Greenhouse selection | `LegacyScopeBootstrapper` stores the selected shed greenhouse as the existing `GreenhouseSelection(LocationName)` value. No save schema changes. |
| Destination discovery | Draft-aware discovery filters expansion chest entries so shed greenhouse and main shed chests are available only for selected shed-greenhouse output. |
| Work route start | `ShiftOrchestrator` asks `ExpansionCompatService` to validate the route for the current worker/source/target/purpose immediately before movement. |
| Route movement | `CrossLocationRouteNavigator` executes the validated hop list in order and reports completion or navigation failure. |
| Crop work | Existing greenhouse crop services execute Water Crops and Harvest Crops only after route success. |
| Deposit route start | `ShiftOrchestrator` validates the expansion deposit route before moving toward an expansion chest. |
| Deposit failure | `ShiftOrchestrator` maps validation, navigation, stand-tile, or transfer failure to existing undelivered/overflow item-safety paths. |
| Logging | `ShiftOrchestrator` emits one maintainer-facing warning per failed route attempt with route id, purpose, target, failing hop when known, and reason. |

## Failure Modes

| Failure | Handling | Owning component | Pattern |
|---|---|---|---|
| Active profile has no route support | Return no route or no discovery entry; vanilla/no-selection behavior unchanged. | `IExpansionProfile` / `VanillaExpansionProfile` | P-T10-NFR-01 |
| Unsupported farm signature | Route lookup returns no route; discovery hides the shed greenhouse or shift skips selected shed route safely. | `SveExpansionProfile` / `ExpansionCompatService` | P-T10-NFR-01, P-T10-NFR-03 |
| Required live location missing | Validation returns typed failure; work route skips or deposit trip fails item-safely. | `ExpansionCompatService` / `ShiftOrchestrator` | P-T10-NFR-03, P-T10-NFR-05 |
| Approach or arrival tile invalid | Validation returns typed failure with first failing hop when known. | `ExpansionCompatService` | P-T10-NFR-03 |
| Approach tile unreachable | Validation returns typed failure; no route movement starts. | `ExpansionCompatService` | P-T10-NFR-03 |
| Mid-route movement failure | Navigator reports failure; orchestrator applies route-failure policy. | `CrossLocationRouteNavigator` / `ShiftOrchestrator` | P-T10-NFR-04, P-T10-NFR-05 |
| Expansion chest missing or unusable | Existing delivery failure handling preserves items. | `ShiftOrchestrator` / deposit path | P-T10-NFR-06 |
| Warning-heavy repeated tile probes | Warnings aggregate to one per failed route attempt. | `ShiftOrchestrator` | P-T10-NFR-08 |

## No New Infrastructure

| Infrastructure concern | Decision |
|---|---|
| Queues or background jobs | Not introduced. Route lookup and validation are synchronous and bounded. |
| Caches | No day-long or save-long passability cache. Small pure route tables may be static profile data. |
| Circuit breakers or retries | Not introduced. Expected world-state absence maps to typed failure and narrow skip/deposit-failure policy. |
| Persistence schema | Not changed. The selected shed greenhouse uses the existing greenhouse location string. |
| External services | None. TODO-10 remains a local SMAPI runtime change. |
| New dependencies | None. Existing C#/.NET, SMAPI/Stardew APIs, movement/navigation services, xUnit, and FsCheck remain sufficient. |

## Test Component Obligations

| Test component | Obligation |
|---|---|
| Example route tests | Supported route definition is selected by farm signature, source, target, and purpose. Unsupported signatures return no route. |
| Example validation tests | Missing location, invalid tile, and unreachable approach failures produce typed failure results. |
| Example policy tests | Work failure skips only the shed greenhouse batch; deposit failure preserves items through undelivered or overflow paths. |
| Example UI/filter tests | Shed greenhouse and main-shed chests are unavailable unless the selected greenhouse is `Custom_GrandpasShedGreenhouse`. |
| FsCheck route generators | Generate valid and invalid route definitions, hop lists, farm signatures, route requests, purposes, and failure reasons. |
| FsCheck policy/filter generators | Generate policy inputs, expansion descriptors, selected scope combinations, and item stacks. |
| Manual playtest checklist | Exercise one supported SVE farm map end to end and document source-grounded route coverage for the remaining supported maps. |

## Extension Compliance

| Extension | Status | Logical component compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. No security component is added. |
| Property-Based Testing | Enabled - Partial | Compliant. Test components explicitly carry PBT-03 invariant, PBT-07 generator, PBT-08 shrinking/reproducibility, and PBT-09 FsCheck obligations into Code Generation and Build/Test. PBT-02 remains N/A unless reversible transforms are introduced. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
