# U-23 — Logical Components

**Unit**: U-23 — Recurring Billing + Calendar Refresh

NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, and NFR-Q5=A apply. Functional-design decisions FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, and FD-Q8=A apply throughout.

---

## Component Map

```text
Dayswork.Core / Recurring Terms & Decisions
  ContractTermsBuilder                 [existing pure rebuild authority]
  RecurringDayStartDecisionEngine      [new pure or near-pure helper seam]
  RecurringNoticeSelector              [new narrow pure helper or folded sub-seam]

Dayswork.Core / Persistence
  ContractStore                        [existing store seam; ReplaceTermsSnapshot remains narrow authority]

Dayswork / Day-Start Shell & Delivery
  RecurringContractScheduler           [existing orchestration shell, constrained]
  CalendarHandlers                     [existing day-state seam]
  MailDispatcher                       [existing same-day notice delivery seam]
  ShiftOrchestrator                    [existing runtime starter, unchanged authority]

Dayswork.Tests / Recurring Lifecycle
  U23ExampleTests                      [test-side grouping]
  U23PropertyGenerators                [test-side helper]
  U23PropertyTests                     [test-side grouping]
```

No new async subsystem, cache layer, billing cache, or second notification pipeline is introduced.

---

## LC-U23-01 — ContractTermsBuilder (Authoritative Rebuild Seam)

**Layer**: Core / pure pricing-and-energy seam  
**Kind**: Existing production seam with preserved authority

**Purpose under U-23**:
- remain the only supported authority for rebuilding today's recurring pricing and worker-energy terms from saved scope plus current config

**Responsibilities**:
1. Rebuild valid recurring `ContractTermsSnapshot` values from saved scope and enabled tasks
2. Surface invalid or unsupported recurring terms outcomes through the existing redesign-era validation model
3. Keep rebuilt pricing and energy aligned for the same day-start decision

**Important design constraints**:
- no scheduler-owned ad hoc recurring pricing formulas
- no dependence on compatibility-era `DepositAmount` / `HourlyRate`
- deterministic output for equivalent inputs

This is the primary owner of U-23's rebuilt recurring authority.

---

## LC-U23-02 — RecurringDayStartDecisionEngine

**Layer**: Core or near-pure lifecycle helper seam  
**Kind**: New logical helper or equivalent extracted decision behavior

**Purpose**:
- turn rebuilt terms, calendar state, affordability, and current contract state into one deterministic recurring morning outcome

**Responsibilities**:
1. Interpret rebuild result as valid vs invalid/unsupported
2. Apply the approved sequencing for festival, affordability, persistence, charge, and shift-start behavior
3. Produce one structured day-start outcome per evaluated contract
4. Keep persistence eligibility and start/skip decisions explicit

**Not responsible for**:
- mutating player gold directly
- writing mail directly
- starting live shifts

This seam is what keeps the highest-risk recurring lifecycle rules out of ad hoc scheduler branching.

---

## LC-U23-03 — RecurringNoticeSelector

**Layer**: Core / pure decision helper or folded sub-seam within the decision engine  
**Kind**: New narrow logical helper behavior

**Purpose**:
- choose the single authoritative same-day recurring notice kind when a notice is needed

**Responsibilities**:
1. Enforce the approved notice set:
   - festival skip
   - cannot afford
   - needs attention
2. Enforce the approved precedence:
   - needs attention
   - cannot afford
   - festival skip
3. Preserve silence for ordinary successful, rainy, and low-work recurring days

**Why it matters in NFR design**:
- it keeps notice precedence deterministic and easy to test
- it prevents courtesy messaging from leaking into multiple scheduler branches

---

## LC-U23-04 — ContractStore (Narrow Terms-Refresh Persistence Authority)

**Layer**: Core / persistence seam  
**Kind**: Existing production seam with preserved narrow ownership

**Purpose under U-23**:
- remain the only place that persists successful recurring terms refreshes

**Responsibilities**:
1. Replace only `TermsSnapshot` when a valid rebuild should persist
2. Preserve scope, destinations, schedule, status, and contract identity data
3. Leave the prior valid snapshot intact when rebuild is invalid

**Important design constraint**:
- U-23 should not expand this into a broad rewrite-everything persistence path just to refresh recurring pricing

---

## LC-U23-05 — RecurringContractScheduler (Constrained Day-Start Shell)

**Layer**: App / SMAPI orchestration seam  
**Kind**: Existing production seam with constrained U-23 ownership

**Purpose under U-23**:
- remain the live 6am adapter while delegating deterministic recurring decision logic to narrower seams

