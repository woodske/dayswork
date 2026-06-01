# Business Rules - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Stage**: Functional Design

## Route and Availability Rules

| Rule | Requirement | Statement |
|---|---|---|
| BR-T10-01 | Vanilla invariance | With the Vanilla profile active, or with SVE active but no shed greenhouse selection, no expansion route, virtual shed greenhouse outline, or shed/main-shed deposit behavior changes existing paths. |
| BR-T10-02 | Centralized SVE data | SVE route ids, supported farm signatures, location names, route purposes, hop locations, and hop tiles live in the SVE profile or route model. General runtime code cannot scatter SVE strings. |
| BR-T10-03 | Supported farms | The route provider covers IF2R, Grandpa's Farm, and Frontier Farm only. Unsupported signatures return no route and do not trigger generic graph discovery. |
| BR-T10-04 | Discovery availability | The hiring/discovery surface may show the shed greenhouse when the active profile exposes a greenhouse-work descriptor, the live target locations exist, the route definition exists for the live farm signature, and the configured hop shape can be resolved. |
| BR-T10-05 | Shift readiness | A shift may start shed-greenhouse movement only after live validation confirms the configured route, every hop source/target location, approach tile, arrival tile, and reachable/passable movement requirements. |
| BR-T10-06 | No quest-flag authority | SVE quest, event, and mail flags are not scheduling authority. Live route and location state decide availability and readiness. |

## Work Scope Rules

| Rule | Requirement | Statement |
|---|---|---|
| BR-T10-07 | Single greenhouse selection | `GreenhouseSelection(LocationName)` remains the only greenhouse selection model. The shed greenhouse is an alternative to the standard greenhouse, not an additional automatic work area. |
| BR-T10-08 | Target location | `Custom_GrandpasShedGreenhouse` is the only new work location. `Custom_GrandpasShed`, `Custom_GrandpasShedOutside`, and `Custom_GrandpasShedRuins` are not work locations. |
| BR-T10-09 | Work types | Shed greenhouse work uses existing greenhouse crop services only: Water Crops and Harvest Crops. Clearing, tree, outdoor, ruins, and main-shed tasks are out of scope. |
| BR-T10-10 | Pricing and energy | Existing greenhouse pricing, stamina, batching, tool, and output provenance semantics apply. TODO-10 does not introduce a new pricing tier or energy profile. |

## Route Execution Rules

| Rule | Requirement | Statement |
|---|---|---|
| BR-T10-11 | Explicit hops | Every expansion route consists of ordered explicit hops. Each hop identifies source location, approach tile, target location, and arrival tile. |
| BR-T10-12 | Movement before transition | The worker must walk to each hop's approach tile before the location transition is performed. |
| BR-T10-13 | No direct shortcut | A successful route to `Custom_GrandpasShedGreenhouse` must not use a direct farm-to-greenhouse shortcut as the primary success path. |
| BR-T10-14 | Total validation | Route lookup and validation are total. Any missing profile data, missing live location, invalid tile, or passability failure returns a failure result and reason rather than throwing. |
| BR-T10-15 | Policy boundary | `CrossLocationRouteNavigator` executes movement and reports status. It does not decide skip, continue, player mail, overflow, or contract state. |
| BR-T10-16 | Orchestrator policy | `ShiftOrchestrator` owns route failure policy: skip affected work batch, continue remaining work, mark affected deposit trip undelivered/overflowed, and emit one warning. |

## Deposit and Item-Safety Rules

| Rule | Requirement | Statement |
|---|---|---|
| BR-T10-17 | Destination eligibility | Chests in `Custom_GrandpasShedGreenhouse` and `Custom_GrandpasShed` are eligible only for output produced by selected shed-greenhouse work. |
| BR-T10-18 | Deposit route | A deposit trip to shed greenhouse or main shed chests must use validated expansion route data, not single-door farm-building assumptions. |
| BR-T10-19 | Main shed deposit-only | `Custom_GrandpasShed` can contribute chest destinations for shed-greenhouse output but never becomes a work-scope selection. |
| BR-T10-20 | Failed deposit safety | If expansion deposit validation, movement, stand-tile selection, or chest transfer fails, all trip items go through existing undelivered or overflow handling. Item id and quantity are preserved. |
| BR-T10-21 | Existing destinations | Shipping bin, mail, and ordinary farm chest destination behavior remain unchanged. |

## Logging and Player-Facing Behavior Rules

| Rule | Requirement | Statement |
|---|---|---|
| BR-T10-22 | Warning content | A route warning includes route id, target, purpose, first failing hop if known, and failure reason. |
| BR-T10-23 | Warning frequency | Emit one maintainer-facing warning per failed route attempt, not one warning per tile probe. |
| BR-T10-24 | No player mail | Route unavailability does not create player mail, HUD errors, or needs-attention contract state. Existing overflow settlement mail remains allowed when items could not be delivered. |

## Save and Compatibility Rules

| Rule | Requirement | Statement |
|---|---|---|
| BR-T10-25 | Save schema | No save DTO or contract persistence shape changes are required. The selected shed greenhouse is represented by the existing greenhouse location string. |
| BR-T10-26 | Edit flow | Existing contracts that do not select `Custom_GrandpasShedGreenhouse` load and run unchanged. |
| BR-T10-27 | Runtime absence | If a saved contract references the shed greenhouse but the route becomes unavailable later, the route failure path skips safely and logs a reason. |

## PBT Rules and Test Obligations

| Rule | Related PBT rule | Statement |
|---|---|---|
| BR-T10-28 | PBT-03 | Each documented route invariant in `business-logic-model.md` needs corresponding property-based coverage during Code Generation where the logic is pure. |
| BR-T10-29 | PBT-07 | Route-model properties must use domain generators for farm signatures, route purposes, route ids, hop lists, location descriptors, validation failures, and item stacks. |
| BR-T10-30 | PBT-08 | Generated FsCheck properties must keep shrinking enabled and remain reproducible through the project's existing test runner behavior. |
| BR-T10-31 | PBT-09 | FsCheck remains the selected C#/.NET property-based testing framework. |
| BR-T10-32 | PBT-02 | Round-trip PBT is N/A for this unit unless Code Generation introduces a new parse/format or serialize/deserialize operation. |

## Extension Compliance

| Extension | Status | Functional Design result |
|---|---|---|
| Security Baseline | Disabled | Skipped per TODO-10 configuration. |
| Property-Based Testing | Enabled - Partial | Compliant. Blocking partial-mode rules are covered as follows: PBT-02 N/A; PBT-03 documented invariants; PBT-07 generator obligations; PBT-08 reproducibility obligation; PBT-09 FsCheck selection. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
