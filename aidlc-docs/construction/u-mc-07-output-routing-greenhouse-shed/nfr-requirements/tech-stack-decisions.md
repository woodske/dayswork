# U-MC-07 Tech Stack Decisions

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - NFR Requirements  
**Status**: Review required

## Decisions

| Area | Decision | Rationale |
|---|---|---|
| Runtime language | C# 10 on .NET 6 | Matches `Dayswork`, `Dayswork.Core`, and current solution configuration. |
| Core production project | `Dayswork.Core` | Provenance keys, destination precedence, batch grouping, and draft projection should remain pure where possible. |
| Mod production project | `Dayswork` | Live Stardew locations, chests, shop re-entry, greenhouse/shed navigation, i18n, and menu rendering belong at the SMAPI boundary. |
| Test project | `Dayswork.Tests` | Existing xUnit/FsCheck project for examples and property tests. |
| PBT framework | FsCheck.Xunit 2.16.5 | Required by PBT-09; already referenced by `Dayswork.Tests.csproj` and integrated with xUnit. |
| UI framework | Existing Stardew/IClickableMenu UI components | U-MC-07 extends current Manage Crops menus rather than adding a new UI framework. |
| Navigation stack | Existing `BuildingWorkNavigator`, `CrossLocationRouteNavigator`, and expansion route descriptors | Greenhouse and SVE shed work/deposit routes must reuse established route seams. |
| Deposit stack | Existing `DepositPlanner`, `DestinationKey`, `ChestDestination`, `AutomaticOutputDestination`, and overflow/mail handling | U-MC-07 adds provenance precedence without replacing destination concepts. |
| Expansion compatibility | Existing `ExpansionProfileSelector`, `ExpansionCompatService`, and `SveExpansionProfile` | Keeps SVE-specific behavior isolated and preserves vanilla/no-SVE invariance. |
| Persistence format | Existing crop-plan DTO shape; no schema bump | Functional Design confirms current fields cover location, mode, group id, zones, and output chest. |
| Package additions | None | U-MC-07 can be implemented with current runtime and test dependencies. |
| Nullable/warnings | Nullable enabled and warnings as errors | Existing project policy in all three C# projects. |

## PBT-09 Verification

| Criterion | Status | Evidence |
|---|---|---|
| Framework selected | Compliant | FsCheck.Xunit selected for C#/.NET property testing. |
| Included as dependency | Compliant | `Dayswork.Tests.csproj` references `FsCheck.Xunit` version `2.16.5`. |
| Custom generators supported | Compliant | FsCheck supports `Arbitrary<T>` and generator composition for domain-specific generators. |
| Shrinking supported | Compliant | FsCheck supports shrinking by default when generators and shrinkers are composed normally. |
| Seed reproducibility supported | Compliant | FsCheck/xUnit reports replay information for failing properties. |
| Test-runner integration | Compliant | FsCheck.Xunit integrates with xUnit, the existing test runner. |

## Code Generation Implications

- Add pure tests for managed-crop provenance destination precedence before touching live SMAPI behavior.
- Preserve the existing `DepositPlanner.Plan(...)` overload and add a provenance-aware overload for managed-crop routing.
- Keep live greenhouse/shed route resolution in Mod-layer runners and adapters; pure Core should consume location strings, assignment shapes, and destination maps.
- Add example tests for the business-critical routing paths even when a property covers the same invariant.
- Add or extend reusable FsCheck generators for crop assignments, location-scoped zones, output chest references, destination keys, buffered harvested items, and season-agnostic crop-group draft shapes.
- Keep manual playtest instructions for visible greenhouse and SVE shed greenhouse flows in the later Build and Test stage.

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops; no security stack addition. |
| Property-Based Testing | Compliant | PBT-09 is fully satisfied by the existing FsCheck.Xunit stack and documented here. |

