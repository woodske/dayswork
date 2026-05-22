# U-15 — Recurring Lifecycle + Calendar Handlers: NFR Requirements Plan

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers
**Phase**: CONSTRUCTION — NFR Requirements

---

## Plan Checklist

- [x] Analyze functional design artifacts (business-logic-model, domain-entities, business-rules)
- [x] Pull applicable NFRs from requirements.md per unit scope
- [x] Determine PBT obligations (PBT extension — Partial mode)
- [x] Record tech-stack decisions
- [x] Generate `nfr-requirements.md`
- [x] Generate `tech-stack-decisions.md`
- [x] Present completion message and await approval — **APPROVED 2026-05-21**

---

## Assessment — no blocking user questions

Consistent with how U-07, U-10, U-13, and U-14 NFR Requirements were handled, all U-15 NFRs are determinable from the approved Functional Design + prior project decisions:

- **Safety** dominates U-15's new branches and is fixed by the FD: conservation (NFR-SAFE-01) across festival skip / can't-afford skip / empty-zone / both fast-forward branches (BR-SAFE-01, BR-FF-05); integer refund (NFR-SAFE-02) is unchanged math, only the *delivery* moved to mail (BR-REF-01); save-integrity (NFR-SAFE-03) is preserved — like U-14, U-15 adds **no** custom Dayswork mail save data (refund/festival/cannot-afford letters all ride the platform/MFM "deliver tomorrow" queue), and festival/weather predicates degrade gracefully (BR-SAFE-02).
- **Performance**: the scheduler runs once per `DayStarted`; `CalendarHandlers` predicates are O(1) lookups; the sleep fast-forward is a **one-time, bounded** loop at the `Saving` event (bounded by the zone's remaining task count and the 8pm time budget), executed during the sleep fade — no per-frame cost. The save-time latency of the headless loop is a bounded engineering detail (cap mechanism, if any) deferred to NFR Design, not a product preference.
- **Usability** (NFR-UX-02): U-15 adds new user-visible strings (settlement-refund line, cannot-afford body, festival bodies, festival-skip log) — all routed through `I18nHelper`; the requirement itself is unambiguous and the keys are enumerated in domain-entities.md.
- **Reliability**: festival/weather predicate failures and the MFM money-attachment fallback (Clar-3=A) are handled-degradation cases, already decided.
- **Tech stack adds no new frameworks/dependencies**: MFM is already a required dependency from U-14; testing stays xUnit + FsCheck; the new logic is plain Mod-layer C# over existing seams.
- The single open *engineering* question — whether to cap the headless fast-forward's per-frame work and how to attach money to a letter if MFM lacks native support — is a pattern decision for **NFR Design / Code Generation**, recorded as a deferred tech decision rather than a user question.

No ambiguity requires user input before producing the NFR artifacts.

---

## Applicable NFRs (detail in nfr-requirements.md)
- **Safety**: NFR-SAFE-01 (conservation across all new branches — primary), NFR-SAFE-02 (integer refund unchanged; delivery mailed), NFR-SAFE-03 (no new Dayswork save data; tolerate absent festival/weather data), NFR-SAFE-04 (fast-forward still collects only self-caused drops).
- **Performance**: NFR-PERF-01 (no per-frame work; scheduler once per day; predicates O(1)), NFR-PERF-02 (fast-forward is a bounded one-time cost at save).
- **Usability**: NFR-UX-02 (new mail/log strings via i18n).
- **Reliability**: graceful festival/weather predicates, mailed-refund money fallback, one-settlement-letter guarantee, atomic at-save settlement before persistence/day-rollover.
- **Maintainability**: NFR-MAINT-03 (CalendarHandlers + fast-forward in Mod layer; pure pricing reused), NFR-MAINT-04 (no new Harmony patches), NFR-MAINT-05 (.NET conventions).
- **Compatibility**: NFR-COMPAT-04 (no new dependency; MFM already required).
- **PBT (Partial mode)**: refund-formula invariants (reuse U-05), fast-forward time-budget + conservation properties, single-active-contract hire guard (unit test), PBT-08 seed logging. PBT-02 **N/A** (no new round-trip serialization type).

---

## Artifact output
- `aidlc-docs/construction/u-15-recurring-lifecycle/nfr-requirements/nfr-requirements.md`
- `aidlc-docs/construction/u-15-recurring-lifecycle/nfr-requirements/tech-stack-decisions.md`