**Responsibilities**:
1. Enumerate eligible recurring contracts for the morning
2. Obtain current day-state and config inputs
3. Invoke rebuild and decision helpers
4. Apply resulting side effects:
   - deduct gold when authorized
   - persist refreshed terms when authorized
   - queue supported same-day notices
   - start the shift when authorized

**Important design constraints**:
- do not become the source of truth for recurring pricing formulas
- do not fuse rebuild interpretation, notice precedence, and persistence eligibility into one large method
- do not introduce global failure semantics that violate per-contract isolation

---

## LC-U23-06 — CalendarHandlers (Preserved Day-State Authority)

**Layer**: App / live day-state seam  
**Kind**: Existing production seam with preserved authority

**Purpose under U-23**:
- remain the single place that exposes festival/weather/sleep day-state facts to the recurring scheduler

**Responsibilities**:
1. Surface festival-day status for recurring skip logic
2. Surface rainy-day status for runtime context while keeping price unchanged
3. Preserve the existing sleep-stop hook shape for already-started recurring shifts

This component is important because U-23's determinism bar depends on clear, centralized day-state inputs.

---

## LC-U23-07 — MailDispatcher (Same-Day Recurring Notice Delivery Seam)

**Layer**: App / delivery integration seam  
**Kind**: Existing production seam with preserved ownership

**Purpose under U-23**:
- continue to deliver same-day recurring notices without becoming the source of truth for notice selection

**Responsibilities**:
1. Render and deliver the supported recurring notices
2. Preserve reliable same-day visibility for those notices
3. Keep silence on ordinary successful, rainy, and low-work recurring days

**Important constraint**:
- `MailDispatcher` should deliver the decided notice kind, not own recurring notice-precedence logic itself

---

## LC-U23-08 — ShiftOrchestrator (Unchanged Runtime Starter)

**Layer**: App / runtime shell seam  
**Kind**: Existing production seam with unchanged ownership

**Purpose under U-23**:
- start the live worker only after recurring day-start logic has authorized today's charge and rebuilt terms

**Responsibilities**:
1. Accept the charged and refreshed recurring contract state
2. Start the shift using the rebuilt `ContractTermsSnapshot`
3. Keep U-21/U-22 runtime behavior unchanged once the day has committed

This seam is listed explicitly because U-23's correctness bar requires billing and runtime terms to stay aligned.

---

## LC-U23-09 — Test-Side Recurring Lifecycle Support

**Layer**: `Dayswork.Tests` only  
**Kind**: Dedicated regression-support helpers

### `U23PropertyGenerators`

**Purpose**:
- generate recurring day-start contexts with varied:
  - scope/task combinations
  - rebuilt-term validity outcomes
  - gold levels around exact affordability boundaries
  - festival vs non-festival day-state
  - pre-6am vs after-6am management timing cases where modeled in pure seams

### `U23ExampleTests`

**Purpose**:
- pin concrete stories such as:
  - valid festival-day terms refresh with no charge
  - valid exact-gold recurring start
  - invalid rebuild that surfaces only needs-attention
  - successful rebuild that persists only `TermsSnapshot`
  - after-6am cancellation leaving the already-committed day intact

### `U23PropertyTests`

**Purpose**:
- express invariants with FsCheck:
  - deterministic rebuild and decision outcomes
  - affordability boundary stability
  - narrow terms-refresh persistence
  - stable notice precedence
  - per-contract isolation for independent evaluated inputs

These are explicit logical components because U-23's NFR bar is driven by recurring morning combinations more than by isolated scheduler calls.

---

## Interaction Summary

```text
6am recurring evaluation
  -> CalendarHandlers supplies current day-state
  -> ContractTermsBuilder rebuilds today's terms
  -> RecurringDayStartDecisionEngine determines skip / charge / persist / start
  -> RecurringNoticeSelector collapses to one authoritative same-day notice when needed
  -> ContractStore applies narrow terms refresh when authorized
  -> RecurringContractScheduler applies gold/mail/runtime side effects
  -> ShiftOrchestrator starts only the charged, authorized shift
```

---

## Why no additional scheduling infrastructure was introduced

The NFR design intentionally does **not** add:
- a background morning job worker
- a recurring billing cache
- a second persistence model for refreshed recurring terms
- a new notification subsystem

Reason:
- the lifecycle is local and synchronous by design
- the hardest risks are deterministic decision-making, same-day visibility, and persistence safety
- the existing shell is sufficient if rebuild, notice, and persistence authority are pulled into narrower seams

That keeps U-23's recurring retrofit incremental, testable, and consistent with the rest of the redesign.
