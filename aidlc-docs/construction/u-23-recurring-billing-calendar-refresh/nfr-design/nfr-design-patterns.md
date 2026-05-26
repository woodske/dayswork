# U-23 — NFR Design Patterns

**Unit**: U-23 — Recurring Billing + Calendar Refresh

NFR design decisions applied: no additional question round required. NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, and NFR-Q5=A apply, along with functional-design decisions FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, and FD-Q8=A.

---

## Applicability Scope

| Category | Applicability |
|---|---|
| Security | **N/A** — Security Baseline is disabled project-wide and U-23 has no network/auth/PII surface |
| Scalability / HA | **N/A** — local single-player in-process lifecycle seam; no replicas, shards, queues, or distributed scale mechanisms |
| Distributed infrastructure | **N/A** — no service deployment, queue, cache server, or async worker runtime |
| Resilience | **Applicable** — invalid rebuild fail-safe behavior, same-day notice reliability, and per-contract isolation in the morning pass |
| Performance | **Applicable** — lightweight synchronous 6am rebuild/charge evaluation with bounded notice shaping |
| Determinism / correctness | **Applicable** — strict deterministic rebuild, affordability, persistence, and notice-precedence decisions |
| Maintainability / testability | **Applicable** — pure or near-pure recurring lifecycle seams plus strong example/property coverage |

---

## PAT-U23-01 — Rebuild-First Recurring Gate

**What**: Every eligible recurring day passes through one explicit rebuild-first authority gate before any charge or spawn occurs.

**Applies to**:
- `PERF-U23-01` lightweight synchronous morning pass
- `REL-U23-01` strict deterministic day-start decisions
- `SAFE-U23-01` invalid rebuilds fail safely before charge or spawn
- `TS-U23-02` keep recurring rebuild authority in `ContractTermsBuilder`
- `TS-U23-05` keep recurring day-start evaluation synchronous and lightweight

**How**:
- the scheduler first rebuilds today's `ContractTermsSnapshot` from saved scope plus current config
- all later day-start decisions branch from that single authoritative rebuild outcome
- stale compatibility pricing fields are excluded from the authoritative path

**Why this pattern**:
- it keeps pricing, energy, and validity decisions aligned
- it prevents old deposit/refund-era data from lingering as an accidental authority
- it makes the day-start lifecycle easier to reason about and test

---

## PAT-U23-02 — Deterministic Decision Pipeline with Stable Notice Precedence

**What**: The recurring day-start lifecycle is modeled as one deterministic decision pipeline with stable precedence between rebuild-invalid, cannot-afford, and festival skip outcomes.

**Applies to**:
- `REL-U23-01` strict deterministic decisions
- `REL-U23-02` determinism must not depend on incidental ordering
- `USAB-U23-01` same-day notices stay clear and non-conflicting
- `TS-U23-07` keep deterministic decision shaping close to pure seams

**How**:
- rebuild result is determined first
- festival, affordability, persistence, and start behavior are derived from that result in a fixed order
- notice selection collapses to one authoritative same-day notice according to the approved precedence

**Why this pattern**:
- it prevents contradictory lifecycle outcomes
- it keeps courtesy messaging from hiding actionable contract problems
- it makes persistence and mail rules stable enough for property-based testing

---

## PAT-U23-03 — Per-Contract Isolation Barrier in the Morning Evaluation Pass

**What**: Each evaluated recurring contract is treated as its own isolated morning decision unit, even though v1 normally allows only one active/paused contract.

**Applies to**:
- `REL-U23-04` one contract's outcome should not poison another
- `SAFE-U23-02` successful refresh persistence is narrow and predictable
- `TS-U23-03` preserve narrow terms-refresh persistence
- `TS-U23-06` use explicit per-contract isolation barriers

**How**:
- rebuild, persistence, notice, and charge/start decisions are scoped locally to one contract
- errors or unsupported outcomes are contained to that contract's evaluation path
- the store remains list-safe even if malformed save data or future feature work loosens the single-contract invariant

**Why this pattern**:
- it avoids brittle global morning-failure behavior
- it future-proofs the scheduler seam without designing full multi-contract orchestration now
- it keeps persistence and diagnostics targeted instead of global

---

## PAT-U23-04 — Narrow Successful-Refresh Persistence

