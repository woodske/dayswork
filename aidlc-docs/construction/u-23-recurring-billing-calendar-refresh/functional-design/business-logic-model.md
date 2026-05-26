# U-23 — Recurring Billing + Calendar Refresh: Business Logic Model

**Unit**: U-23 — Recurring Billing + Calendar Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A

Technology-agnostic recurring-lifecycle flows for replacing the remaining day-start deposit/refund behavior with the redesign-era fixed recurring contract model.

This unit locks five major recurring-lifecycle behaviors:
- each eligible recurring morning rebuilds today's terms from saved scope plus current config before any charge
- successful rebuilds become the contract's latest saved terms snapshot of record
- festival days still refresh recurring terms but never charge or spawn the worker
- rain and low-work mornings stay price-stable and silent
- after-6am pause/cancel does not interrupt or refund the already-committed day

See [domain-entities.md](domain-entities.md) for lifecycle data shapes, [business-rules.md](business-rules.md) for enforceable rules, and [frontend-components.md](frontend-components.md) for the bulletin-board and notice-facing implications.

---

## 0. Where this plugs into the redesign

U-18 introduced authoritative contract pricing and energy snapshots:

```text
ContractScopeSelection + EnabledTasks + CurrentConfig
  -> ContractTermsBuilder
  -> ContractTermsSnapshot
```

U-19 established that recurring contracts persist both:
- saved typed scope as the durable source of truth
- the latest known `ContractTermsSnapshot` as the saved pricing/energy snapshot

U-21 removed refund/debt settlement from active shift runtime, and U-22 made typed scope authoritative for execution.

U-23 is the recurring day-start bridge:

```text
Active recurring contract at 6am
  -> rebuild today's terms from saved scope + current config
  -> persist refreshed terms when rebuild succeeds
  -> decide skip / charge / start
  -> queue only the supported same-day notices
```

The important boundary is that U-23 does not redesign live shift behavior again. It only decides how recurring contracts become today's authoritative shift input and billing event.

---

## 1. Authoritative day-start inputs

For an active recurring contract on a candidate work day, day-start orchestration reads:
- the saved `Contract`
- `Contract.ScopeSelection` as the durable scope source of truth
- `Contract.EnabledTasks`
- the current config snapshot at 6am
- today's calendar state
- the farmer's gold at 6am

The rebuilt contract price for today is therefore not:
- yesterday's saved price blindly replayed
- a variable price based on today's actionable tile count
- a rain-adjusted discount

Instead it is the fixed price produced by rebuilding today's `ContractTermsSnapshot` from:
- saved scope
- saved enabled services
- current config values

This keeps recurring pricing stable relative to the contract the player saved, while still allowing config changes to take effect the next morning.

---

## 2. Normal eligible recurring day

### 2.1 Morning candidate selection

At 6am, the scheduler identifies contracts that are:
- `Active`
- `Recurring`
- scheduled for today

Edits, pauses, and cancels that were saved before 6am are already reflected in that persisted contract state before U-23 begins its morning logic.

### 2.2 Terms rebuild first

For each active recurring candidate, the first authoritative step is:

```text
saved scope + enabled tasks + current config
  -> rebuild today's contract terms
```

That rebuild determines:
- today's fixed daily price
- today's worker energy profile
- whether the saved contract is still valid and supported

### 2.3 Valid successful rebuild on a normal non-festival day

If rebuild succeeds and today is not a festival:
1. today's fixed price is read from the rebuilt terms
2. affordability is checked against that rebuilt fixed price
3. if affordable, the charge is taken immediately at 6am
4. today's refreshed terms become the persisted snapshot of record
5. the shift starts from those refreshed terms

This is the player-facing recurring contract story:
- the contract is repriced from what was saved
- the player pays exactly that rebuilt recurring price for today
- the worker runs on the same pricing/energy snapshot that billing used

### 2.4 Successful rebuild but cannot afford

If rebuild succeeds but the farmer cannot afford today's rebuilt fixed price:
- no gold is deducted
- the worker does not spawn
- the contract stays `Active`
- the latest valid rebuilt terms still replace the saved terms snapshot
- a same-day cannot-afford notice is sent

This keeps the contract ready to retry tomorrow without forcing the player to re-edit or re-enable it.

---

## 3. Festival-day behavior

Festival days keep their own dedicated recurring path.

For an active recurring contract on a festival day:
1. rebuild today's terms from saved scope and current config
2. if rebuild succeeds, persist the refreshed terms snapshot
3. take no charge
4. do not spawn the worker
5. send the same-day festival skip notice

This preserves two important truths at once:
- the contract did not run and the player was not charged
- the saved recurring record still stays current with the latest config-driven pricing and energy values

### 3.1 Festival day with rebuild failure

If the festival-day rebuild cannot produce a valid supported recurring contract:
- no charge is taken
- no worker spawns
- the contract stays `Active`
- no refreshed terms snapshot is persisted
- the actionable same-day needs-attention notice wins over the courtesy festival skip notice

