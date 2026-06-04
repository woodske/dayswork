# Code Generation Plan - U-MC-01 Crop-plan Domain + Persistence Foundation

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation  
**Stage**: CONSTRUCTION - Code Generation Part 1 (Planning)  
**Status**: Complete; review required  
**This plan is the single source of truth for U-MC-01 Code Generation Part 2.**

## Planning Checklist

- [x] Load Code Generation rule details.
- [x] Load U-MC-01 Functional Design artifacts.
- [x] Load U-MC-01 NFR Requirements and NFR Design artifacts.
- [x] Load unit-of-work and story-map context.
- [x] Inspect existing Core domain, persistence, serializer, scope classifier, and tests.
- [x] Determine application-code and test-code paths.
- [x] Create this executable code generation plan.
- [x] Log the approval prompt in `aidlc-docs/audit.md`.
- [x] Receive explicit approval before changing application code.

## Unit Context

**Stories implemented by this unit**

| Story | U-MC-01 responsibility |
|---|---|
| S-34 | Persist crop plan data additively in schema 3; missing plan loads as empty/disabled. |
| S-35 | Provide pure Core crop-plan seams and PBT-ready planner/test boundaries. |

**Dependencies**

- Prior construction units are already built.
- U-MC-01 has no dependency on later Manage Crops units.
- Later units U-MC-02 through U-MC-07 depend on the domain/planner/persistence seams created here.

**Application code roots**

- Core production code: `Dayswork.Core/`
- Tests: `Dayswork.Tests/`
- Documentation summary only: `aidlc-docs/construction/u-mc-01-domain-persistence/code/`

**Brownfield modification rules**

- Modify existing files in place where they already own behavior.
- Create new files only for new domain/planner/DTO/test types.
- Do not create duplicate files such as `Contract_new.cs` or `SaveDataSerializer_modified.cs`.

## Expected Application Files

### New Core files

- `Dayswork.Core/Crops/CropPlan.cs`
- `Dayswork.Core/Crops/CropZoneAssignment.cs`
- `Dayswork.Core/Crops/CropAssignmentMode.cs`
- `Dayswork.Core/Crops/SeasonCropChoice.cs`
- `Dayswork.Core/Crops/StorePreference.cs`
- `Dayswork.Core/Crops/ManagedCropWorkScope.cs`
- `Dayswork.Core/Crops/CropDescriptor.cs`
- `Dayswork.Core/Crops/SupplyInventory.cs`
- `Dayswork.Core/Crops/SupplyTarget.cs`
- `Dayswork.Core/Crops/PurchaseLine.cs`
- `Dayswork.Core/Crops/ShopStockSnapshot.cs`
- `Dayswork.Core/Crops/Store.cs`
- `Dayswork.Core/Crops/StoreResolution.cs`
- `Dayswork.Core/Crops/StoreClosedReason.cs`
- `Dayswork.Core/Crops/ManagedCropShiftPlan.cs`
- `Dayswork.Core/Crops/TileAction.cs`
- `Dayswork.Core/Crops/ManagedCropActionKind.cs`
- `Dayswork.Core/Crops/FieldState.cs`
- `Dayswork.Core/Crops/TileState.cs`
- `Dayswork.Core/Crops/SeasonAssignmentResolver.cs`
- `Dayswork.Core/Crops/PlantingViabilityCalculator.cs`
- `Dayswork.Core/Crops/CropSupplyPlanner.cs`
- `Dayswork.Core/Crops/StoreResolver.cs`
- `Dayswork.Core/Crops/CropShiftPlanner.cs`
- `Dayswork.Core/Crops/CropPlanSerialization.cs`
- `Dayswork.Core/Persistence/Dto/CropPlanDtoV1.cs`
- `Dayswork.Core/Persistence/Dto/CropZoneAssignmentDtoV1.cs`
- `Dayswork.Core/Persistence/Dto/SeasonCropChoiceDtoV1.cs`
- `Dayswork.Core/Persistence/Dto/ChestRefDtoV1.cs`

### Modified Core files

- `Dayswork.Core/Domain/Contract.cs`
- `Dayswork.Core/Domain/WorkScopeSet.cs`
- `Dayswork.Core/Pricing/WorkScopeClassifier.cs`
- `Dayswork.Core/Persistence/Dto/ContractDtoV2.cs`
- `Dayswork.Core/Persistence/SaveDataSerializer.cs`

### New or modified test files

- `Dayswork.Tests/Generators/ManageCropsGen.cs`
- `Dayswork.Tests/ManageCrops/CropPlanSerializationTests.cs`
- `Dayswork.Tests/ManageCrops/SeasonAssignmentResolverTests.cs`
- `Dayswork.Tests/ManageCrops/PlantingViabilityCalculatorTests.cs`
- `Dayswork.Tests/ManageCrops/CropSupplyPlannerTests.cs`
- `Dayswork.Tests/ManageCrops/StoreResolverTests.cs`
- `Dayswork.Tests/ManageCrops/CropShiftPlannerTests.cs`
- `Dayswork.Tests/ManageCrops/ManageCropsPropertyTests.cs`
- `Dayswork.Tests/Persistence/SaveDataSerializerTests.cs`
- `Dayswork.Tests/Persistence/SaveDataSerializerPropertyTests.cs`
- `Dayswork.Tests/Persistence/Generators/U19PersistenceGen.cs`
- `Dayswork.Tests/Persistence/ContractStructuralComparer.cs`

## Generation Steps

