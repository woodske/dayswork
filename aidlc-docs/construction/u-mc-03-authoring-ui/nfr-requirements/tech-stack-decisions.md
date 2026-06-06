# U-MC-03 Tech Stack Decisions

**Unit**: U-MC-03 - Manage Crops Authoring UI
**Stage**: CONSTRUCTION - NFR Requirements
**Status**: Complete

## Decision Summary

U-MC-03 reuses the **existing C#/.NET 6 + SMAPI + Stardew Valley** stack and the established
mod UI / Core seams. **No new runtime dependency, package, framework, or infrastructure is
introduced** (NFR-MC-09).

## Decisions

| Area | Decision | Rationale |
|---|---|---|
| UI framework | Stardew `IClickableMenu` (existing). New `ManageCropsMenu`, `CropPickerMenu`, `FertilizerPickerMenu` follow the existing menu idiom; extend `HubMenu`. | Consistency with all prior UI units; gamepad/mouse parity already solved in this idiom. |
| List/scroll | Reuse `MenuScrollBar` for the crop/fertilizer pickers (Q2=B). | Existing, tested scroll control; avoids bespoke scrolling. |
| Chest selection | Reuse the existing `ChestResolver` selectable-chest picker idiom (Q7=A). | Office chests already excluded (U-MC-02); no new chest UI needed. |
| Zone draw | Reuse the existing `ZoneDrawMenu` / overlay machinery for the begin-draw handoff (Q4=A). | U-MC-04 hardens visuals later; this unit needs no new draw tech. |
| Crop catalog read | Thin mod adapter reading live 1.6 crop data (`Data/Crops`, seed→crop links, seasons/regrow) and shop stock for auto-buyable tagging; maps to pure `CropDescriptor`/`CropCatalogEntry` (Q3=A). | Live-data authority with deterministic Core mapping; respects modded crops. |
| Pure logic location | `Dayswork.Core` for season-filter, supply-tagging, sort, and multi-season resolution (`SeasonAssignmentResolver`, C-27). | Determinism + PBT (NFR-MC-01/08). |
| Authoring state | New `CropPlanDraft`/`SeasonSlotDraft` on `ContractDraft` (transient, not serialized). | Single source of truth; no schema change. |
| Persistence | None new. Authored `CropPlan` rides the existing U-MC-01 `ContractDtoV2.CropPlan` (written only when non-empty). | Backward-compatible; opt-in (NFR-MC-06). |
| i18n | Existing `I18nHelper` + i18n JSON; new keys for labels/tags/lock-reason/chest/status. | NFR-MC-07; hardcoded-string lint gate. |
| Testing | xUnit example tests (adapter + menu wiring) + FsCheck properties (pure catalog/resolver logic). | Q3=A; existing test stack (FsCheck.Xunit, PBT-09). |

## Rejected / Deferred Alternatives

- **New picker/scroll UI library** — rejected; reuse `MenuScrollBar` (NFR-MC-09).
- **Persisting the catalog or authoring drafts** — rejected; catalog is rebuilt per session,
  drafts are transient.
- **Authoring greenhouse/shed season-agnostic mode now** — deferred to U-MC-07 (Q5=A).
- **Plan-level toggles / store-preference UI now** — deferred to U-MC-05 / U-MC-06 (Q6=A).

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops; no security-relevant tech choice. |
| Property-Based Testing | Compliant | No new framework; FsCheck.Xunit retained; pure-logic seams kept PBT-able (Q3=A). |
