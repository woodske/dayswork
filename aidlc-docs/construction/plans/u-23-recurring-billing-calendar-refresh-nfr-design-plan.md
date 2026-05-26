# U-23 — Recurring Billing + Calendar Refresh: NFR Design Plan

**Unit**: U-23 — Recurring Billing + Calendar Refresh  
**Phase**: CONSTRUCTION — NFR Design  
**Builds on**: approved NFR Requirements for `U-23`. See [nfr-requirements/](../u-23-recurring-billing-calendar-refresh/nfr-requirements/).

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

No additional user questions are needed for U-23 NFR Design. The approved NFR requirements and functional design already determine the pattern set cleanly:

- **Resilience patterns** — Applicable and already determined by the approved invalid-rebuild, same-day notice, and persistence-safety bar:
  - explicit rebuild-first recurring gate before charge/spawn
  - strong per-contract isolation inside the morning evaluation path
  - narrow successful-terms refresh through `ReplaceTermsSnapshot(...)`
  - existing same-day mail path reused for the supported recurring notices
- **Scalability patterns** — N/A. This is a local in-process single-player lifecycle seam with no distributed load, queue, replica, or scale-out mechanism.
- **Performance patterns** — Applicable and already determined:
  - lightweight synchronous 6am recurring evaluation
  - no async rebuild worker, speculative cache, or deferred billing pipeline
  - bounded notice shaping and no heavy post-hoc recalculation
- **Security patterns** — N/A. Security Baseline is disabled project-wide and the unit has no network/auth/PII surface.
- **Logical components** — Applicable and already determined:
  - `ContractTermsBuilder` remains the authoritative rebuild seam
  - `RecurringContractScheduler` stays the day-start shell but should be constrained
  - one narrow recurring decision/persistence helper seam should own deterministic skip/charge/notice rules
  - `ContractStore.ReplaceTermsSnapshot(...)` remains the only terms-refresh persistence seam
  - `MailDispatcher` remains the delivery seam for same-day recurring notices
  - dedicated U-23 test-side helpers should cover recurring morning invariants and notice precedence

The approved NFR requirements are all recommended-path decisions (`NFR-Q1=A` through `NFR-Q5=A`), so no clarification round is needed to resolve tradeoffs before producing the NFR design artifacts.

---

## Artifact Output

- `aidlc-docs/construction/u-23-recurring-billing-calendar-refresh/nfr-design/nfr-design-patterns.md`
- `aidlc-docs/construction/u-23-recurring-billing-calendar-refresh/nfr-design/logical-components.md`
