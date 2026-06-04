# U-MC-01 Tech Stack Decisions

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation  
**Stage**: CONSTRUCTION - NFR Requirements  
**Status**: Review required

## Decisions

| Area | Decision | Rationale |
|---|---|---|
| Runtime language | C# 10 on .NET 6 | Matches `Dayswork.Core` and current solution configuration. |
| Production project | `Dayswork.Core` | U-MC-01 is pure domain, planner, and persistence-mapping logic with no SMAPI dependency. |
| Core dependencies | Existing Newtonsoft.Json only | `SaveDataSerializer` already uses Newtonsoft.Json; no new runtime dependency is required. |
| Mod project | No U-MC-01 production code in `Dayswork` except later integration hooks if unavoidable | Live Stardew data, shop reads, chests, and menus belong to later units. |
| Persistence format | Existing JSON envelope with `SchemaVersion = 3` | Functional Design Q1=B selected additive nullable crop-plan field rather than a save-envelope bump. |
| Test framework | xUnit | Existing test project standard. |
| PBT framework | FsCheck.Xunit 2.16.5 | Required by PBT-09; already present in `Dayswork.Tests.csproj` and integrates with xUnit. |
| Nullable/warnings | Nullable enabled and warnings as errors | Existing `Dayswork.Core` and `Dayswork.Tests` project policy. |
| Package additions | None | U-MC-01 can be implemented with current dependencies. |

## PBT-09 Verification

| Criterion | Status | Evidence |
|---|---|---|
| Framework selected | Compliant | FsCheck.Xunit selected for C#/.NET. |
| Included as dependency | Compliant | `Dayswork.Tests.csproj` references `FsCheck.Xunit` version `2.16.5`. |
| Custom generators supported | Compliant | FsCheck supports `Arbitrary<T>` and generator composition. |
| Shrinking supported | Compliant | FsCheck supports shrinking by default when generators/shrinkers are composed normally. |
| Seed reproducibility supported | Compliant | FsCheck/xUnit reports replay information for failing properties. |
| Test-runner integration | Compliant | FsCheck.Xunit integrates with xUnit, already used by the project. |

## Code Generation Implications

- Add production types under `Dayswork.Core` only unless integration with existing domain constructors requires minimal cross-file updates.
- Add DTO types under `Dayswork.Core/Persistence/Dto` and mapping in `SaveDataSerializer`.
- Add tests under `Dayswork.Tests`, preferably grouped by Manage Crops or U-MC-01.
- Add reusable generators for crop-plan domain values rather than local raw primitive generation.
- Keep example-based tests beside property tests for save compatibility and business-critical planner behavior.

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops; no security tech stack addition. |
| Property-Based Testing | Compliant | PBT-09 is fully satisfied by the existing FsCheck.Xunit stack and documented here. |

