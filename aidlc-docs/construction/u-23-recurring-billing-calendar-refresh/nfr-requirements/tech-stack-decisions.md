# U-23 — Tech Stack Decisions

**Unit**: U-23 — Recurring Billing + Calendar Refresh

NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A. Functional-design decisions FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, and FD-Q8=A apply.

---

## TS-U23-01 — Stay on the existing SMAPI day-start shell
U-23 introduces no new runtime or scheduling framework. Implementation stays on the established architecture:
- `RecurringContractScheduler` as the day-start orchestration shell
- `CalendarHandlers` as the festival/weather/sleep boundary seam
- `ContractTermsBuilder` as the rebuilt pricing and energy authority
- `ContractStore.ReplaceTermsSnapshot(...)` as the narrow persistence seam
- `MailDispatcher` as the same-day notice delivery seam

This keeps the recurring retrofit incremental instead of layering in a second scheduler model.

## TS-U23-02 — Keep recurring rebuild authority in `ContractTermsBuilder`
The preferred implementation direction is:
- read saved `Contract.ScopeSelection` and `EnabledTasks`
- rebuild today's `ContractTermsSnapshot` from current config
- decide charge/skip from that rebuilt result

No ad hoc recurring pricing formula should be reintroduced in the scheduler.

## TS-U23-03 — Preserve narrow terms-refresh persistence through `ReplaceTermsSnapshot(...)`
When a rebuild succeeds, the preferred persistence path is:
- keep the saved contract otherwise intact
- replace only the `TermsSnapshot`
- do this immediately when the approved lifecycle path says refreshed terms should persist

This is simpler and safer than rewriting the full contract record just to refresh recurring pricing/energy terms.

## TS-U23-04 — Keep same-day notice delivery on the existing mail path
No new message or notification subsystem is needed. The preferred direction is:
- continue using the existing mail delivery path
- preserve same-day visibility for festival, cannot-afford, and needs-attention notices
- keep ordinary rain and low-work days silent

This satisfies the notice-visibility bar without duplicating infrastructure.

## TS-U23-05 — Keep recurring day-start evaluation synchronous and lightweight
The preferred implementation remains:
- rebuild inline at 6am
- make one authoritative decision per contract
- persist or skip inline
- start the shift or queue the supported same-day notice

No async rebuild worker, speculative cache, or deferred billing pipeline is required.

## TS-U23-06 — Use explicit per-contract isolation barriers in the scheduler path
Even though v1 currently enforces a single active/paused contract, the preferred implementation should still treat each evaluated contract as its own isolated decision unit:
- local rebuild result
- local persistence decision
- local notice decision
- local exception/diagnostic boundary

This keeps the scheduler resilient if malformed save data or future contract-expansion loosens the single-contract invariant.

## TS-U23-07 — Keep deterministic decision shaping close to pure seams
The main U-23 decisions should remain practical to test with pure or near-pure inputs:
- rebuild validity
- affordability outcome
- notice precedence
- terms-refresh persistence eligibility
- timing cutoffs for pre-6am vs after-6am management actions where the pure model owns them

This is the cleanest way to satisfy the strict determinism bar.

## TS-U23-08 — Tests stay on `xUnit` + `FsCheck`
No new test framework is needed. U-23 should lean on:
- `xUnit` for concrete recurring morning scenarios and persistence/notice regressions
- `FsCheck` for recurring lifecycle invariants and edge-boundary generation

The strongest value comes from generated day-start contexts rather than from UI-only tests.
