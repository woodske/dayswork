# Tech Stack Decisions - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Change**: TODO-10 SVE Grandpa's Shed greenhouse routing
**Stage**: Construction / NFR Requirements

## Primary Decision

Reuse the existing stack only: C#/.NET, SMAPI/Stardew APIs, current movement and navigation services, xUnit, and FsCheck. Do not add a new runtime dependency, route-graph package, Content Patcher parser, async job system, or persistence schema.

## Decisions

| Concern | Decision | Rationale |
|---|---|---|
| Runtime language and platform | Continue with C#/.NET in the existing SMAPI mod structure. | Keeps TODO-10 inside the established mod architecture and avoids deployment or dependency churn. |
| Pure route model | Add or extend pure Core compat types for expansion route ids, purposes, requests, definitions, hops, descriptors, validation failures, and policy values. | Pure types keep route lookup, invariants, and policy mapping testable without live SMAPI state. |
| SVE route data | Store supported farm signatures, SVE location names, route ids, purposes, and ordered hop data in `SveExpansionProfile` or adjacent profile-owned route data. | Centralizes SVE-specific strings and coordinates and preserves vanilla invariance. |
| Profile seam | Extend the existing `IExpansionProfile` and active-profile flow rather than creating a separate SVE routing subsystem. | Matches the approved Application Design and keeps future expansion support profile-driven. |
| Live validation | Use `ExpansionCompatService` as the thin adapter that reads live Stardew locations, map bounds, passability, and worker reachability, then returns total validation results. | Keeps live game object access out of pure Core and provides one boundary for failure reasons. |
| Movement execution | Add a narrow `CrossLocationRouteNavigator` or equivalent runtime helper that executes validated ordered hops using the existing worker movement services. | Provides multi-hop route execution without replacing the existing movement stack. |
| Failure policy | Keep skip, continue, warning, overflow, and undelivered decisions in `ShiftOrchestrator`, supported by pure policy values where practical. | Preserves the existing orchestration boundary and item-safety behavior. |
| UI discovery | Extend existing `ChestResolver`, `LegacyScopeBootstrapper`, `OutputDestinationsMenu`, and related menu data sources through profile descriptors and draft-aware filtering. | Avoids new menus and keeps shed greenhouse availability data-driven. |
| Testing framework | Use xUnit for example tests and FsCheck for property-based tests. | FsCheck is already the selected C#/.NET PBT framework and satisfies PBT-09. |
| New libraries | None. | The route set is explicit and small; a route-graph library or Content Patcher parser would add risk without improving the bounded TODO-10 path. |

## Rejected Alternatives

| Alternative | Reason rejected |
|---|---|
| Dedicated route-graph/pathfinding library | TODO-10 needs explicit source-grounded multi-hop route data, not generic graph solving. Existing movement services already handle tile walking inside each hop. |
| Runtime Content Patcher map/action parser | Adds broad parsing scope, runtime fragility, and dependency risk. Route data must be source-grounded and encoded through the profile seam. |
| Day-long route/passability cache | Live SVE location and passability state can change across days, saves, and reloads. Per-attempt validation is the selected reliability model. |
| New save schema for shed greenhouse selection | The existing `GreenhouseSelection(LocationName)` can represent `Custom_GrandpasShedGreenhouse` without migration. |
| New player-facing error UI | Route failures are maintainer-facing warnings plus existing item overflow behavior, not a new UX surface. |

## PBT Framework Decision

| PBT rule | Decision |
|---|---|
| PBT-02 | N/A for this unit unless Code Generation introduces a reversible parse/format or serialize/deserialize operation. |
| PBT-03 | Route, policy, filtering, and item-safety invariants must be covered by FsCheck properties where the logic is pure. |
| PBT-07 | Domain-specific FsCheck generators must be used for route definitions, requests, failures, destinations, policy values, and item stacks. |
| PBT-08 | FsCheck shrinking and reproducibility remain enabled through the existing xUnit integration; tests must not disable shrinking. |
| PBT-09 | FsCheck is the selected framework for C#/.NET property-based testing and remains in the project dependency set. |

## Manual Verification Stack

Manual verification uses the existing local Stardew Valley plus SMAPI playtest workflow. At least one supported SVE farm map must be exercised end to end for TODO-10: select the shed greenhouse, enter through the explicit multi-hop route, perform greenhouse crop work, deposit or exit item-safely, and confirm route failures do not create new player-facing route-error UI.

## Extension Compliance

| Extension | Status | Tech-stack compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. No new security-sensitive stack element is introduced. |
| Property-Based Testing | Enabled - Partial | Compliant. FsCheck is selected and documented for PBT-09; PBT-03, PBT-07, and PBT-08 obligations are carried into Code Generation and Build and Test. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
