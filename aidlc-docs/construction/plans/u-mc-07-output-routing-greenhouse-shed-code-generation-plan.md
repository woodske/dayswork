# Code Generation Plan - U-MC-07 Output Routing + Greenhouse/Shed

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stories**: S-31, S-32  
**Stage**: CONSTRUCTION - Code Generation (single source of truth for generation)  
**Package order**: `Dayswork.Core` -> `Dayswork` -> `Dayswork.Tests`  
**Workspace root**: `C:\Users\kwood\Repos\dayswork` (brownfield - modify existing files in place)

## Context and Boundary

U-MC-07 completes Manage Crops by adding:

- per-zone managed-crop harvest output routing through managed-crop provenance;
- season-agnostic greenhouse and SVE Grandpa's Shed greenhouse authoring;
- managed-crop batches for non-farm locations;
- live-location `Diggable` field reads;
- greenhouse/shed entry, shopping re-entry, return, and deposit path reuse.

Out of scope:

- new save schema or migration;
- new runtime packages;
- new infrastructure;
- new shop rules or pricing changes;
- ordinary non-managed `HarvestCrops` routing changes.

## Dependencies

- U-MC-01 crop-plan domain, pure planners, DTOs, and FsCheck generators.
- U-MC-02 cabin input/output chests and `ChestResolver`.
- U-MC-03/U-MC-04 crop-group UI and zone draw overlay.
- U-MC-05 managed-crop field runner, action map, and coexistence tile exclusion.
- U-MC-06 shopping manifest, store routes, headless purchase, and visible shopping return.
- TODO-10/SVE route descriptors for `Custom_GrandpasShedGreenhouse`.

## Planning Checklist

- [x] Load Code Generation rule details from `.aidlc-rule-details/construction/code-generation.md`.
- [x] Load common workflow, session-continuity, content-validation, and question-format rules.
- [x] Load U-MC-07 Functional Design, NFR Requirements, and NFR Design artifacts.
- [x] Load Manage Crops story map and confirm S-31/S-32 traceability.
- [x] Inspect current code seams and target paths.
- [x] Validate plan content uses plain Markdown with no Mermaid or ASCII diagrams.
- [x] Log NFR Design approval and this code-generation plan approval prompt in `audit.md`.
- [x] Update `aidlc-state.md` to the U-MC-07 Code Generation Planning review gate.

## Risk Callouts

- **R1 - Runtime location handoff**: Managed crops currently assume `Game1.getFarm()` in the field reader and runner. Generation must keep all existing farm behavior green while introducing greenhouse/shed entry and return.
- **R2 - Output routing regression**: Ordinary `HarvestCrops` destination behavior is high-risk. Pure planner tests must prove managed provenance only affects matching managed-crop items.
- **R3 - SVE shed route availability**: Grandpa's Shed greenhouse must reuse the existing expansion profile route descriptors. Missing or invalid routes skip only that batch.
- **R4 - UI scope creep**: The authoring UI should extend current crop-group menus and draw overlay, not introduce a parallel flow.

## Part 2 - Generation Steps

### Core routing and planning

- [x] **S1** Extend `Dayswork.Core/Domain/OutputScopeFamily.cs` with `ManagedCrop` and `Dayswork.Core/Domain/OutputScopeProvenance.cs` with `ManagedCrop(string assignmentKey)`.
- [x] **S2** Add a pure managed-crop provenance key/destination helper under `Dayswork.Core/Crops` or `Dayswork.Core/Inventory`:
      build deterministic assignment keys from group id, location, and zone bounds; build provenance destination maps only for assignments with `OutputChest`.
- [x] **S3** Extend `Dayswork.Core/Crops/TileAction.cs` with optional `OutputScopeProvenance? OutputProvenance`; set managed-crop provenance on harvest actions produced by `CropShiftPlanner` for each assignment.
- [x] **S4** Extend `Dayswork.Core/Inventory/IDepositPlanner.cs` and `DepositPlanner.cs` with a provenance-aware `Plan(...)` overload. Preserve the existing overload by delegating with an empty provenance map. Destination order: provenance map -> task map -> automatic fallback.
- [x] **S5** Update `Dayswork.Core/Shifts/ShiftPlanBuilder.cs` so managed-crop batches are emitted for every distinct managed assignment location (`Farm`, `Greenhouse`, supported expansion greenhouse locations), with managed batches ordered before ordinary crop work for the same location.
- [x] **S6** Keep `ManagedZoneTileSet` location-scoped and update any runtime use sites that currently pass only farm managed zones so general crop work is excluded only inside the active location's managed zones.

