# U-23 — NFR Requirements

**Unit**: U-23 — Recurring Billing + Calendar Refresh

U-23 is a recurring-lifecycle retrofit unit. Its NFR surface is centered on **a lightweight synchronous 6am recurring pass**, **strict determinism for rebuild, charge, persistence, and notice decisions**, **strong isolation of one contract's morning outcome from any other contract data being evaluated**, **reliable same-day visibility for the small supported notice set**, and **strong example + property-based regression coverage for the rebuilt recurring billing path**. NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A. Functional-design decisions FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, and FD-Q8=A apply throughout.

---

## Performance

### PERF-U23-01 — The 6am recurring evaluation pass remains comfortably lightweight (NFR-Q1=A)
Recurring term rebuild, affordability evaluation, terms-refresh persistence, and notice-decision logic must stay synchronous and cheap enough that normal day start has no visible hitching or noticeable delay.

This unit is not permitted to depend on:
- background workers or async day-start pipelines
- repeated redundant rebuilds of the same recurring contract during one 6am pass
- expensive farm-state rescans solely for billing logic
- heavy post-hoc recalculation after a skip/charge decision is already known

### PERF-U23-02 — U-23 must reuse the existing recurring shell
This retrofit should achieve its behavior by redirecting the existing day-start scheduler to the redesign-era contract-terms seams, not by introducing a second scheduler or parallel billing subsystem.

### PERF-U23-03 — Same-day notice shaping stays bounded
The supported same-day recurring notices are intentionally narrow. Festival, cannot-afford, and needs-attention letters must remain simple day-start decisions, not an unbounded lifecycle-reporting system.

---

## Reliability & Correctness

### REL-U23-01 — Pure recurring day-start decisions are strictly deterministic (NFR-Q2=A)
Equivalent saved-contract data, config, calendar flags, and gold inputs must produce the same:
- rebuilt recurring terms
- charge vs skip outcome
- terms-refresh persistence decision
- same-day notice choice

across runs and machines.

### REL-U23-02 — Determinism must not depend on incidental ordering
Recurring day-start outcomes must not vary because of:
- incidental dictionary or collection ordering
- stale compatibility-era `DepositAmount` / `HourlyRate` values
- mailbox ordering side effects
- hidden mutable scheduler state from a previous contract evaluation

### REL-U23-03 — Billing and runtime terms must stay aligned
When a recurring day successfully charges and starts, the worker must run from the same rebuilt `ContractTermsSnapshot` that authorized the charge.

### REL-U23-04 — One contract's outcome should not poison another contract's morning evaluation (NFR-Q3=A)
Per-contract isolation is required in the recurring evaluation seam. An invalid rebuild, cannot-afford result, or persistence refresh failure for one contract must not prevent other contracts from being evaluated correctly in the same pass.

Current v1 note:
- normal play still enforces at most one active/paused contract (`DEV-U15-01`)
- this isolation bar still applies because the scheduler/store remain list-based and future loosening of that invariant should not require a redesign of the error model

---

## Safety & Data Integrity

### SAFE-U23-01 — Rebuild-invalid recurring contracts fail safely before charge or spawn
If a recurring rebuild cannot produce a valid supported contract, U-23 must:
- take no charge
- avoid starting live work
- preserve the last known valid terms snapshot
- surface the supported needs-attention path instead of guessing or partially running

### SAFE-U23-02 — Successful refresh persistence is narrow and predictable
When a rebuild succeeds, replacing the saved terms snapshot must not mutate:
- saved scope
- destinations
- schedule
- status
- other recurring contract identity metadata

### SAFE-U23-03 — Festival and cannot-afford skips preserve recurring state safely
These skip reasons must not silently:
- pause the contract
- cancel the contract
- consume the recurring contract as if it had run
- reopen refund or debt semantics

### SAFE-U23-04 — Same-day notices must not quietly drift to tomorrow (NFR-Q4=A)
If U-23 selects a supported same-day notice, that explanation must be available in the same in-game day and not degrade into a delayed next-morning surprise.