- [x] **Step 1 - Core crop domain records and enums**  
  Create the crop-plan domain files under `Dayswork.Core/Crops/`: `CropPlan`, `CropZoneAssignment`, `CropAssignmentMode`, `SeasonCropChoice`, `StorePreference`, `ManagedCropWorkScope`, and `CropDescriptor`. Include `CropPlan.Empty`, `IsEnabled`, and defensive construction defaults where appropriate.

- [x] **Step 2 - Pure planner input/output records**  
  Create supply, shop, field-state, tile-state, shift-plan, tile-action, and managed-action result records under `Dayswork.Core/Crops/`. Keep these SMAPI-free and suitable for FsCheck generation.

- [x] **Step 3 - Season assignment resolver**  
  Implement `SeasonAssignmentResolver` with multi-season span application, lock derivation, same-choice idempotence, and season-agnostic assignment behavior.

- [x] **Step 4 - Planting viability calculator**  
  Implement `PlantingViabilityCalculator` with first-harvest viability, fertilized-growth input, and greenhouse/shed bypass.

- [x] **Step 5 - Crop supply planner**  
  Implement `CropSupplyPlanner` with `CompletableTiles(...)`, both-components-on-hand logic, purchase-target calculation, chest-supply-only skip, and seed/fertilizer atomicity.

- [x] **Step 6 - Store resolver**  
  Implement `StoreResolver`, `StoreResolution`, and closed-reason handling with deterministic `Either` ordering, festival no-store behavior, preferred-store fallback, and pure stock lookup.

- [x] **Step 7 - Crop shift planner skeleton/composition**  
  Implement `CropShiftPlanner` with pure plan composition, supply-independent/dependent partitioning, per-tile action order, viability filtering, and atomic supply gate usage. Keep live `Diggable` and runtime execution out of scope.

- [x] **Step 8 - Crop plan serialization mapper and DTOs**  
  Add crop-plan DTO files and implement `CropPlanSerialization` mapping between domain and DTO. Preserve nullable/additive schema-3 behavior and deterministic season/zone ordering.

- [x] **Step 9 - Contract and WorkScopeSet integration**  
  Modify `Contract.cs` to include `CropPlan` with compatibility/defaulting support for existing call sites. Modify `WorkScopeSet.cs` to carry nullable `ManagedCropWorkScope` while preserving existing constructors/factory behavior.

- [x] **Step 10 - WorkScopeClassifier projection**  
  Modify `WorkScopeClassifier.cs` to project an enabled crop plan into `WorkScopeSet.ManagedCrops` without affecting existing outdoor, animal, or greenhouse scope behavior when the crop plan is empty.

- [x] **Step 11 - SaveDataSerializer mapping**  
  Modify `ContractDtoV2.cs` and `SaveDataSerializer.cs` to write non-empty crop plans, default missing/null crop plans to `CropPlan.Empty`, and skip only malformed crop-plan contracts through the existing warning path.

- [x] **Step 12 - Update persistence generators and structural comparer**  
  Update `U19PersistenceGen`, `ContractGen` if needed, and `ContractStructuralComparer` so generated contracts and equality checks include crop plans while still producing empty-plan examples for old-save compatibility.

- [x] **Step 13 - Manage Crops FsCheck generators**  
  Add `ManageCropsGen` with domain-specific generators for crop plans, assignments, choices, descriptors, supply inventories, shop snapshots, field states, and shift-planner inputs. Constrain collection sizes and item IDs.

- [x] **Step 14 - Example-based tests**  
  Add focused xUnit examples for missing crop-plan defaulting, schema-3 additive load, malformed crop-plan contract skip, multi-season lock behavior, viability edge cases, seed/fertilizer partial stock, festival no-store, and deterministic action ordering.

- [x] **Step 15 - Property-based tests**  
  Add FsCheck properties for DTO/domain round-trip, viability determinism/greenhouse bypass, seed/fertilizer atomicity, season lock idempotence, store/fallback determinism, and tile-action ordering. Use reusable generators and keep shrinking/replay support enabled.

- [x] **Step 16 - Build and test verification**  
  Run `dotnet build Dayswork.sln /p:EnableModDeploy=false` and `dotnet test Dayswork.sln /p:EnableModDeploy=false`. If failures occur, fix within the approved plan scope and rerun.

- [x] **Step 17 - Duplicate-file and scope verification**  
  Verify no duplicate brownfield files were created, no application code was placed under `aidlc-docs/`, Core remains SMAPI-free, and no new package dependency was added.

- [x] **Step 18 - Code summary and workflow updates**  
  Create `aidlc-docs/construction/u-mc-01-domain-persistence/code/code-summary.md`, update this plan's checkboxes as each step completes, update `aidlc-state.md`, and log completion in `audit.md`.

## Story Traceability

| Story | Plan steps |
|---|---|
| S-34 | Steps 1, 8, 9, 11, 12, 14, 15 |
| S-35 | Steps 1 through 18, with PBT focus in Steps 13 and 15 |

## PBT and Extension Compliance

| Extension | Status | Plan coverage |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops; no network, auth, PII, or security infrastructure in scope. |
| Property-Based Testing | Compliant for planning | Steps 13 and 15 implement PBT-02, PBT-03, PBT-04, PBT-07, PBT-08, and PBT-10 obligations; PBT-09 already satisfied by FsCheck.Xunit. |

## Out of Scope for U-MC-01

- Manage Crops UI and hub wiring.
- Cabin input/output chest creation and backfill.
- Live Stardew crop catalog reads.
- Live `ShopBuilder` transactions.
- Worker runtime execution, animation, map `Diggable` checks, and town navigation.
- Per-zone harvest output routing and SVE shed playtest.

## Approval Gate

Code Generation Part 2 must not begin until this plan is explicitly approved.