### Authoring UI and draft projection

- [x] **S7** Extend `Dayswork/UI/CropPlanDraft.cs` and `CropGroupDraft` with group `LocationName`, `CropAssignmentMode`, and a season-agnostic slot. Projection and hydration must preserve location, crop, fertilizer, replant, output chest, and zones without a schema bump.
- [x] **S8** Update `Dayswork/UI/ManageCropsMenu.cs` and `Dayswork/UI/CropGroupEditorMenu.cs` to show location labels/selectors and switch between seasonal rows for `Farm` and one year-round row for greenhouse/shed groups.
- [x] **S9** Update `Dayswork/UI/HiringFlowCoordinator.cs` crop/fertilizer/output-picker routing so season-agnostic crop selection uses `CropCatalogProvider.GetCatalog(null, greenhouse: true)` and location changes clear stale zones.
- [x] **S10** Extend `Dayswork/UI/ZoneDrawMenu.cs`, `Dayswork/UI/ZoneDrawOverlay.cs`, and draw launch code so managed-crop draw sessions can target the selected live location and persist zones with that location name; protected zones must be filtered by location.
- [x] **S11** Update `Dayswork/Integration/ChestResolver.cs`, output chest picker wiring, and `Dayswork/i18n/default.json` so explicit managed-crop output choices exclude both built-in office chests while the automatic fallback option remains available.

### Runtime greenhouse/shed execution and output routing

- [x] **S12** Update `Dayswork/Orchestration/ManagedCropFieldReader.cs` to read the active `GameLocation`, accept a caller-provided season-agnostic flag, and keep tile reads bounded to assigned zone tiles.
- [x] **S13** Update `Dayswork/Orchestration/ShiftOrchestrator.ManagedCrops.cs` to resolve the active managed-crop batch location, enter vanilla greenhouse or expansion greenhouse before planning, and build/replan actions from that live location instead of always using the farm.
- [x] **S14** Update managed-crop action execution so harvest actions set `_pendingOutputProvenance` from `TileAction.OutputProvenance`; ordinary managed non-harvest actions and non-managed crop work must keep existing provenance behavior.
- [x] **S15** Update `Dayswork/Orchestration/ShiftOrchestrator.ManagedCropShopping.cs` so shopping for a non-farm managed-crop batch returns supplies to the input chest, then re-enters the active greenhouse/shed location before replanning supply-dependent work.
- [x] **S16** Update `Dayswork/Orchestration/ShiftOrchestrator.Deposit.cs` to pass the managed-crop provenance destination map into `DepositPlanner` and reuse existing chest/overflow/mail/deposit-route behavior.
- [x] **S17** Ensure greenhouse and SVE shed route failures skip only the affected managed-crop batch with diagnostics and leave the rest of the shift runnable.

### Tests

- [x] **S18** Extend `Dayswork.Tests/Inventory/DepositPlannerTests.cs` and `Dayswork.Tests/Generators/DepositInputGen.cs` with example and FsCheck coverage for provenance-first routing, automatic fallback, and ordinary harvest routing preservation.
- [x] **S19** Add or extend Manage Crops Core tests under `Dayswork.Tests/ManageCrops/` for managed-crop provenance key equality/distinction, destination map construction, TileAction harvest provenance, and location-scoped zone exclusion.
- [x] **S20** Extend `Dayswork.Tests/Shifts/ManagedCropBatchPlanTests.cs` and/or `ShiftPlanBuilderTests.cs` with examples and properties for one managed batch per distinct location and managed-before-general crop ordering.
- [x] **S21** Extend `Dayswork.Tests/UI/CropPlanDraftTests.cs` for season-agnostic projection/hydration, location-change zone clearing, protected-zone location filtering, and output chest preservation.
- [x] **S22** Add focused Mod-layer examples where practical for chest filtering, crop catalog greenhouse picker routing, and expansion greenhouse availability; keep live rendered UI and route walking for manual playtest.
- [x] **S23** Run `dotnet build Dayswork.sln /p:EnableModDeploy=false` and `dotnet test Dayswork.sln /p:EnableModDeploy=false`; resolve all build/test failures before closing.

