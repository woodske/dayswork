# Code Summary — U-MC-04 Zone Draw Overlay Extension

**Unit**: U-MC-04 — Zone Draw Overlay Extension
**Stage**: CONSTRUCTION — Code Generation
**Status**: Complete; review required (in-game playtest)

## Summary

U-MC-04 extends the existing on-farm zone-draw session into a distinct **managed-crop layer**:
other crop groups' zones render **red** and are unselectable, the active group's draw renders
**green**, overlap is prevented, and editing is delete-and-redraw only (DEV-MC-01; FR-MC-06/07/08;
S-28).

**This behavior was delivered early, inside the U-MC-03 code-generation review fix** (the crop-
groups change), because supporting multiple crop groups required exactly this active-vs-other
draw distinction. This unit therefore consisted of: documenting the as-built functional design,
confirming the seam satisfies the DoD, and closing the residual dedicated-test gap. No new
runtime code was required.

## As-built implementation (delivered in U-MC-03 fix)

- `Dayswork/UI/IZoneDrawSource.cs` — `ZoneFillColor` (active) + `ProtectedZoneFillColor` contract.
- `Dayswork/UI/ZoneDrawOverlay.cs` — green active-zone fill, red protected-zone fill, light-green drag preview; O(zone count) per frame (NFR-PERF-03).
- `Dayswork/UI/ZoneDrawMenu.cs` — protected-overlap guard (reject + HUD error `ui.manage_crops.zone_overlap_protected`), delete-and-redraw toggle for own zones.
- `Dayswork/UI/ZoneOverlapPolicy.cs` — pure edge-inclusive rectangle overlap predicate (`OverlapsAny` / `ZonesOverlap`).
- `Dayswork/UI/CropPlanDraft.cs` — `ProtectedZones(activeGroupId)` (all other groups' zones) + `SetGroupZones`.
- `Dayswork/UI/HiringFlowCoordinator.cs` — `BeginCropZoneDraw` configures the crop layer (`allowBuildingSelection:false`, `overlapTogglesSelection:true`, protected zones, `Color.LimeGreen * 0.5f`), commits zones on Done, applies the seasonal plan to each drawn zone.

## Changed in this unit

**Tests (`Dayswork.Tests/`)**
- `UI/ZoneOverlapPolicyTests.cs` — +2 dedicated U-MC-04 examples:
  - `OverlapsAny_ReturnsFalseWhenNoProtectedZonesExist` (first/only group draws freely).
  - `OverlapsAny_ReturnsTrueWhenIntersectingAnyOfSeveralProtectedZones` (multi-group "any" rejection).

**Docs**
- `aidlc-docs/construction/u-mc-04-zone-draw-overlay/functional-design/` (domain-entities, business-logic-model, business-rules, frontend-components).
- `aidlc-docs/construction/plans/u-mc-04-zone-draw-overlay-functional-design-plan.md` and `-code-generation-plan.md`.

## Existing coverage relied upon

- `UI/ZoneOverlapPolicyTests.cs` — overlap true/false, shared-edge.
- `UI/CropPlanDraftTests.cs` — `ProtectedZones_ExcludesActiveGroup`, group projection/hydration.

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` — 0 warnings / 0 errors.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` — 435 passed / 1 expected skip / 0 failed (+2).
- `dotnet build Dayswork.sln` — deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.

## Stages skipped (with rationale)

- **NFR Requirements / NFR Design** — skipped: no new tech, dependency, or quality concern; reuses the already-designed/already-NFR'd overlay seam.
- **Infrastructure Design** — skipped: SMAPI mod, no cloud/IaC.

## Extension compliance

- **Security Baseline**: N/A (disabled for Manage Crops; UI rendering only).
- **Property-Based Testing (full mode)**: compliant — pure overlap/protection logic example-/property-covered; no new property-applicable algorithm.

## Playtest checklist (in-game)

- Author ≥2 crop groups; draw zones for group 1, then edit group 2 → group 1's zones appear **red** and cannot be drawn over.
- Active group's drawn zones + in-progress drag appear **green**.
- Dragging over a red (other-group) zone is rejected with a HUD message; nothing is added.
- Dragging over your own green zone removes it (delete-and-redraw); a fresh area adds a zone.
- Confirming the contract persists each group's zones with its plan; edit flow reloads them.
