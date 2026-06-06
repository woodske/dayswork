# Code Summary - U-MC-03 Manage Crops Authoring UI

**Unit**: U-MC-03 - Manage Crops Authoring UI
**Stage**: CONSTRUCTION - Code Generation
**Status**: Complete; review required (in-game playtest)

## Summary

U-MC-03 adds the crop-first **Manage Crops** authoring page to the contract hub. The player
configures up to four seasons (crop → fertilizer → auto-replant), optionally assigns an output
chest, then draws zone(s) to apply the whole plan. Decision logic (catalog filter/tag/sort,
multi-season locking, draft→`CropPlan` projection) is pure and testable; live 1.6 crop/shop data
reads are isolated in a thin adapter (Q3=A). No persistence schema version change.

## Created Application Files

**Pure Core (`Dayswork.Core/Crops/`)**
- `CropSupplyTag.cs` — `AutoBuyable` / `ChestSupplyOnly` tag.
- `CropCatalogEntry.cs` — pickable crop row (crop + display name + supply tag).
- `FertilizerOption.cs` — pickable fertilizer row.
- `CropCatalogSource.cs` — raw adapter-supplied record before filter/tag.
- `CropCatalog.cs` — pure `Build(...)` (season filter, greenhouse bypass, supply tag, dedup, sort) + `SortFertilizers(...)`.

**Mod (`Dayswork/`)**
- `UI/CropPlanDraft.cs` — `CropPlanDraft` + `SeasonSlotDraft` + `SeasonLockState`; per-season authoring, multi-season locking via `SeasonAssignmentResolver` with conflict rejection (R-08), projection (`BuildAssignmentChoices`/`MaterializeForZones`/`BuildCropPlan`), edit-flow `HydrateFrom`, `EnrichDisplayNames`.
- `Integration/CropCatalogProvider.cs` — live 1.6 adapter (M-25): reads `Data/Crops`, object data (fertilizers), and `Data/Shops` (Pierre/Joja stock for auto-buyable tagging); maps to `CropCatalogSource`/`FertilizerOption`; per-session per-season memoization; skip-unmappable + trace logging via `IMonitor`.
- `UI/CropListPickerMenu.cs` — reusable scrollable single-select picker (label + tag) for crop, fertilizer, and chest selection (Q2=B); `MenuScrollBar`, gamepad/KBM, B-to-back.
- `UI/ManageCropsMenu.cs` — the authoring page (M-24, Q1=A): four season rows (crop/fertilizer/replant; locked styling + reason), output-chest row, draw-gating, clear-zones, Back.

**Tests (`Dayswork.Tests/`)**
- `ManageCrops/CropCatalogTests.cs` (7), `ManageCrops/CropDescriptorWithFertilizerTests.cs` (2), `UI/CropPlanDraftTests.cs` (10).
- 3 FsCheck properties added to `ManageCrops/ManageCropsPropertyTests.cs` (season-filter membership, deterministic ordering, `WithFertilizer` growth-field preservation).

## Modified Application Files

- `Dayswork.Core/Crops/CropDescriptor.cs` — added pure `WithFertilizer(string?)`.
- `Dayswork.Core/Crops/SeasonCropChoice.cs` — added additive `AutoReplant` (default false).
- `Dayswork.Core/Crops/SeasonAssignmentResolver.cs` — propagate `AutoReplant` through expansion / season-agnostic.
- `Dayswork.Core/Persistence/Dto/SeasonCropChoiceDtoV1.cs` + `Dayswork.Core/Crops/CropPlanSerialization.cs` — persist `AutoReplant` (additive, backward-compatible; missing → false).
- `Dayswork/UI/ContractDraft.cs` — added transient `CropPlan` (`CropPlanDraft`).
- `Dayswork/UI/HubMenu.cs` — Manage Crops nav row + status chip (Done at ≥1 assignment, else Optional; Q8=A).
- `Dayswork/UI/HiringFlowCoordinator.cs` — Manage Crops page/picker/draw wiring, catalog provider lifecycle, `BuildContract` attaches `CropPlan`, `CreateEditDraft` hydrates the draft.
- `Dayswork/i18n/default.json` — new `ui.hub.manage_crops` + `ui.manage_crops.*` keys.
- `Dayswork.Tests/Generators/ManageCropsGen.cs` — randomize `AutoReplant` so the round-trip property exercises it.