### Closeout

- [x] **S24** Run deploy-enabled `dotnet build Dayswork.sln` when compile/test verification is green.
- [x] **S25** Create `aidlc-docs/construction/u-mc-07-output-routing-greenhouse-shed/code/code-summary.md` with modified/created files, behavior delivered, test results, PBT compliance, and manual playtest checklist.
- [x] **S26** Update this plan checkboxes, `aidlc-docs/aidlc-state.md`, and `aidlc-docs/audit.md`; present the standardized Code Generation completion message and stop at the U-MC-07 code review/playtest gate.

## Story Traceability

- **S-31 - Two cabin chests + per-zone output routing**: S1-S4, S11, S14, S16, S18-S19.
- **S-32 - Greenhouse & Grandpa's Shed crops**: S5-S10, S12-S13, S15, S17, S20-S22.
- **FR-MC-23** greenhouse/shed viability bypass: S7, S12, S21.
- **FR-MC-28** coexistence with ordinary crop tasks: S5-S6, S20.
- **FR-MC-29** per-zone output routing: S1-S4, S14, S16, S18-S19.
- **FR-MC-43/44** greenhouse/SVE shed support and live `Diggable`: S10, S12-S13, S17, S22.

## Expected Application Code Paths

- `Dayswork.Core/Domain/OutputScopeFamily.cs`
- `Dayswork.Core/Domain/OutputScopeProvenance.cs`
- `Dayswork.Core/Crops/TileAction.cs`
- `Dayswork.Core/Crops/CropShiftPlanner.cs`
- `Dayswork.Core/Crops/ManagedZoneTileSet.cs`
- `Dayswork.Core/Shifts/ShiftPlanBuilder.cs`
- `Dayswork.Core/Inventory/IDepositPlanner.cs`
- `Dayswork.Core/Inventory/DepositPlanner.cs`
- `Dayswork/UI/CropPlanDraft.cs`
- `Dayswork/UI/ManageCropsMenu.cs`
- `Dayswork/UI/CropGroupEditorMenu.cs`
- `Dayswork/UI/CropListPickerMenu.cs`
- `Dayswork/UI/ZoneDrawMenu.cs`
- `Dayswork/UI/ZoneDrawOverlay.cs`
- `Dayswork/UI/HiringFlowCoordinator.cs`
- `Dayswork/Integration/ChestResolver.cs`
- `Dayswork/Integration/CropCatalogProvider.cs`
- `Dayswork/Orchestration/ManagedCropFieldReader.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.ManagedCrops.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.ManagedCropShopping.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.Deposit.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.cs`
- `Dayswork/Orchestration/WorkAreaScanner.cs`
- `Dayswork/i18n/default.json`
- `Dayswork.Tests/Inventory/DepositPlannerTests.cs`
- `Dayswork.Tests/Generators/DepositInputGen.cs`
- `Dayswork.Tests/Generators/ManageCropsGen.cs`
- `Dayswork.Tests/ManageCrops/*.cs`
- `Dayswork.Tests/Shifts/ManagedCropBatchPlanTests.cs`
- `Dayswork.Tests/UI/CropPlanDraftTests.cs`
- `Dayswork.Tests/Integration/ChestResolverTests.cs`

## Documentation Outputs

- `aidlc-docs/construction/u-mc-07-output-routing-greenhouse-shed/code/code-summary.md`

## Extension Compliance

| Extension | Status | Plan impact |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops; U-MC-07 adds no network, auth, PII, secrets, or external service surface. |
| Property-Based Testing | Compliant | S18-S22 carry full-mode PBT obligations for pure routing, provenance, batch grouping, draft projection, and location-scoped invariants; live SMAPI behavior is example/manual playtest covered. |
