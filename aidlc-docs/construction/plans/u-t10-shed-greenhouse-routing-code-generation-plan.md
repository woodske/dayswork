# Code Generation Plan - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Change**: TODO-10 SVE Grandpa's Shed greenhouse routing
**Stage**: Construction / Code Generation
**Status**: Complete; review required

This document is the single source of truth for TODO-10 Code Generation. Brownfield rule: modify existing files in place; do not create duplicate `*_new.cs` or `*_modified.cs` files. Application code belongs in the workspace root projects only. Markdown summaries belong under `aidlc-docs/construction/u-t10-shed-greenhouse-routing/code/`.

## Unit Context

| Area | Context |
|---|---|
| Stories | S-25: Grandpa's Shed greenhouse is a selectable crop-work location. S-26: expansion compatibility remains profile-driven and property-testable. |
| Requirements | FR-T10-01 through FR-T10-12 and NFR-T10-01 through NFR-T10-25. |
| Approved design | Route data in `IExpansionProfile` / `SveExpansionProfile`; live validation in `ExpansionCompatService`; movement in `CrossLocationRouteNavigator`; failure policy in `ShiftOrchestrator`; draft-aware discovery/filtering through existing UI/data-source seams. |
| Package order | `Dayswork.Core` route model first, `Dayswork` runtime wiring second, `Dayswork.Tests` coverage third. |
| Supported SVE farm maps | IF2R, Grandpa's Farm, and Frontier Farm. Route coordinates and location names must be source-grounded from `C:\Users\kwood\Repos\StardewValleyExpanded` before encoding. |
| Save/schema impact | None. The selected shed greenhouse uses existing `GreenhouseSelection(LocationName)`. |
| New dependencies | None. Use existing C#/.NET, SMAPI/Stardew APIs, movement/navigation services, xUnit, and FsCheck. |

## Files In Scope

| Action | Path |
|---|---|
| Modify | `Dayswork.Core/Compat/IExpansionProfile.cs` |
| Modify | `Dayswork.Core/Compat/VanillaExpansionProfile.cs` |
| Modify | `Dayswork.Core/Compat/SveExpansionProfile.cs` |
| Create | `Dayswork.Core/Compat/ExpansionRouteId.cs` or equivalent route-model file |
| Create | `Dayswork.Core/Compat/ExpansionRoutePurpose.cs` or equivalent route-model file |
| Create | `Dayswork.Core/Compat/ExpansionRouteRequest.cs` or equivalent route-model file |
| Create | `Dayswork.Core/Compat/ExpansionRouteDefinition.cs` or equivalent route-model file |
| Create | `Dayswork.Core/Compat/ExpansionRouteHop.cs` or equivalent route-model file |
| Create | `Dayswork.Core/Compat/ExpansionLocationDescriptor.cs` or equivalent location-descriptor file |
| Create | `Dayswork.Core/Compat/ExpansionRouteFailure.cs` or equivalent validation/policy value file if needed in Core |
| Modify | `Dayswork/Compat/ExpansionCompatService.cs` |
| Create | `Dayswork/Orchestration/CrossLocationRouteNavigator.cs` |
| Modify | `Dayswork/Integration/ChestResolver.cs` |
| Modify | `Dayswork/UI/LegacyScopeBootstrapper.cs` |
| Modify | `Dayswork/UI/OutputDestinationsMenu.cs` |
| Modify | `Dayswork/Orchestration/ShiftOrchestrator.cs` |
| Create/Modify | `Dayswork.Tests/Compat/Todo10RouteDefinitionTests.cs` |
| Create/Modify | `Dayswork.Tests/Compat/Todo10RoutePropertyTests.cs` |
| Create/Modify | `Dayswork.Tests/UI/Todo10ExpansionDestinationFilteringTests.cs` |
| Create/Modify | `Dayswork.Tests/Orchestration/Todo10RoutePolicyTests.cs` if the policy seam is pure-testable outside live SMAPI |
| Create | `aidlc-docs/construction/u-t10-shed-greenhouse-routing/code/code-summary.md` |

The exact Core route-model file split may be adjusted during generation if a single cohesive file better matches the local `Dayswork.Core/Compat` style, but all created application-code files remain under the paths above and no duplicate modified-file copies are allowed.

## Part 1 Planning Checklist

