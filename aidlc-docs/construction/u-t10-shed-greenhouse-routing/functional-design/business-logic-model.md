# Business Logic Model - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Stage**: Functional Design
**Status**: Generated from answered functional-design plan on 2026-05-31

## Answered Decisions

| Question | Answer | Functional decision |
|---|---|---|
| FD-Q1 | A | Discovery uses route-shape availability; the shift revalidates reachability and passability before movement. |
| FD-Q2 | A | Every route hop is explicit data: route id, source location, approach tile, target location, and arrival tile. |
| FD-Q3 | A | Route failure skips only the affected shed greenhouse batch or deposit trip and preserves items through existing paths. |
| FD-Q4 | A | Shed greenhouse and main shed chests are available only for selected shed-greenhouse output. |
| FD-Q5 | A | `ShiftOrchestrator` owns skip, continue, warning, and deposit fallback policy. |
| FD-Q6 | A | Pure route-model invariants and domain generators are required for Code Generation. |

## Business Goal

The player can select SVE's `Custom_GrandpasShedGreenhouse` as the single greenhouse work scope when SVE exposes the live shed greenhouse route shape. The worker then services only greenhouse crop work in that location, using explicit source-grounded route hops instead of a direct farm-to-greenhouse shortcut. Any route failure is narrow, item-safe, and non-player-facing.

## Availability Model

Availability has two levels:

| Level | Used by | Checks | Outcome |
|---|---|---|---|
| Discovery availability | Hiring scope and destination discovery | Active profile supports a descriptor; target locations exist; a route definition exists for the live farm signature; configured hop locations and tile coordinates are present enough to identify the route shape. | The shed greenhouse can be shown as a greenhouse alternative, and eligible shed/main-shed chests can be shown when the draft selected shed greenhouse work. |
| Shift readiness | Runtime work and deposit execution | Recompute live route; validate all hop source/target locations; validate approach and arrival tiles; validate worker-reachable approach tiles and usable standing tiles where relevant. | The worker may start the route. Failure returns a reason and never throws. |

Discovery must not inspect SVE quest, event, or mail flags. If the shed is unrepaired or partially loaded, the missing live locations or invalid route shape naturally make the shed greenhouse undiscoverable or unready.

## Work Scope Flow

1. `ChestResolver.GetBuildingOutlines(farm)` gathers normal building outlines as today.
2. The expansion compatibility bridge appends a virtual `BuildingOutline` for `Custom_GrandpasShedGreenhouse` only when discovery availability succeeds.
3. `LegacyScopeBootstrapper.FilterSupportedBuildings(...)` and `TryApplySelectedBuildings(...)` treat the virtual outline as greenhouse-like because its location/display identity is greenhouse-scoped.
4. The draft stores the selection as the existing `GreenhouseSelection(LocationName)` with no save-schema change.
5. Only one greenhouse selection exists in the contract. Selecting the standard greenhouse never includes the shed greenhouse, and selecting the shed greenhouse never includes the standard greenhouse.

## Shed Greenhouse Work Flow

1. `ShiftOrchestrator` builds a `BatchKind.Greenhouse` batch from the selected greenhouse location.
2. If the selected greenhouse is not an expansion greenhouse alternative, the existing greenhouse/building path runs unchanged.
3. If the selected greenhouse is `Custom_GrandpasShedGreenhouse`, `ShiftOrchestrator` requests an expansion route from the current worker location to the shed greenhouse with purpose `WorkEntry`.
4. `ExpansionCompatService` computes the live farm map signature, asks the active profile for the route, and validates the route against live game state.
5. If validation succeeds, `ShiftOrchestrator` starts `CrossLocationRouteNavigator`.
6. The navigator executes each hop in order:
   - walk to the hop approach tile in the source location;
   - perform the location transition to the target location;
   - place the worker at the configured arrival tile;
   - advance to the next hop.
7. After the final hop, the existing greenhouse scan and task execution path handles Water Crops and Harvest Crops only.
8. When shed greenhouse work is complete, the worker exits by an explicit route from the current expansion location back to the next required location, normally the farm or a deposit destination route. Reverse travel must be route-provider data or an explicitly validated reverse route; it is not inferred from a direct farm shortcut.

## Deposit Flow

