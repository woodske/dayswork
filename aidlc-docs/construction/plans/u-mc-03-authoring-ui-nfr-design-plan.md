# NFR Design Plan - U-MC-03 Manage Crops Authoring UI

**Unit**: U-MC-03 - Manage Crops Authoring UI
**Stage**: CONSTRUCTION - NFR Design
**Status**: Complete (no question round needed)

## Plan Checklist

- [x] Load NFR Design rule details.
- [x] Read U-MC-03 NFR Requirements (nfr-requirements.md, tech-stack-decisions.md).
- [x] Evaluate all mandatory categories: Resilience, Scalability, Performance, Security, Logical Components.
- [x] Determine whether a question round is needed.
- [x] Generate nfr-design-patterns.md.
- [x] Generate logical-components.md.
- [x] Present NFR Design completion gate.

## Category Evaluation

| Category | Applicability | Resolution |
|---|---|---|
| Resilience | Applicable (graceful degradation) | Skip-unmappable, empty-state, conflict-safe locking, all-or-nothing draw, opt-in empty plan, draft isolation. Fixed by NFR-MC3-REL-01..06; no new infra. |
| Scalability | Applicable (catalog size) | Virtualized/scrollable picker over the full catalog; session-cached per-season lists; bounded four-slot model. Fixed by NFR-MC3-SCA-01..03. |
| Performance | Applicable (hot-path avoidance) | Catalog built on menu-open/season-change, cached per session; pure O(n) logic; `draw()` reads precomputed state. Fixed by NFR-MC3-PERF-01..04. |
| Security | Not applicable | UI-only; no network/auth/PII/filesystem. Security Baseline disabled. |
| Logical Components | Applicable | New `ManageCropsMenu`, `CropPickerMenu`, `FertilizerPickerMenu`, `CropCatalogProvider` (mod adapter), pure Core catalog/resolver seam, `CropPlanDraft` state, coordinator wiring; reuse existing seams. |

## Question-Round Decision

**No question round needed.** Every mandatory category is resolved by the approved NFR
Requirements (NFR-MC3-*) and the Functional Design decisions (Q1–Q8). No infrastructure
components (queues/caches-as-services/circuit breakers) are introduced — the only "cache" is an
in-session per-season catalog memo. This mirrors U-MC-01/U-MC-02 NFR Design.

## Extension Notes

- **Security Baseline:** disabled → N/A.
- **Property-Based Testing (full mode):** PBT-09 satisfied; pure catalog/resolver seams kept
  property-testable (Q3=A); live adapter + menu wiring example-tested.
