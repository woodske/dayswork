# U-MC-03 Logical Components

**Unit**: U-MC-03 - Manage Crops Authoring UI
**Stage**: CONSTRUCTION - NFR Design
**Status**: Complete

## Component Map

| Component | Layer | NFR responsibility |
|---|---|---|
| `HubMenu` (extended) | Mod UI | Adds the Manage Crops NavItem + status delegate; reads precomputed draft state only. |
| `ManageCropsMenu` (M-24, new) | Mod UI | Single scrolling authoring page; orchestrates season rows, output-chest control, draw button; gamepad/KBM parity; `draw()` reads precomputed state. |
| `CropPickerMenu` / `FertilizerPickerMenu` (new) | Mod UI | Scrollable pickers over the session-cached, season-filtered catalog (virtualized via `MenuScrollBar`). |
| `CropCatalogProvider` (M-25, new) | Mod adapter | Lifecycle-scoped live crop/shop read; maps to pure descriptors/entries; per-session per-season memoization. |
| `PureCropCatalog` (Core, new) | Core (pure) | Deterministic season-filter, supply-tag, sort. PBT-covered. |
| `SeasonAssignmentResolver` (C-27, wired) | Core (pure) | Multi-season auto-population/locking; conflict-safe, idempotent. PBT-covered. |
| `CropPlanDraft` / `SeasonSlotDraft` (new) | Mod state | Single source of truth for in-progress authoring; transient (not serialized). |
| `HiringFlowCoordinator` (extended) | Mod | Wires page-open, picker opens, output-chest pick, begin-draw handoff, and assignment materialization; extends `BuildContract`/`CreateEditDraft`. |
| `ChestResolver` (reused) | Mod discovery | Supplies the selectable chest list (office chests already excluded, U-MC-02). |
| `ZoneDrawMenu` (reused) | Mod UI | Existing draw machinery for the begin-draw handoff (Q4=A). |
| `i18n/default.json` | Localization | New keys: nav label, season names usage, picker chrome, supply tags, lock reason, chest label, status chip. |
| Tests | Test project | FsCheck properties for pure catalog/resolver logic; example tests for the adapter + menu wiring. |

## Logical Flow

1. Player opens the contract flow → `HiringFlowCoordinator.ShowHub` renders `HubMenu` with the
   new Manage Crops row (status from `CropPlanDraft.HasAnyAssignment`).
2. Player opens Manage Crops → coordinator lazily creates/hydrates `CropPlanDraft` and shows
   `ManageCropsMenu`.
3. Opening a season's crop picker triggers `CropCatalogProvider.GetCatalog(season, false)` →
   `PureCropCatalog` filters/tags/sorts; result cached for the session.
4. Selecting a crop writes `SeasonSlotDraft.Crop`; multi-season crops invoke
   `SeasonAssignmentResolver` to lock linked seasons (conflict-safe).
5. Fertilizer picker and auto-replant toggle update the slot; output-chest control writes
   `CropPlanDraft.OutputChest` via the reused `ChestResolver` picker.
6. "Draw zone(s)" (enabled once ≥1 season configured) → reused `ZoneDrawMenu`; on complete, each
   drawn `Zone` is projected into a `CropZoneAssignment` appended to
   `CropPlanDraft.MaterializedAssignments`; `RefreshPreview` updates the hub chip.
7. Confirm → extended `BuildContract` attaches `CropPlan(MaterializedAssignments)` (or
   `CropPlan.Empty`) to the `Contract`, round-tripping the existing U-MC-01 DTO.

## Implementation Constraints

| Constraint | Design implication |
|---|---|
| Application code stays outside `aidlc-docs/` | Production code under `Dayswork/` and `Dayswork.Core/`; tests under `Dayswork.Tests/`. |
| Brownfield files modified in place | Extend `HubMenu`, `ContractDraft`, `HiringFlowCoordinator` in place; no `*_new.cs`/`*_modified.cs` duplicates. |
| No new dependencies | Reuse SMAPI/StardewValley, `MenuScrollBar`, `ChestResolver`, `ZoneDrawMenu`, FsCheck.Xunit (NFR-MC-09). |
| No per-frame data reads | Catalog read on menu-open/season-change only; `draw()` uses precomputed state. |
| No schema change | Authored `CropPlan` rides the existing `ContractDtoV2.CropPlan`; drafts/catalog are transient. |
| Determinism in Core | Filter/tag/sort + multi-season resolution stay pure-Core for PBT (Q3=A). |
| Draft isolation | Edit hydration must not mutate the stored contract before confirm. |
| Scope boundaries | No greenhouse/shed authoring (U-MC-07), no plan-level toggles/store UI (U-MC-05/06), no overlay coloring (U-MC-04). |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Pure components preserve identified properties; live adapter + UI example-tested. |
