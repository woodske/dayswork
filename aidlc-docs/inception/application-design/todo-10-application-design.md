# Application Design - TODO-10 SVE Grandpa's Shed Greenhouse

## Scope
TODO-10 extends the existing SVE compatibility design to support `Custom_GrandpasShedGreenhouse` as an explicitly selected greenhouse work location reached through source-grounded multi-hop routes.

This design is an addendum to:
- [sve-compatibility-application-design.md](sve-compatibility-application-design.md)
- [components.md](components.md)
- [component-methods.md](component-methods.md)
- [services.md](services.md)
- [component-dependency.md](component-dependency.md)

## Approved Design Decisions
- **Q1=A**: Extend `IExpansionProfile` / `SveExpansionProfile` with route-definition lookups. Route data remains pure Core data; live validation stays in `ExpansionCompatService`.
- **Q2=A**: Add a narrow `CrossLocationRouteNavigator`. It executes route movement while `ShiftOrchestrator` owns state and policy.
- **Q3=A**: Extend the compat seam to provide expansion work/destination locations. `ChestResolver` and building-outline enumeration add virtual shed greenhouse/main-shed entries.
- **Q4=A**: Keep route-failure decisions in `ShiftOrchestrator`, using pure validation results from the compat bridge.

## Design Artifacts
- [todo-10-components.md](todo-10-components.md) defines new/extended components.
- [todo-10-component-methods.md](todo-10-component-methods.md) defines high-level method contracts.
- [todo-10-services.md](todo-10-services.md) defines service orchestration flows.
- [todo-10-component-dependency.md](todo-10-component-dependency.md) defines dependencies, communication patterns, and coupling constraints.

## Component Summary

| Component | Layer | Summary |
|---|---|---|
| Expansion Route Model | Core | Pure route IDs, requests, route definitions, and ordered hops. |
| Expansion Location Descriptor | Core | Pure work/destination metadata for shed greenhouse and main shed. |
| `IExpansionProfile` route extension | Core | Route and expansion-location lookup seam. |
| `SveExpansionProfile` route data | Core | Single SVE data home for route tables and location descriptors. |
| `ExpansionCompatService` route bridge | Mod | Live route validation and virtual discovery bridge. |
| `CrossLocationRouteNavigator` | Mod | Executes validated route hops using movement primitives. |
| Expansion scope/destination discovery | Mod/UI integration | Adds virtual greenhouse selection and deposit chest entries. |
| `ShiftOrchestrator` route policy | Mod orchestration | Owns skip/continue, deposit failure handling, and state transitions. |

## Vanilla and Save Compatibility
- `VanillaExpansionProfile` returns no route and no expansion locations.
- No SVE string is added to general route/orchestration code outside the compat seam and route bridge.
- Existing vanilla greenhouse and standard SVE greenhouse paths remain unchanged when no shed greenhouse route is selected.
- `ContractScopeSelection` and persistence DTOs remain unchanged; the shed greenhouse is represented by the existing `GreenhouseSelection(LocationName)` field.

## Design Completeness Check

| Requirement area | Design coverage |
|---|---|
| Explicit source-grounded routes | `SveExpansionProfile` route data plus Core route model. |
| Runtime route validation | `ExpansionCompatService` route bridge. |
| Multi-hop execution | `CrossLocationRouteNavigator`. |
| Single greenhouse selection | Expansion virtual outline plus existing `GreenhouseSelection`. |
| Main shed deposit-only support | Expansion location descriptor plus `ChestResolver` expansion chest discovery. |
| Skip/continue failure behavior | `ShiftOrchestrator` route policy. |
| Item safety | Existing deposit/overflow paths retained; route failure maps into those paths. |
| PBT-ready route model | Core pure route model and profile lookup. |

## Extension Rule Compliance

| Extension | Status | Compliance / Rationale |
|---|---|---|
| Security Baseline | Disabled | Skipped per TODO-10 configuration. No network, authentication, secrets, or PII surface is introduced by the design. |
| Property-Based Testing | Enabled - Partial | Compliant at design level. Route definitions and validation decisions are separated into pure model/profile and live bridge boundaries so route-model properties can be implemented in construction. |

## Content Validation
- Markdown only.
- No Mermaid diagrams.
- No ASCII diagrams.
- Code blocks are C# signature sketches only.
