# Code Generation Plan - U-MC-03 Manage Crops Authoring UI

**Unit**: U-MC-03 - Manage Crops Authoring UI
**Stage**: CONSTRUCTION - Code Generation
**Status**: Part 1 plan (pre-authorized to proceed to Part 2 per user direction "continue until I need to answer questions or playtest")

## Part 1 — Plan (this file)

Design decisions carried in: FD Q1=A (single scrolling season-row page), Q2=B (scrollable
pickers), Q3=A (pure Core catalog seam + thin live adapter, PBT), Q4=A (wire begin-draw now,
materialize `CropZoneAssignment` per drawn zone), Q5=A (Seasonal-only authoring), Q6=A (defer
plan-level toggles/store preference), Q7=A (reuse chest-selection idiom), Q8=A ("Done" at ≥1
assignment). NFR: catalog read off the hot path + session memoization, determinism in Core,
draft isolation, gamepad parity, i18n, no schema change, no new dependency.

## Part 2 — Generation Steps

### Pure Core (Dayswork.Core/Crops)
- [x] 1. Add `CropSupplyTag` enum (`AutoBuyable`, `ChestSupplyOnly`).
- [x] 2. Add `CropCatalogEntry` record (`Crop`, `DisplayName`, `Supply`).
- [x] 3. Add `FertilizerOption` record (`ItemId`, `DisplayName`, `Supply`).
- [x] 4. Add `CropCatalogSource` record (raw adapter input: `Crop`, `DisplayName`, `StockedAtPierre`, `StockedAtJoja`).
- [x] 5. Add `CropCatalog` pure builder: `Build(sources, Season? seasonFilter, bool greenhouse)` → deterministic season-filter (skipped when greenhouse), supply-tag, name/id sort; plus `SortFertilizers(...)`.
- [x] 6. Add `CropDescriptor.WithFertilizer(string?)` helper (pure; for authoring fertilizer selection).

### Mod authoring state (Dayswork/UI)
- [x] 7. Add `CropPlanDraft` + `SeasonSlotDraft` + `SeasonLockState`: per-season crop/fertilizer/replant, `OutputChest`, `MaterializedAssignments`; consolidation via `SeasonAssignmentResolver` with conflict rejection (R-08); `HasAnyConfiguredSeason`/`HasAnyAssignment`; `BuildAssignmentChoices()`; `MaterializeForZones(zones)`; `BuildCropPlan()`; `HydrateFrom(CropPlan)`.
- [x] 8. Extend `ContractDraft` with a lazy `CropPlanDraft CropPlan` property (transient).

### Mod live adapter (Dayswork/Integration)
- [x] 9. Add `CropCatalogProvider` (M-25): reads live 1.6 crop data → `CropCatalogSource[]`, season-aware; `GetCatalog(Season? season, bool greenhouse)` (delegates to `CropCatalog`), `GetFertilizers()`; in-session per-season memoization; thin/skip-unmappable.

### Mod UI (Dayswork/UI)
- [x] 10. Add `CropListPickerMenu`: generic scrollable list picker (label + optional tag) reusing `MenuScrollBar`; gamepad/KBM, B-to-back; `Action<int>`/`Action` callbacks. Reused for crop, fertilizer, output-chest pickers.
- [x] 11. Add `ManageCropsMenu` (M-24): single scrolling page — four season rows (crop/fertilizer/replant; locked styling + reason), output-chest row, "Draw zone(s)" button (gated R-17), Back; `draw()` reads precomputed state.
- [x] 12. Extend `HubMenu`: new `onManageCrops` action + "Manage Crops" NavItem + status delegate (Done/Optional, Q8=A).

### Mod wiring (Dayswork/UI)
- [x] 13. Extend `HiringFlowCoordinator`: construct `CropCatalogProvider`; `ShowManageCrops`; crop/fertilizer/chest picker opens; `BeginCropZoneDraw` → reuse `ZoneDrawMenu` → materialize assignments; extend `BuildContract` to attach `CropPlan`; extend `CreateEditDraft` to hydrate `CropPlanDraft`. Update `HubMenu` construction call.

### i18n
- [x] 14. Add new keys to `Dayswork/i18n/default.json` (nav label, page title/help, season names, crop/fertilizer/chest picker chrome, supply tags, lock reason, replant, output labels, draw button, status reuse).

### Tests (Dayswork.Tests)
- [x] 15. Add `Dayswork.Core` `CropCatalogTests` (filter/tag/sort, greenhouse bypass) + `CropDescriptorWithFertilizerTests`.
- [x] 16. Add `CropCatalog`/`WithFertilizer` FsCheck properties to the Manage Crops property suite.
- [x] 17. Add `Dayswork.Tests/UI/CropPlanDraftTests` (configure, multi-season lock/conflict, materialize per zone, build plan, hydrate round-trip).

### Verification
- [x] 18. `dotnet build Dayswork.sln /p:EnableModDeploy=false` → 0/0.
- [x] 19. `dotnet test Dayswork.sln /p:EnableModDeploy=false` → green.
- [x] 20. Write code-summary.md; update aidlc-state.md + audit.md.

## Notes / Deviations
- The `CropCatalogProvider` live-data auto-buyable tagging is best-effort and **display-only** in
  this unit; exact stock/purchasability is U-MC-06's headless transaction. Recorded for playtest.
- No persistence schema change: the authored `CropPlan` rides the existing U-MC-01
  `ContractDtoV2.CropPlan` (written only when non-empty).