- [x] Read Code Generation rule details.
- [x] Read content-validation and question-format rules.
- [x] Read Property-Based Testing extension rules.
- [x] Read approved NFR Design artifacts.
- [x] Inspect existing Core compat profile files.
- [x] Inspect existing Mod compat, building navigation, chest discovery, scope bootstrap, output destination, and shift orchestration seams.
- [x] Inspect existing tests and FsCheck generator patterns.
- [x] Determine code locations in workspace root projects.
- [x] Create this detailed Code Generation plan.
- [x] Log the plan approval prompt in `aidlc-docs/audit.md`.
- [x] Record the user's plan approval response.
- [x] Mark Code Generation Part 1 planning complete in `aidlc-state.md` after approval.

## Generation Steps

- [x] **Step 1 - Verify SVE route source data before coding.** Re-open the local SVE source under `C:\Users\kwood\Repos\StardewValleyExpanded` and record the exact location names, supported farm signatures, tile-action route names, hop order, approach/arrival tiles, and any relevant `DefaultArrivalTile` / plantable greenhouse data for `Custom_GrandpasShedGreenhouse`, `Custom_GrandpasShed`, and supported farm routes. Update the code summary with the source-grounding note. *(S-25, NFR-T10-20, P-T10-NFR-01)*

- [x] **Step 2 - Add pure Core expansion route and descriptor model.** In `Dayswork.Core/Compat`, add route value types for route id, purpose, request, definition, hop, location descriptor, eligibility, and typed failure/policy values as needed. Keep types immutable, primitive-only, and independent of SMAPI/Stardew live objects. Preserve ASCII naming and avoid new dependencies. *(BR-T10-11, BR-T10-14, P-T10-NFR-09)*

- [x] **Step 3 - Extend profile APIs and Null-Object behavior.** Modify `IExpansionProfile.cs` and `VanillaExpansionProfile.cs` to expose route lookup and expansion location descriptor APIs. `VanillaExpansionProfile` must return no routes, no shed greenhouse descriptors, and preserve vanilla invariance. Existing entrance/content/premium APIs remain compatible. *(BR-T10-01, BR-T10-02, NFR-T10-14)*

- [x] **Step 4 - Populate SVE route definitions and descriptors.** Modify `SveExpansionProfile.cs` with centralized TODO-10 SVE constants, supported farm-signature route definitions for IF2R, Grandpa's Farm, and Frontier Farm, descriptor data for `Custom_GrandpasShedGreenhouse` as `GreenhouseWork`, and `Custom_GrandpasShed` as `DepositOnly`. Include work-entry, deposit-entry, and return route purposes where required by runtime flow. No SVE strings should be scattered outside the profile or route model. *(S-25, S-26, BR-T10-02, BR-T10-03, BR-T10-08, BR-T10-19)*

- [x] **Step 5 - Add live route-shape discovery and shift-readiness validation.** Modify `ExpansionCompatService.cs` to expose profile-backed expansion location discovery and total route validation. Validation must compute the live farm signature, resolve source/target locations, check map bounds, approach/arrival usability, passability/reachability where available, and return typed success/failure without throwing for expected missing world state. No passability cache across days/saves/reloads. *(NFR-T10-02, NFR-T10-05, NFR-T10-08)*

- [x] **Step 6 - Add `CrossLocationRouteNavigator`.** Create `Dayswork/Orchestration/CrossLocationRouteNavigator.cs` to execute a validated ordered-hop route using existing worker movement and warp/location-transition primitives. It walks to each approach tile before transition, places the worker at each arrival tile, reports completion or navigation failure, and does not own skip/deposit/mail/overflow policy. *(BR-T10-12, BR-T10-15, P-T10-NFR-04)*

- [x] **Step 7 - Wire work-scope discovery for the shed greenhouse.** Modify `ChestResolver.GetBuildingOutlines(...)` and related compat calls so a virtual `BuildingOutline` for `Custom_GrandpasShedGreenhouse` is appended only when route-shape discovery succeeds. Modify `LegacyScopeBootstrapper` only if needed so the virtual outline is classified as the existing single `GreenhouseSelection(LocationName)` while `Custom_GrandpasShed` never becomes a work scope. *(BR-T10-04, BR-T10-07, BR-T10-08, NFR-T10-18)*

- [x] **Step 8 - Wire draft-aware expansion destination filtering.** Modify `ChestResolver` and `OutputDestinationsMenu` so chests in `Custom_GrandpasShedGreenhouse` and `Custom_GrandpasShed` are offered only when the current draft selected `GreenhouseSelection("Custom_GrandpasShedGreenhouse")`, and only for shed-greenhouse output tasks. Keep ordinary farm/building chest, mail, and shipping-bin behavior unchanged. *(BR-T10-17, BR-T10-19, BR-T10-21, P-T10-NFR-07)*