## Review Fix - 2026-06-06 Contract Menu Overflow and Scroll Containment

Playtest feedback found that the Configure Contract hub could overlap the lower nav rows with the
`Cancel` / `Hire` footer buttons in small windowed mode, allowing clicks on the footer to trigger
the overlapped row instead. The review fix keeps contract-flow footers pinned and moves long body
content into bounded scroll regions.

### Modified Files

- `Dayswork/UI/ContractMenuViewport.cs` — small internal helper for fixed-row and variable-row viewport math used by the overflow fix and covered by focused tests.
- `Dayswork/UI/Layout/PageShell.cs` — additive support for a custom leading footer label/sound/width so the hub can use a cancel-style footer button while keeping the shared shell layout.
- `Dayswork/UI/HubMenu.cs` — refactored from manual `IClickableMenu` geometry to `LayoutMenu` + `PageShell` + `ScrollPanel`; hub rows now scroll inside a bounded body while `Cancel` and `Hire` stay pinned in the footer.
- `Dayswork/UI/ZoneAndChestMenu.cs` — replaced free-flowing scope summary drawing with a bounded text viewport using `MenuScrollBar`, preventing long animal/greenhouse summaries from colliding with the footer.
- `Dayswork/UI/ContractListMenu.cs` — replaced content-sized menu growth with a fixed-height scrolling viewport that windows visible contracts and keeps action buttons inside the viewport.
- `Dayswork.Tests/UI/ContractMenuViewportTests.cs` — focused example tests for compressed fixed-row and variable-row viewport behavior.

### Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` — **0 warnings / 0 errors**.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` — **458 passed / 1 expected skip / 0 failed** (+4 new).

### Notes

- The safe-screen audit left `TaskSelectionMenu`, `OutputDestinationsMenu`, `CropListPickerMenu`,
  `SummaryMenu`, `ManageCropsMenu`, `TaskPriorityMenu`, `EnergyMenu`, and `ScheduleMenu`
  unchanged: inspection found their existing bounded layouts sufficient for this fix scope.
- Manual in-game verification is still recommended for the original small-window click path because
  the automated suite only covers the extracted viewport math, not rendered SMAPI menu interaction.

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` — **0 warnings / 0 errors**.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` — **401 passed / 1 expected skip / 0 failed** (+22 new).
- `dotnet build Dayswork.sln` — deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.
- Hardcoded-string lint gate green (new player-facing strings are i18n-backed; adapter trace logs routed through `IMonitor.Log`).

## Deviations / Notes

- **DEV-MC-03-01**: Added per-season `AutoReplant` to the existing `SeasonCropChoice` + `SeasonCropChoiceDtoV1` (additive, no schema version bump). Required so the authored replant intent (in this unit's DoD) persists for the runtime consumer (U-MC-05). Backward-compatible: older saves deserialize replant as false.
- **Auto-buyable tagging is best-effort and display-only** this unit: the adapter checks Pierre (`SeedShop`) / Joja shop item ids. Exact stock/purchasability is U-MC-06's headless transaction. To be confirmed at playtest.
- **Boundaries held**: no greenhouse/shed season-agnostic authoring (U-MC-07, Q5=A); no plan-level debris/dead-plant toggles or store-preference UI (U-MC-05/06, Q6=A); begin-draw reuses the existing overlay as-is, with red/green coloring + overlap prevention deferred to U-MC-04 (Q4=A).

## Extension Compliance

- **Security Baseline**: N/A (disabled for Manage Crops; UI-only, no network/auth/PII/filesystem).
- **Property-Based Testing (full mode)**: Compliant. Pure catalog/resolver/projection logic covered by FsCheck properties; live adapter + menu wiring example-tested.

## Playtest Checklist (in-game)

- Manage Crops row appears on the hub; chip shows Optional, then Ready after a draw.
- Crop picker lists vanilla + modded crops filtered to the chosen season with buyable/chest-only tags.
- Multi-season crop (corn) locks its linked season with a clear reason; conflicts are rejected.
- Fertilizer + auto-replant selectable per configured season; gamepad + mouse/keyboard both work.
- Output chest picker lists selectable chests (office chests excluded) and an Automatic default.
- Draw applies the plan to each drawn zone; confirming the contract persists the crop plan; edit flow reloads it.