1. `OutputDestinationsMenu` offers ordinary mail, shipping-bin, and farm chest destinations as today.
2. Expansion chest destinations from `Custom_GrandpasShedGreenhouse` and `Custom_GrandpasShed` are offered only when the current draft has `GreenhouseSelection("Custom_GrandpasShedGreenhouse")`.
3. `Custom_GrandpasShedGreenhouse` chests and `Custom_GrandpasShed` chests are valid destinations only for output produced by the selected shed greenhouse scope.
4. When a deposit trip targets an expansion chest, `ShiftOrchestrator` requests a validated expansion route with purpose `DepositEntry`.
5. After route completion, the existing chest stand-tile selection and transfer code performs the actual deposit.
6. If the route, chest, or stand tile fails, the trip is marked undelivered or overflowed through existing item-safety paths. No collected item is discarded.

## Failure Handling Model

| Failure point | Decision owner | Functional outcome |
|---|---|---|
| No matching route definition | `ShiftOrchestrator`, based on compat result | Skip shed greenhouse work batch, or mark matching deposit trip undelivered. |
| Required location missing | `ShiftOrchestrator`, based on compat result | Same as above; log maintainer-facing route reason. |
| Approach or arrival tile invalid | `ShiftOrchestrator`, based on compat result | Same as above; include first failing hop in reason. |
| Approach tile not reachable | `ShiftOrchestrator`, based on compat result | Same as above; no player mail. |
| Mid-route navigation failure | `ShiftOrchestrator`, based on navigator status | Skip affected work route or fail affected deposit route item-safely. |
| Chest missing or full after successful route | Existing deposit code | Existing undelivered/overflow behavior. |

Warnings are maintainer-facing. A failed route attempt should produce one warning with the route id, target, first failing hop if known, and reason. The warning must not become player mail or a needs-attention contract state.

## Data Flow

| Input | Source | Consumer | Notes |
|---|---|---|---|
| Active expansion profile | Startup detection | `ExpansionCompatService` | Vanilla profile returns no routes and no expansion locations. |
| Live farm signature | `ExpansionCompatService` | Route lookup | Same signature concept used by existing SVE entrance overrides. |
| Expansion route definitions | `SveExpansionProfile` | `ExpansionCompatService`, tests | Pure data only; no live game objects. |
| Expansion location descriptors | `SveExpansionProfile` | Discovery adapters | Identify greenhouse work and deposit-only locations. |
| Selected greenhouse | `ContractScopeSelection.Greenhouse` | Runtime batch builder | Existing save model remains authoritative. |
| Chest destinations | `ChestResolver` and output draft | Deposit planner and orchestrator | Expansion chests are gated to selected shed-greenhouse output. |
| Validation results | `ExpansionCompatService` | `ShiftOrchestrator` | Total result with failure reason, not exceptions. |

## Testable Properties

| Property id | Category | Property |
|---|---|---|
| P-T10-FD-01 | Invariant | Route lookup is deterministic for the same farm signature, target location, source location, and purpose. |
| P-T10-FD-02 | Invariant | A returned route preserves configured hop order and contains no missing hop ordinal. |
| P-T10-FD-03 | Invariant | A successful shed-greenhouse route never has a single direct farm-to-`Custom_GrandpasShedGreenhouse` success hop when intermediate shed route data is required. |
| P-T10-FD-04 | Invariant | Route validation is total: every generated valid or invalid route model maps to success or a typed failure result, never an exception. |
| P-T10-FD-05 | Invariant | Work-route failure maps to skip-and-continue for the affected shed-greenhouse batch only. |
| P-T10-FD-06 | Invariant | Deposit-route failure maps all in-flight items to undelivered or overflow states; quantity and item identity are preserved. |
| P-T10-FD-07 | Invariant | Expansion chests are eligible only when the selected work scope is the shed greenhouse. |

PBT generator expectations are carried into Code Generation: route ids, farm signatures, source/target names, route purposes, hop lists, failure reasons, and item stacks need domain-specific generators. FsCheck remains the selected framework.

## Extension Compliance

| Extension | Status | Functional Design result |
|---|---|---|
| Security Baseline | Disabled | Skipped per TODO-10 configuration. No network, authentication, secrets, or PII behavior is introduced. |
| Property-Based Testing | Enabled - Partial | Compliant for Functional Design. PBT-03 invariants are documented above; PBT-07 requires domain generators in Code Generation; PBT-08 and PBT-09 are carried to NFR/Code Generation/Build and Test. PBT-02 is N/A because this design introduces no inverse or round-trip operation. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
