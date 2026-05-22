# U-15 — Recurring Lifecycle + Calendar Handlers: NFR Design Plan

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers
**Phase**: CONSTRUCTION — NFR Design

---

## Plan Checklist

- [x] Analyze NFR requirements + tech-stack decisions
- [x] Resolve the two deferred engineering items (fast-forward per-frame work, MFM money attachment) as patterns
- [x] Define lifecycle / calendar / fast-forward / tool / mail patterns (Q–U)
- [x] Define logical components + integration map
- [x] Generate `nfr-design-patterns.md`
- [x] Generate `logical-components.md`
- [x] Present completion message and await approval — **APPROVED 2026-05-21**

---

## Assessment — no blocking user questions

Consistent with U-10/U-13/U-13B/U-14 NFR Design (which resolved deferred engineering items as documented patterns rather than user questions), every U-15 NFR-design decision is an engineering pattern choice with a clear best-practice answer, not a product preference:

- **Resilience patterns** — the morning lifecycle is a guard chain that fails safe (festival/affordability skips queue a letter and return; missing calendar data → safe default). The at-save settlement is ordered (fast-forward → settle → persist) so atomicity is structural, not incidental. The two items deferred from NFR Requirements are resolved here: the **headless fast-forward** runs to completion within the `Saving` fade with no artificial per-frame cap in v1 (bounded by zone size; a pathological hitch is a code-gen play-test finding, mirroring U-14's large-attachment resolution), and the **MFM money attachment** uses a text-only "credit-on-collection" fallback if MFM can't carry gold (Pattern U).
- **Performance patterns** — scheduler once per day; calendar predicates O(1); fast-forward one-time at save; no new per-frame work.
- **Security patterns** — N/A (Security Baseline disabled, Q28).
- **Scalability patterns** — N/A (single-player mod).
- **Logical components** — one new Mod component (`CalendarHandlers`), a new orchestrator method (`FastForwardAndSettle`), a `ToolLevelReader` semantics change, and `MailDispatcher` extensions; removal of the tool-missing warning path. No new infrastructure (queues/caches/circuit-breakers) — mail "queuing" is the platform's.

No ambiguity requires user input before producing the NFR-design artifacts.

---

## Patterns (detail in nfr-design-patterns.md)
- **Pattern Q** — Calendar Predicate Adapter (`IsFestivalToday`/`IsRainyToday`) (FD-Q3→Clar-1a=C, FD-Q4=B)
- **Pattern R** — Morning Lifecycle Guard Chain (Service S-D: festival → config-lock → rain-rate → estimate/deposit → affordability → deduct/start) (FD-Q1/Q5/Q6=C)
- **Pattern S** — Ordered At-Save Settlement Hook + Time-Budgeted Headless Fast-Forward (FD-Q2=A, FD-Q7=A)
- **Pattern T** — Lowest-Tier Tool Fallback + warning-path removal (FD-Q8=C, Clar-2)
- **Pattern U** — Mailed Settlement: one letter (overflow items + refund gold) with money-attachment fallback (FD-Q9=C, Clar-3) — extends Patterns O/P
- **Retained unchanged** — entire U-13B worker behavioural loop + U-14 deposit/mail seam (Patterns L–P), except the seams Patterns S/T/U touch

---

## Artifact output
- `aidlc-docs/construction/u-15-recurring-lifecycle/nfr-design/nfr-design-patterns.md`
- `aidlc-docs/construction/u-15-recurring-lifecycle/nfr-design/logical-components.md`
