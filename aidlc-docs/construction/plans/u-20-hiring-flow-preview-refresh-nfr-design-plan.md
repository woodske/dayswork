# U-20 — Hiring Flow Preview Refresh: NFR Design Plan

**Unit**: U-20 — Hiring Flow Preview Refresh  
**Phase**: CONSTRUCTION — NFR Design  
**Builds on**: approved NFR Requirements for `U-20`. See [nfr-requirements/](../u-20-hiring-flow-preview-refresh/nfr-requirements/).

---

## Plan Checklist

- [x] Analyze NFR requirements artifacts
- [x] Create this NFR design plan
- [x] Evaluate all NFR design question categories and determine whether clarification is needed
- [x] Generate `nfr-design-patterns.md`
- [x] Generate `logical-components.md`
- [x] Present completion message and await approval

---

## Pattern Determination

No additional user questions are needed for U-20 NFR Design. The approved NFR requirements and functional design already determine the design patterns cleanly:

- **Resilience patterns** — Applicable and already determined by the approved invalid-preview and legacy-hydration bar:
  - explicit invalid-preview recovery path
  - Screen 4 as the sole confirmation gate
  - best-effort legacy scope bootstrap with honest degradation
  - no silent whole-farm fallback or silent scope invention
- **Scalability patterns** — N/A. This is a local in-process hire/edit flow with tiny draft data; no queue, sharding, replica, or load-distribution patterns apply.
- **Performance patterns** — Applicable and already determined:
  - synchronous inline preview recomputation
  - narrower non-pricing refresh paths for schedule and destination edits
  - no caching, debounce, async preview pipeline, or memoization subsystem
- **Security patterns** — N/A. Security Baseline is disabled project-wide and the unit has no network/auth/PII surface.
- **Logical components** — Applicable and already determined:
  - `HiringFlowCoordinator` owns preview recomputation boundaries and canonical view-model shaping
  - menus remain presentation-thin and consume coordinator-provided state
  - legacy scope bootstrap stays a narrow compatibility helper
  - dedicated U-20 test-side helpers carry the stronger determinism/orchestration property bar

The approved NFR requirements are all recommended-path decisions (`NFR-Q1=A` through `NFR-Q5=A`), so no clarification round is needed to resolve tradeoffs before producing the NFR design artifacts.

---

## Artifact Output

- `aidlc-docs/construction/u-20-hiring-flow-preview-refresh/nfr-design/nfr-design-patterns.md`
- `aidlc-docs/construction/u-20-hiring-flow-preview-refresh/nfr-design/logical-components.md`
