# Code Summary - U-MC-07 Output Routing + Greenhouse/Shed

**Completed**: 2026-06-07T05:20:52Z  
**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stories**: S-31, S-32

## Behavior Delivered

- Added managed-crop output provenance (`OutputScopeFamily.ManagedCrop`) and deterministic per-assignment provenance keys based on group id, location, and zone bounds.
- Added provenance-first deposit planning while preserving the existing task destination fallback and automatic output fallback.
- Tagged managed-crop harvest actions with per-zone provenance so harvested output can route to each crop group's explicit output chest.
- Emitted managed-crop batches for every distinct managed assignment location, including `Farm`, `Greenhouse`, and expansion greenhouse locations, ordered ahead of ordinary crop work for the same location.
- Extended Manage Crops authoring with group location selection, farm seasonal rows, greenhouse/shed year-round rows, location-scoped zone drawing, and location-filtered protected zones.
- Updated managed-crop runtime execution to read and plan against the active live location, enter vanilla/expansion greenhouse locations before planning, re-enter non-farm managed locations after shopping, and pass managed-crop provenance destination maps into deposit planning.
- Excluded both built-in Farmhand Cabin office chests from explicit chest selections while leaving the automatic output fallback available.

## Modified Application Files

- `Dayswork.Core/Domain/OutputScopeFamily.cs`
- `Dayswork.Core/Domain/OutputScopeProvenance.cs`
- `Dayswork.Core/Crops/ManagedCropOutputRouter.cs`
- `Dayswork.Core/Crops/TileAction.cs`
- `Dayswork.Core/Crops/CropShiftPlanner.cs`
- `Dayswork.Core/Shifts/ShiftPlanBuilder.cs`
- `Dayswork.Core/Inventory/IDepositPlanner.cs`
- `Dayswork.Core/Inventory/DepositPlanner.cs`
- `Dayswork/UI/CropPlanDraft.cs`
- `Dayswork/UI/CropGroupLocationOption.cs`
- `Dayswork/UI/ManageCropsMenu.cs`
- `Dayswork/UI/CropGroupEditorMenu.cs`
- `Dayswork/UI/HiringFlowCoordinator.cs`
- `Dayswork/UI/ZoneDrawMenu.cs`
- `Dayswork/Integration/ChestResolver.cs`
- `Dayswork/Orchestration/ManagedCropFieldReader.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.ManagedCrops.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.ManagedCropShopping.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.Deposit.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.Routing.cs`
- `Dayswork/i18n/default.json`

## Tests Added Or Extended

- `Dayswork.Tests/Inventory/DepositPlannerTests.cs`
- `Dayswork.Tests/Generators/DepositInputGen.cs`
- `Dayswork.Tests/ManageCrops/ManagedCropOutputRouterTests.cs`
- `Dayswork.Tests/ManageCrops/CropCatalogTests.cs`
- `Dayswork.Tests/Shifts/ManagedCropBatchPlanTests.cs`
- `Dayswork.Tests/UI/CropPlanDraftTests.cs`
- `Dayswork.Tests/Integration/ChestResolverTests.cs`

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false`: passed, 0 warnings, 0 errors.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false`: passed, 488 passed, 1 expected skip, 0 failed.
- `dotnet build Dayswork.sln`: passed, 0 warnings, 0 errors; copied mod files to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.

## Extension Compliance

- **Security Baseline**: N/A. Disabled for Manage Crops in state; U-MC-07 added no auth, secrets, network, PII, or external service surface.
- **Property-Based Testing**: Compliant. Added provenance-aware deposit planner properties and focused examples, plus deterministic managed-crop routing, batch ordering, draft projection/hydration, and chest filtering examples. Live UI rendering and route walking remain manual playtest items.

## Manual Playtest Checklist

- Create a Farm managed crop group and confirm seasonal rows, zone drawing, harvest, deposit, and automatic fallback still behave as before.
- Create two managed crop groups with different explicit output chests and confirm harvested output routes by group.
- Create a vanilla Greenhouse managed group, confirm the editor shows one year-round crop row, draw zones inside Greenhouse, and confirm the worker enters before planning.
- With SVE active, create a Grandpa's Shed Greenhouse group and confirm route validation/entry; if route validation fails, confirm only that managed batch is skipped and later batches still run.
- Trigger managed-crop shopping for a greenhouse/shed group and confirm the worker deposits purchases into the office input chest, re-enters the active crop location, then replans and plants.