---

## Usability & Interaction Quality

### USAB-U23-01 — Same-day recurring notices must stay clear and non-conflicting
The supported same-day notice set is deliberately small:
- festival skip
- cannot afford
- needs attention

The player should be able to quickly understand:
- why the worker did not run
- whether any charge was taken
- whether action is needed before the contract can run again

### USAB-U23-02 — Normal rain and low-work mornings remain intentionally silent
U-23 must not introduce noisy lifecycle messaging for:
- rain-satisfied outdoor watering
- little or no actionable work
- ordinary successful recurring days

That silence is a deliberate legibility choice, not missing functionality.

### USAB-U23-03 — Bulletin-board timing language must remain truthful
The recurring contract management surfaces must keep the approved timing semantics understandable:
- pre-6am edit/pause/cancel can still affect today's eligible morning
- after-6am changes are future-facing only

This is mostly a wording/truthfulness quality bar, not a new UI architecture requirement.

---

## Maintainability & Testability

### MAINT-U23-01 — Rebuild/charge/persist logic stays in pure or near-pure seams
The highest-value recurring rules should remain practical to test outside the full SMAPI runtime:
- recurring terms rebuild
- affordability decision
- notice precedence
- terms-refresh persistence decision
- before-6am vs after-6am lifecycle timing

### MAINT-U23-02 — Strong example + property coverage is required (NFR-Q5=A)
Because U-23 changes the authoritative recurring billing path, it carries a stronger regression bar than a normal orchestration tweak. It requires:
- focused example-based tests for key recurring morning scenarios
- meaningful FsCheck coverage for deterministic lifecycle invariants
- explicit regression coverage for persistence semantics and notice precedence

### MAINT-U23-03 — Property coverage must target the recurring lifecycle invariants
At minimum, FsCheck-friendly coverage for U-23 should exercise:
- rebuild determinism
- festival no-charge/no-spawn behavior
- affordability boundary behavior
- valid-vs-invalid terms-refresh persistence
- notice precedence stability
- pre-6am vs after-6am timing effects where modeled in pure seams

### MAINT-U23-04 — No new scheduling or messaging architecture is required
The required quality bar should be met through clearer seam ownership and stronger tests, not by introducing:
- a background morning job system
- a second recurring persistence model
- a new mail subsystem
- a separate billing cache or speculative rebuild layer

---

## Availability / Security / Infrastructure

### AVAIL-U23-01 — No availability-specific requirements
U-23 is an in-process single-player lifecycle seam. It has no external uptime, failover, or disaster-recovery surface.

### SEC-U23-01 — Security Baseline is N/A
Security Baseline is disabled project-wide. U-23 has no network, auth, or PII surface, so Security Baseline rules are N/A for this unit.

### INFRA-U23-01 — No infrastructure decisions introduced
U-23 requires no cloud, container, service, or deployment mapping beyond the existing `.NET 6` / SMAPI mod runtime.

---

## Property-Based Testing Obligations

### PBT-U23-01 — Rebuild determinism invariants
Equivalent saved scope, enabled tasks, config, and calendar inputs should rebuild into equivalent recurring terms and produce equivalent lifecycle decisions.

### PBT-U23-02 — Festival path invariants
Festival-day recurring contexts should never deduct gold or start the worker, even when rebuilt terms are valid and persistent refresh is allowed.

### PBT-U23-03 — Affordability boundary invariants
Generated contexts around exact-price, one-gold-short, and above-price cases should show stable charge/skip behavior with no hidden alternative path.

### PBT-U23-04 — Terms-refresh persistence invariants
Successful rebuilds should change only the saved terms snapshot, while invalid rebuilds should preserve the prior valid snapshot and all other contract data.

### PBT-U23-05 — Notice precedence invariants
Equivalent day-start failure/skip conditions should always collapse into the same single authoritative same-day notice according to the approved precedence.
