# U-MC-03 NFR Design Patterns

**Unit**: U-MC-03 - Manage Crops Authoring UI
**Stage**: CONSTRUCTION - NFR Design
**Status**: Complete

## Pattern Summary

U-MC-03 uses small, local UI/domain patterns — not infrastructure patterns. The goals: keep
the live crop/shop catalog read off the game-loop hot path, keep all decision logic
deterministic and pure-Core, protect the in-progress draft and existing contract data, and
reuse the established menu/seam idioms. No queues, caches-as-services, or circuit breakers are
introduced.

## Resilience Patterns

| Pattern | Application |
|---|---|
| Skip-and-continue mapping | `CropCatalogProvider` skips crop/fertilizer records it cannot map from live data instead of throwing (NFR-MC3-REL-01). |
| Empty-state tolerance | A season with no growable crops (e.g. vanilla winter) renders a clear empty list, not an error (NFR-MC3-REL-02). |
| Conflict-safe transition | Multi-season selection that would collide with an existing/locked slot is rejected, preserving prior configuration (NFR-MC3-REL-03 / R-08). |
| All-or-nothing materialization | A cancelled/zero-zone draw produces no `CropZoneAssignment`; the draft is unchanged (NFR-MC3-REL-04 / R-20). |
| Opt-in safe default | Empty/absent plan persists as `CropPlan.Empty` and never blocks confirm (NFR-MC3-REL-05 / R-22/R-23). |
| Draft isolation | Edit-flow hydration copies an existing contract's `CropPlan` into the draft; the saved contract is untouched until confirm (NFR-MC3-REL-06). |

## Scalability Patterns

| Pattern | Application |
|---|---|
| Scrollable/virtualized list | The crop/fertilizer pickers (Q2=B) page the full vanilla+modded catalog through `MenuScrollBar`, rendering only visible rows (NFR-MC3-SCA-01). |
| Bounded authoring model | Exactly four season slots; assignment count grows only by explicit draws (NFR-MC3-SCA-02). |
| Data-proportional build | Catalog construction is O(catalog size), independent of frame count (NFR-MC3-SCA-03). |

## Performance Patterns

| Pattern | Application |
|---|---|
| Lifecycle-scoped data read | Live crop/shop data is read at menu-open / season-selection, never per frame or per draw (NFR-MC3-PERF-01). |
| In-session memoization | Per-season catalog results are cached for the authoring session so repeated picker opens reuse them (NFR-MC3-PERF-02). |
| Pure O(n) decision logic | Season-filter, supply-tag, sort, and multi-season resolution are linear over the catalog, no world/tile/graph work (NFR-MC3-PERF-03). |
| Precomputed render | `draw()` reads only precomputed view state — no allocations or data reads in the render loop (NFR-MC3-PERF-04). |

## Maintainability Patterns

| Pattern | Application |
|---|---|
| Pure-core / thin-adapter split | Deterministic catalog filter/tag/sort + multi-season resolution live in `Dayswork.Core`; the live data read is an isolated mod adapter (Q3=A / NFR-MC3-MAINT-01). |
| Single source of truth | All authoring state lives in `CropPlanDraft`/`SeasonSlotDraft` on `ContractDraft` (NFR-MC3-MAINT-02). |
| Seam reuse | Reuse `MenuScrollBar`, `ChestResolver` picker, `ZoneDrawMenu` handoff, `HubMenu` NavItem, `SeasonAssignmentResolver` (NFR-MC3-MAINT-03). |
| Test-vehicle split | PBT for pure logic; example tests for live adapter + `IClickableMenu` wiring (NFR-MC3-MAINT-04). |
| Centralized i18n | New player-facing strings are i18n keys, lint-gated (NFR-MC3-MAINT-05 / R-24). |

## Usability Patterns

| Pattern | Application |
|---|---|
| Gamepad+KBM parity | Page and both pickers are snap-navigable with B-to-back (NFR-MC3-UX-01 / R-25). |
| Affordance gating | "Draw zone(s)" disabled until ≥1 season configured (NFR-MC3-UX-05 / R-17). |
| Explanatory locking | Locked multi-season slots are distinctly styled with a reason (NFR-MC3-UX-02 / R-07). |
| Expectation-setting tags | Auto-buyable vs chest-supply-only chips on catalog rows (NFR-MC3-UX-03 / R-05). |
| Status reflection | Hub chip = "Done" at ≥1 materialized assignment, else "Optional" (NFR-MC3-UX-04 / R-02). |

## Security Patterns

| Pattern | Application |
|---|---|
| N/A | No network/auth/PII/filesystem boundary; Security Baseline disabled for Manage Crops. |

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-01 | Satisfied by Functional Design | Properties identified for catalog filter/tag/sort and multi-season resolution. |
| PBT-02..PBT-08 | Carried to code generation | Pure-Core seams (Q3=A) will carry generators + properties (filter correctness, tag determinism, stable sort, lock idempotence/conflict-safety). |
| PBT-09 | Compliant | FsCheck.Xunit remains available. |
| PBT-10 | Compliant for design | Example-test strategy for live adapter + menu wiring documented. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Patterns preserve identified properties; pure logic kept PBT-able, live/UI example-tested. |