The reasoning is that actionable contract repair information is more important than the ordinary festival courtesy message.

---

## 4. Rebuild-invalid or unsupported contracts

U-23 treats "saved scope/config can no longer produce a valid supported contract" as a blocking day-start condition.

Examples include:
- typed scope missing or unusable
- selected services no longer have any chargeable supported scope-task pair
- saved contract shape is outside the supported redesign path

The blocking-invalid day-start flow is:

```text
rebuild attempted
  -> invalid / unsupported result
  -> no charge
  -> no worker
  -> contract remains Active
  -> same-day needs-attention notice
```

This contract is not auto-paused. The player can:
- edit it before a future 6am
- pause it manually
- cancel it manually
- leave it active and let it keep surfacing the blocking notice until corrected

The important design choice is that U-23 does not silently fall back to stale saved terms and does not guess a partial execution path.

---

## 5. Rain and low-work semantics

### 5.1 Rain

Rain changes what work is actionable, not what the contract costs.

So on rainy mornings:
- the recurring contract still rebuilds the same way
- the rebuilt fixed price is unchanged by rain
- the worker still runs if the rebuilt contract is valid and affordable
- outdoor watering may naturally collapse to little or no live work
- greenhouse and non-watering services still behave normally

U-23 does not send a special same-day rain notice.

### 5.2 Low-work or no-work mornings

Recurring contracts reserve labor capacity, not guaranteed workload volume.

So when the selected recurring scope happens to contain little or no actionable work that morning:
- the normal recurring price still applies
- the worker may have a short shift or almost nothing to do
- no special low-work informational notice is sent

This keeps recurring behavior legible and non-noisy:
- price is tied to the saved contract
- runtime value varies naturally with the farm's morning state

---

## 6. Edit, pause, and cancel timing

### 6.1 Edits before 6am

If the player edits an active recurring contract before 6am and confirms the change:
- the edited scope and settings save immediately
- the next eligible 6am rebuild uses that edited scope
- today's rebuilt price and shift terms come from the edited contract, not the old snapshot

There is no one-day lag after a pre-6am edit.

### 6.2 Pause or cancel before 6am

If the player pauses or cancels before 6am:
- the persisted status change is authoritative for that morning
- no recurring day-start charge occurs for that contract
- no worker spawns for that contract

### 6.3 Pause or cancel after 6am

Once the recurring morning has already committed:
- no same-day rollback occurs
- no refund is computed
- no in-progress shift interruption is introduced by U-23

The current day continues under the already-authoritative U-21/U-22 runtime model, and the status change only affects future eligible mornings.

---

## 7. Supported same-day notices

U-23 deliberately keeps recurring lifecycle mail narrow.

The supported same-day notices are:
- festival skip
- cannot afford
- rebuild invalid / needs attention

U-23 does not add same-day notices for:
- rain-reduced work
- low-work mornings
- ordinary successful recurring runs

### 7.1 Notice precedence

At most one recurring day-start notice should be authored for the same contract/day-start decision path.

The intended priority is:
1. rebuild invalid / needs attention
2. cannot afford
3. festival skip

That means courtesy messaging never hides a blocking actionable problem.

---

## 8. Sleep-stop alignment

U-21 already defined that player sleep:
- stops the worker
- settles buffered output safely
- does not compute a refund

U-23 preserves that shape for recurring work.

Once a recurring day has successfully charged and started:
- that day is committed
- sleep-stop remains a runtime/output-settlement concern
- sleep does not reopen billing or recurring repricing

This keeps recurring lifecycle logic cleanly separated from active shift wrap-up.

---

## 9. Testable properties

Property-Based Testing is enabled in partial mode. U-23 should preserve pure recurring/day-start seams that support deterministic lifecycle verification.

| Component / seam | Category | Property to carry into code generation |
|---|---|---|
| recurring terms rebuild + decision seam | Invariant | Equivalent saved scope, enabled tasks, config, calendar flags, and gold inputs produce equivalent recurring day-start outcomes. |
| successful rebuild persistence | Invariant | A successful rebuild updates only the saved terms snapshot and leaves saved scope, destinations, status, and hire metadata unchanged. |
| festival path | Invariant | Festival days never charge or spawn a worker, even when rebuild succeeds. |
| rain / low-work handling | Invariant | Rain and low-actionable-work conditions never change the rebuilt recurring price or trigger extra lifecycle mail on their own. |
| pre-6am vs post-6am status changes | Oracle / easy verification | Before-6am pause/cancel suppresses that morning; after-6am pause/cancel leaves the already-committed day intact. |
| notice precedence | Invariant | Blocking recurring lifecycle notices dominate courtesy notices so each contract/day emits at most one authoritative day-start message. |

These are design-time property identifications for the later U-23 code-generation and test-planning stages.