**What**: Valid recurring rebuilds persist through a narrow terms-refresh seam rather than through whole-contract rewrites.

**Applies to**:
- `REL-U23-03` billing and runtime terms stay aligned
- `SAFE-U23-02` successful refresh persistence is narrow and predictable
- `MAINT-U23-01` rebuild/charge/persist logic stays in pure or near-pure seams
- `TS-U23-03` preserve narrow terms-refresh persistence through `ReplaceTermsSnapshot(...)`

**How**:
- successful rebuilds replace only `TermsSnapshot`
- invalid rebuilds preserve the last known valid saved terms snapshot
- the same refreshed terms snapshot that persists is the one used for today's charge and runtime start

**Why this pattern**:
- it reduces mutation surface area
- it protects saved scope and destination data from incidental rewrite bugs
- it gives tests one small persistence seam to verify

---

## PAT-U23-05 — Existing Same-Day Mail Path with Bounded Lifecycle Notice Set

**What**: The supported recurring notices stay on the existing mail path and are deliberately bounded to festival skip, cannot afford, and needs attention.

**Applies to**:
- `PERF-U23-03` same-day notice shaping stays bounded
- `SAFE-U23-04` same-day notices must not drift to tomorrow
- `USAB-U23-02` ordinary rain and low-work mornings stay silent
- `TS-U23-04` keep same-day notice delivery on the existing mail path

**How**:
- day-start logic chooses from a small fixed notice set
- the existing same-day delivery mechanism is reused
- ordinary successful, rainy, and low-work mornings produce no lifecycle mail

**Why this pattern**:
- it preserves clarity without turning morning mail into a verbose report
- it reuses already-proven same-day notice infrastructure
- it keeps lifecycle messaging aligned with the approved player-facing model

---

## PAT-U23-06 — Thin Scheduler, Narrow Recurring Decision Helper Seams

**What**: `RecurringContractScheduler` remains the day-start shell, while narrower helper seams own deterministic recurring decision logic and persistence shaping.

**Applies to**:
- `PERF-U23-02` reuse the existing recurring shell
- `MAINT-U23-01` rebuild/charge/persist logic stays in pure or near-pure seams
- `MAINT-U23-04` no new scheduling or messaging architecture is required
- `TS-U23-01` stay on the existing SMAPI day-start shell

**How**:
- the scheduler coordinates live day-start events
- narrow helper logic owns rebuild interpretation, affordability outcome, persistence eligibility, and notice precedence
- scheduler branches become orchestration over structured decisions rather than ad hoc pricing logic

**Why this pattern**:
- it prevents U-23 from collapsing into one large scheduler method
- it keeps the highest-risk lifecycle rules close to pure inputs
- it makes the later code-generation step easier to stage and test

---

## PAT-U23-07 — Dedicated Recurring Lifecycle Regression Support

**What**: U-23's stronger quality bar is satisfied through focused recurring morning examples plus property-based lifecycle invariants.

**Applies to**:
- `MAINT-U23-02` strong example + property coverage
- `MAINT-U23-03` property coverage must target recurring invariants
- `PBT-U23-01` through `PBT-U23-05`
- `TS-U23-08` tests stay on `xUnit` + `FsCheck`

**How**:
- example tests pin concrete stories such as valid festival skip with refreshed terms, exact-affordability success, invalid rebuild with needs-attention, and future-facing after-6am cancellation
- FsCheck generators produce recurring day-start contexts with varied scope, config, gold, and calendar inputs
- property tests verify deterministic rebuilds, stable notice precedence, and narrow persistence behavior

**Why this pattern**:
- U-23's hardest risks come from combinations of day-start flags and contract state, not just single isolated calls
- generated contexts are the best fit for the enabled partial PBT mode
- dedicated test-side helpers keep production orchestration simpler

---

## Pattern Summary

U-23's NFR design stays intentionally focused:
- one rebuild-first authority gate before charge or spawn
- one deterministic decision pipeline with stable notice precedence
- one per-contract isolation barrier in the morning pass
- one narrow successful-refresh persistence seam
- one bounded same-day notice set on the existing mail path
- one thin-scheduler / narrow-helper split
- one dedicated recurring lifecycle regression-support strategy

That gives the recurring billing retrofit a strong performance, determinism, and reliability bar without introducing new runtime infrastructure or re-opening the deposit/refund model.
