# Code Summary - U-MC-01 Crop-plan Domain + Persistence Foundation

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation
**Stage**: CONSTRUCTION - Code Generation
**Status**: Complete; review required

## Summary

U-MC-01 added the pure Core crop-plan foundation for Manage Crops and persisted it additively on contracts without changing the save envelope version.

## Created Application Files

- `Dayswork.Core/Crops/*` for crop-plan domain records, planner input/output records, season assignment, viability, supply, store resolution, shift planning, and crop-plan serialization mapping.
- `Dayswork.Core/Persistence/Dto/CropPlanDtoV1.cs`
- `Dayswork.Core/Persistence/Dto/CropZoneAssignmentDtoV1.cs`
- `Dayswork.Core/Persistence/Dto/SeasonCropChoiceDtoV1.cs`
- `Dayswork.Core/Persistence/Dto/ChestRefDtoV1.cs`
- `Dayswork.Tests/Generators/ManageCropsGen.cs`
- `Dayswork.Tests/ManageCrops/*Tests.cs`

## Modified Application Files

- `Dayswork.Core/Domain/Contract.cs` now carries `CropPlan` with a compatibility constructor defaulting to `CropPlan.Empty`.
- `Dayswork.Core/Domain/WorkScopeSet.cs` now carries optional `ManagedCropWorkScope`.
- `Dayswork.Core/Pricing/IWorkScopeClassifier.cs` and `Dayswork.Core/Pricing/WorkScopeClassifier.cs` now accept an optional crop plan and project enabled crop plans into `WorkScopeSet.ManagedCrops`.
- `Dayswork.Core/Persistence/Dto/ContractDtoV2.cs` adds nullable `CropPlan`.
- `Dayswork.Core/Persistence/SaveDataSerializer.cs` maps missing/null crop plans to `CropPlan.Empty` and writes only non-empty crop-plan DTOs.
- Persistence test generators, structural comparer, and serializer tests were updated for crop-plan defaults and malformed crop-plan skip behavior.

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` passed.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` passed: 373 passed, 1 existing skipped.
- Duplicate-file and scope checks passed: no generated application code under `aidlc-docs`, no project dependency changes, and no duplicate brownfield replacement files.

## Extension Compliance

- Security Baseline: N/A. Disabled for Manage Crops in requirements.
- Property-Based Testing: Compliant. Added reusable Manage Crops generators plus properties for DTO round-trip, viability determinism and greenhouse bypass, supply atomicity, season assignment idempotence, store determinism/festival behavior, and tile-action ordering.
