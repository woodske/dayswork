# U-14 — Output Pipeline: NFR Design Plan

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail
**Phase**: CONSTRUCTION — NFR Design

---

## Plan Checklist

- [x] Analyze NFR requirements + tech-stack decisions
- [x] Resolve the two deferred engineering items (MFM large-attachment, GetApi-null) as patterns
- [x] Define deposit/mail design patterns
- [x] Define logical components + integration map
- [x] Generate `nfr-design-patterns.md`
- [x] Generate `logical-components.md`
- [ ] Present completion message and await approval

---

## Assessment — no blocking user questions

Consistent with U-10/U-13/U-13B NFR Design (which resolved deferred engineering items as documented patterns rather than user questions), every U-14 NFR-design decision is an engineering pattern choice with a clear best-practice answer, not a product preference:

- **Resilience patterns** — chest-full/missing/unassigned/sleep-interrupt all funnel into one Overflow Accumulator → single-letter flush (Pattern O). The two items deferred from NFR Requirements are resolved here: a **large overflow attachment** is handed to MFM in one call (product rule "one letter, all items" wins; any practical MFM cap is a code-gen play-test point), and a **null MFM API** logs-and-continues without crashing (items stay in the mail intent, never discarded). Both are standard defensive patterns (Pattern P).
- **Performance patterns** — planning is one-time at shift end; the multi-trip loop reuses the existing per-trip navigation; no new per-frame work (Pattern M/N).
- **Security patterns** — N/A (Security Baseline disabled, Q28).
- **Scalability patterns** — N/A (single-player mod).
- **Logical components** — additions are confined to the deposit/mail seam (DepositPlanner, MailDispatcher) plus pure-Core extensions (ItemBuffer +SourceTask, ShiftContext +TaskDestinations/+Overflow, ShiftIntent +IntentDepositAtChest). No new infrastructure (queues/caches/circuit-breakers) — mail "queuing" is the platform's.

No ambiguity requires user input before producing the NFR-design artifacts.

---

## Patterns (detail in nfr-design-patterns.md)
- **Pattern L** — Collection-Time Task Tagging (FD-Q1=A)
- **Pattern M** — Pure Deposit Planner with Injected Distance Oracle (FD-Q1/Q2/Q3=A)
- **Pattern N** — Multi-Trip Deposit Loop (intent re-issue, no new phase) (BR-SM-01)
- **Pattern O** — Overflow Accumulator + Single-Letter Flush (FD-Q5/Q6=A)
- **Pattern P** — Mail Adapter over MFM (deliver-tomorrow) + vanilla no-item warnings (FD-Q4/Q7=A, V9)
- **Retained unchanged** — entire U-13B worker behavioural loop (throttle, movement, render, save-exclusion, stuck, invuln, tool visuals)

---

## Artifact output
- `aidlc-docs/construction/u-14-output-pipeline/nfr-design/nfr-design-patterns.md`
- `aidlc-docs/construction/u-14-output-pipeline/nfr-design/logical-components.md`
