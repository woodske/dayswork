# U-22 — Scope-Driven Runtime Alignment: NFR Design Plan

**Unit**: U-22 — Scope-Driven Runtime Alignment  
**Phase**: CONSTRUCTION — NFR Design  
**Builds on**: approved NFR Requirements for `U-22`. See [nfr-requirements/](../u-22-scope-driven-runtime-alignment/nfr-requirements/).

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

No additional user questions are needed for U-22 NFR Design. The approved NFR requirements and functional design already determine the pattern set cleanly:

- **Resilience patterns** — Applicable and already determined by the approved fail-fast / no-item-loss bar:
  - explicit fail-fast rejection for unsupported contracts without `ScopeSelection`
  - reuse of the existing output/deposit and mail safety path
  - richer scope-aware overflow categorization without introducing new billing or item-loss semantics
- **Scalability patterns** — N/A. This is a local single-worker in-process runtime seam with no distributed load, replica, queue, or scale-out mechanism.
- **Performance patterns** — Applicable and already determined:
  - lightweight synchronous scope classification at shift start
  - deterministic batch-family shaping without per-tick whole-farm replanning
  - bounded scope-aware mail categorization on already-buffered output/provenance data
  - no async planning worker, background categorization pipeline, or speculative caching subsystem
- **Security patterns** — N/A. Security Baseline is disabled project-wide and the unit has no network/auth/PII surface.
- **Logical components** — Applicable and already determined:
  - `WorkScopeClassifier` as the authoritative normalization seam
  - a narrow runtime support guard for no-scope contracts
  - task-owned `DepositPlanner` preserved as the routing authority
  - a provenance-aware overflow categorization seam feeding the existing `MailDispatcher`
  - dedicated U-22 test-side helpers for mixed-scope invariants and categorization determinism

The approved NFR requirements are all recommended-path decisions (`NFR-Q1=A` through `NFR-Q5=A`), so no clarification round is needed to resolve tradeoffs before producing the NFR design artifacts.

---

## Artifact Output

- `aidlc-docs/construction/u-22-scope-driven-runtime-alignment/nfr-design/nfr-design-patterns.md`
- `aidlc-docs/construction/u-22-scope-driven-runtime-alignment/nfr-design/logical-components.md`
