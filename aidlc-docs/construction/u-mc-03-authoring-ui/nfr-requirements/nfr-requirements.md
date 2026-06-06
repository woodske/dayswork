# U-MC-03 NFR Requirements

**Unit**: U-MC-03 - Manage Crops Authoring UI
**Stage**: CONSTRUCTION - NFR Requirements
**Status**: Complete

## Summary

U-MC-03 is a **client-side authoring UI** unit (a new `HubMenu` row + `ManageCropsMenu`,
scrollable crop/fertilizer pickers, a live crop catalog seam, and draft→`CropPlan`
materialization). It adds **no new infrastructure, no network/auth/PII surface, and no new
runtime dependency**. The NFR posture: keep the live crop/shop catalog read off the per-frame
hot path, keep all decision logic deterministic and pure-Core, protect the player's in-progress
draft and existing contract data, preserve existing menu behavior, and meet i18n + gamepad
parity. This refines the feature-level NFR-MC-01..09 for the authoring slice.

## Scalability

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC3-SCA-01 | Support the full vanilla + modded crop catalog (potentially hundreds of entries) in the picker without UI degradation. | Modded crop packs can be large; the scrollable list (Q2=B) must stay responsive. |
| NFR-MC3-SCA-02 | Support up to four configured season slots and an arbitrary number of materialized `CropZoneAssignment`s per draft. | Crop-first authoring allows a full year-round rotation applied to many drawn zones (FR-MC-08). |
| NFR-MC3-SCA-03 | Catalog construction work scales with the crop/shop data set, not with frame count. | Building the list is a menu-open/season-change event, not per-tick. |

## Performance

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC3-PERF-01 | Build the crop catalog at menu-open / season-selection time, not per frame or per draw. | Live `Data/Crops` / shop-stock reads must not run on the game loop hot path (NFR-MC-02). |
| NFR-MC3-PERF-02 | Cache the per-season catalog within an authoring session so repeated picker opens do not re-read live data each time. | Keeps picker opens snappy; data is stable within a session. |
| NFR-MC3-PERF-03 | Pure season-filter, supply-tag, sort, and multi-season resolution are O(n) over the catalog with no per-tile or graph work. | Authoring has no world-scan cost; keeps logic cheap and deterministic. |
| NFR-MC3-PERF-04 | `draw()` reads only pre-computed view state (no live data reads or allocations in the render loop). | Matches the existing menu convention (`HubMenu.draw` reads precomputed state). |

## Availability and Reliability

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC3-REL-01 | Crop/fertilizer items that cannot be mapped from live data are skipped, never crash the picker. | Modded/edge crop data must degrade gracefully (NFR-MC-05). |
| NFR-MC3-REL-02 | An empty season catalog (e.g. vanilla winter) yields a clear empty state, not an error. | Season filtering legitimately produces empty lists. |
| NFR-MC3-REL-03 | Multi-season conflicts are rejected without corrupting existing slot configuration. | Locking/conflict handling must preserve prior choices (R-08). |
| NFR-MC3-REL-04 | A cancelled or zero-zone draw leaves the draft unchanged; no partial assignment is produced. | Materialization is all-or-nothing per draw (R-20). |
| NFR-MC3-REL-05 | An empty/absent crop plan persists as `CropPlan.Empty` and never blocks contract confirm. | Crop management is opt-in; must be non-regressive (NFR-MC-06, R-22/R-23). |
| NFR-MC3-REL-06 | Edit-flow hydration of an existing contract's `CropPlan` must not mutate the saved contract until confirm. | Draft isolation protects existing contract data. |

## Security and Compliance

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC3-SEC-01 | No additional security controls required. | UI-only; no network, auth, PII, or filesystem boundary. Security Baseline disabled for Manage Crops. |

## Maintainability

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC3-MAINT-01 | Keep catalog season-filter/supply-tag/sort and multi-season resolution in pure `Dayswork.Core`; keep live data reads in a thin mod adapter. | Q3=A: determinism + testability in Core, live API isolated. |
| NFR-MC3-MAINT-02 | Confine new authoring state to `CropPlanDraft`/`SeasonSlotDraft` hanging off `ContractDraft`; do not spread crop state across unrelated menus. | Keeps the authoring model cohesive and the draft the single source of truth. |
| NFR-MC3-MAINT-03 | Reuse existing UI seams (`MenuScrollBar`, `ChestResolver` chest picker, `ZoneDrawMenu` handoff, `HubMenu` NavItem pattern) rather than new bespoke machinery. | Q1/Q2/Q4/Q7: lower surface area, consistent behavior. |
| NFR-MC3-MAINT-04 | Use PBT for the pure catalog/resolver logic and example tests for the live adapter and menu wiring. | Q3=A; mirrors U-MC-02 example emphasis for live APIs. |
| NFR-MC3-MAINT-05 | Centralize new i18n keys and avoid hardcoded player-facing strings. | R-24 / NFR-MC-07; passes the hardcoded-string lint gate. |

## Usability

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC3-UX-01 | Full mouse/keyboard **and** gamepad navigation across the page and both pickers (snap order, B-to-back). | NFR-UX-01 / R-25; consistent with existing menus. |
| NFR-MC3-UX-02 | Locked (multi-season) seasons are visually distinct and show **why** they are blocked. | FR-MC-04 / R-07; avoids player confusion. |
| NFR-MC3-UX-03 | Crops are tagged auto-buyable vs chest-supply-only in the picker. | FR-MC-03 / R-05; sets purchasing expectations. |
| NFR-MC3-UX-04 | The hub status chip reflects configured state ("Done" at ≥1 materialized assignment, else "Optional"). | FR-MC-01 / R-02 / Q8=A. |
| NFR-MC3-UX-05 | The "Draw zone(s)" affordance is disabled until at least one season is configured. | R-17; prevents drawing an empty plan. |

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-09 | Compliant | FsCheck.Xunit remains the selected, present framework; no new framework decision. |
| PBT-01 / pure-logic properties | Carried to design/codegen | Q3=A keeps catalog filter/tag/sort and multi-season resolution pure-Core; properties (filter correctness, tag determinism, stable sort, lock idempotence/conflict-safety) identified for codegen. |
| Live-adapter / menu wiring | Example-tested | Live crop/shop reads and `IClickableMenu` behavior covered by example tests (Q3=A). |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Framework valid; pure-logic properties carried; live/UI behavior example-tested. |
