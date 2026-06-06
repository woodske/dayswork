# U-MC-04 — Zone Draw Overlay Extension — Functional Design Plan

**Unit**: U-MC-04 — Zone Draw Overlay Extension
**Stage**: CONSTRUCTION — Functional Design
**Stories**: S-28 (Draw crop zones around existing assignments)
**Requirements**: FR-MC-06 (red/unselectable existing, green active, overlap prevention), FR-MC-07 (delete-and-redraw only), FR-MC-08 (non-contiguous same-plan zones)
**Decision of record**: DEV-MC-01 (user override 2026-06-04) — existing/assigned zones render a single **red**; the active draw session renders **green**.

## Context note (important)

The full Definition of Done for U-MC-04 was delivered **early**, inside the U-MC-03
code-generation **review fix** (crop groups). At that point the authoring flow gained
multiple crop groups, each with its own zones, which forced the overlay to distinguish
"this group's active draw" from "other groups' existing assignments" — exactly the
DEV-MC-01 red/green + overlap-prevention behavior this unit owns. As a result, this
Functional Design **documents the as-built design** and maps it to S-28 / FR-MC-06/07/08
rather than proposing new behavior.

## Plan steps

- [x] Analyze unit definition (`unit-of-work.md` U-MC-04) and story map (S-28).
- [x] Confirm the requirement/decision set: FR-MC-06, FR-MC-07, FR-MC-08, DEV-MC-01.
- [x] Inspect the as-built overlay seam: `IZoneDrawSource`, `ZoneDrawOverlay`, `ZoneDrawMenu`, `ZoneOverlapPolicy`, `CropPlanDraft.ProtectedZones`, `HiringFlowCoordinator.BeginCropZoneDraw`.
- [x] Confirm existing test coverage for the pure logic (`ZoneOverlapPolicyTests`, `CropPlanDraftTests`).
- [x] Generate functional-design artifacts (business-logic-model, business-rules, domain-entities, frontend-components).
- [x] Identify any residual gap to close in Code Generation (dedicated U-MC-04 example coverage tying the unit to its DoD).

## Questions for the user

None. DEV-MC-01 fully fixes the coloring and selectability behavior, and the implementation
already exists and is deployed. No ambiguity remains; no clarification round required.

## Extension compliance

- **Security Baseline**: disabled for Manage Crops → N/A.
- **Property-Based Testing (full mode)**: the unit's pure logic (`ZoneOverlapPolicy`, draft
  protected-zone projection) is already FsCheck-/example-covered from U-MC-01/03; this unit
  adds focused example coverage. No new property-applicable algorithm is introduced.