- [x] **Step 9 - Integrate shed greenhouse work routes in `ShiftOrchestrator`.** Modify greenhouse batch entry/exit handling so selected `Custom_GrandpasShedGreenhouse` work uses validated expansion routes and `CrossLocationRouteNavigator` instead of the single-building door path. Existing standard greenhouse and vanilla/SVE non-shed paths stay unchanged. Route failures skip only the affected shed greenhouse work batch and continue remaining work. *(BR-T10-05, BR-T10-09, BR-T10-13, BR-T10-16)*

- [x] **Step 10 - Integrate expansion chest deposit routes and item safety.** Modify deposit-trip start/return handling in `ShiftOrchestrator` so chest destinations in `Custom_GrandpasShedGreenhouse` or `Custom_GrandpasShed` use validated expansion deposit routes. Route, navigation, stand-tile, missing chest, full chest, or transfer failures must preserve all items through existing undelivered/overflow behavior. *(BR-T10-18, BR-T10-20, NFR-T10-10)*

- [x] **Step 11 - Add one-warning route failure logging.** Add or reuse a warning payload path so each failed expansion route attempt emits one maintainer-facing warning with route id, purpose, target, first failing hop when known, and reason. Do not add new player-facing mail, HUD errors, or needs-attention contract state for route unavailability. *(BR-T10-22, BR-T10-23, BR-T10-24, NFR-T10-11, NFR-T10-12)*

- [x] **Step 12 - Add route-definition example tests.** Under `Dayswork.Tests/Compat`, add tests covering Vanilla no-op route behavior, SVE supported farm-signature route lookup, unsupported signatures returning no route, hop order/contiguity, no direct farm-to-`Custom_GrandpasShedGreenhouse` success shortcut where intermediate shed route data is required, and descriptor roles/eligibility. *(NFR-T10-22)*

- [x] **Step 13 - Add FsCheck route-model properties and generators.** Add reusable TODO-10 domain generators for route definitions, requests, farm signatures, route purposes, hop lists, descriptors, failures, policy inputs, destination filters, and item stacks. Add properties for deterministic lookup, contiguous hop order, total validation/policy result mapping where pure, destination eligibility, and item identity/quantity preservation. Keep FsCheck shrinking/reproducibility enabled. *(PBT-03, PBT-07, PBT-08, PBT-09)*

- [x] **Step 14 - Add UI/filtering and policy example tests.** Add or extend tests for standard greenhouse versus shed greenhouse selection, no shed/main-shed chest leakage when standard greenhouse or no greenhouse is selected, main shed not appearing as work scope, route-failure policy mapping to skip/deposit-undelivered, and vanilla/no-SVE invariance. *(S-25, S-26, NFR-T10-22)*

- [x] **Step 15 - Run automated verification.** Run `dotnet build Dayswork.sln /p:EnableModDeploy=false` and `dotnet test Dayswork.sln /p:EnableModDeploy=false`. Fix any compile/test fallout before proceeding. If a sandbox/network/deploy constraint blocks a required command, request escalation according to the environment rules. *(Build/test verification; PBT execution)*

- [x] **Step 16 - Write Code Generation summary and update workflow tracking.** Create `aidlc-docs/construction/u-t10-shed-greenhouse-routing/code/code-summary.md` with modified/created files, source-grounded SVE route data notes, test coverage, verification output, known manual-playtest requirements, and deviations if any. Update this plan checklist, `aidlc-state.md`, and `audit.md`; present the standardized Code Generation completion gate. *(AI-DLC bookkeeping)*

## Story Traceability

| Story | Covered by steps |
|---|---|
| S-25 Grandpa's Shed greenhouse selectable crop-work location | Steps 1, 4, 5, 7, 9, 10, 11, 12, 14, 15 |
| S-26 Profile-driven expansion compatibility and testability | Steps 2, 3, 4, 5, 12, 13, 14, 15 |

## PBT Compliance Plan

| Rule | Planning result |
|---|---|
| PBT-02 | N/A unless generation introduces a reversible parse/format or serialize/deserialize operation. If it does, add a round-trip property before completing Code Generation. |
| PBT-03 | Covered by Steps 12 and 13 for route, descriptor, policy, filter, and item-safety invariants. |
| PBT-07 | Covered by Step 13 with reusable domain generators. |
| PBT-08 | Covered by Step 13 and Step 15; do not disable FsCheck shrinking or reproducibility. |
| PBT-09 | Already satisfied by selected FsCheck stack; Step 13 uses it directly. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
