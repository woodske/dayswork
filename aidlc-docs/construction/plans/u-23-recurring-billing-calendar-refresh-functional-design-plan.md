# U-23 — Recurring Billing + Calendar Refresh: Functional Design Plan

**Unit**: U-23 — Recurring Billing + Calendar Refresh  
**Stories**: S-05, S-12, S-14, S-15, S-20  
**Phase**: CONSTRUCTION — Functional Design  
**Status**: Completed — functional-design artifacts generated and awaiting review approval.

---

## Plan Checklist

- [x] Load unit definition, story map, refreshed requirements, refreshed user stories, refreshed application design, and the latest retrofit code summaries
- [x] Inspect current recurring/day-start/calendar/save seams in `Dayswork/Orchestration/`, `Dayswork/Integration/`, and `Dayswork.Core/`
- [x] Draft FD-Q1 through FD-Q8
- [x] Collect answers to FD-Q1 through FD-Q8
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [x] Generate `frontend-components.md` (if warranted by the approved answers)
- [x] Present completion message and await approval

---

## Context Loaded

- [unit-of-work.md](../../inception/application-design/unit-of-work.md) — U-23 definition and definition of done
- [unit-of-work-story-map.md](../../inception/application-design/unit-of-work-story-map.md) — story ownership for `S-05`, `S-12`, `S-14`, `S-15`, and `S-20`
- [requirements.md](../../inception/requirements/requirements.md) — fixed recurring price, festival/rain/low-work, same-day notice, and sleep-settlement requirements
- [stories.md](../../inception/user-stories/stories.md) — player-facing expectations for pause/edit timing, recurring pricing stability, and sleep-stop outcomes
- [application-design.md](../../inception/application-design/application-design.md) — redesign summary and recurring `ContractTermsBuilder` ownership
- [components.md](../../inception/application-design/components.md)
- [component-methods.md](../../inception/application-design/component-methods.md)
- [services.md](../../inception/application-design/services.md)
- Latest retrofit carry-forward:
  - [u-19-contract-snapshot-persistence-legacy-cleanup/code/code-summary.md](../u-19-contract-snapshot-persistence-legacy-cleanup/code/code-summary.md)
  - [u-20-hiring-flow-preview-refresh/code/code-summary.md](../u-20-hiring-flow-preview-refresh/code/code-summary.md)
  - [u-21-worker-energy-shift-runtime-refresh/code/code-summary.md](../u-21-worker-energy-shift-runtime-refresh/code/code-summary.md)
  - [u-22-scope-driven-runtime-alignment/code/code-summary.md](../u-22-scope-driven-runtime-alignment/code/code-summary.md)
- Brownfield implementation review:
  - [RecurringContractScheduler.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Orchestration/RecurringContractScheduler.cs)
  - [CalendarHandlers.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Orchestration/CalendarHandlers.cs)
  - [ContractStore.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Persistence/ContractStore.cs)
  - [ContractPersistenceAdapter.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Integration/ContractPersistenceAdapter.cs)
  - [MailDispatcher.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Integration/MailDispatcher.cs)
  - [ShiftOrchestrator.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Orchestration/ShiftOrchestrator.cs)

---

## What This Unit Must Define

U-23 is the recurring-lifecycle retrofit that replaces the remaining deposit/refund day-start behavior with fixed recurring contract charging and stable calendar semantics.

This unit owns the functional behavior of:
- `C-15 ContractStore`
- `M-13 RecurringContractScheduler`
- `M-14 CalendarHandlers`
- `M-15 ContractPersistenceAdapter`
- `M-16 MailDispatcher`

This unit must define:
- how recurring contracts rebuild today's `ContractTermsSnapshot` from saved scope and current config before any eligible 6am charge
- the exact day-start sequence for festival checks, pause/cancel/edit state, affordability, charge timing, and shift start
- how recurring contracts behave when today's rebuild cannot produce valid terms
- how festival days, rainy days, and low-work mornings interact with charging and messaging while keeping recurring pricing predictable
- how sleep-stop completion now settles output and persistence without any refund logic
- which recurring lifecycle transitions need player-facing messages now that pricing is explicit and same-day notices already exist

Because this unit touches day-start behavior, persistence refresh timing, and mail wording, frontend/component notes may be needed even though the work is primarily orchestration and business-rule logic.

---

## Already Decided And Not Re-Decided Here

- One-time confirmation already charges the full contract price immediately; U-23 focuses on recurring day-start behavior.
- Recurring pricing is fixed by saved scope plus current config, not by today's actionable tile count.
- Festival days skip with no charge and a same-day explanatory mail message.
- Rain does not change recurring price; it may only reduce actionable outdoor work.
- Low-work recurring mornings still charge the normal recurring price because that day's labor capacity was reserved.
- Worker sleep-stop already settles buffered output and exits without refund logic after U-21.
- Typed runtime scope is already authoritative after U-22; U-23 should not reopen scope-selection or runtime-zone design.
- Config-driven price and energy changes for active recurring contracts apply starting the next morning, not retroactively to the already-started day.

This plan focuses only on the remaining functional-design decisions that shape recurring billing, day-start orchestration, and calendar-edge behavior.

---

## Design Questions

> Answer each question by writing after its `[Answer]:` tag. Pick the letter that best matches your preference. If none fit, choose `X` and describe your preference after the tag.

## Question 1
What should be the authoritative 6am sequence for an active recurring contract on a normal non-festival day?

A) Rebuild today's terms from saved scope and current config, validate/support-check them, check affordability against today's rebuilt fixed price, charge immediately if affordable, persist today's refreshed terms, then start the shift (Recommended)

B) Check affordability against the last saved terms first, then rebuild/persist today's terms only after the charge succeeds

C) Start the shift from the previously saved terms and defer any refresh until the next save/edit cycle

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 2
If the 6am recurring terms rebuild fails because saved scope/config can no longer produce a valid supported contract, how should the day behave?

A) Skip the day with no charge, leave the contract Active, and send a same-day notice explaining that the contract needs attention before it can run again (Recommended)

B) Auto-pause the contract with no charge and require the player to manually resume it after editing

C) Attempt to run from the previously saved terms snapshot anyway, even if today's rebuild failed

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 3
On a festival day, should U-23 still refresh a recurring contract's stored terms snapshot from current config/scope even though the worker is skipped?

A) Yes. Festival days still refresh and persist the latest recurring terms, but no charge is taken and only the courtesy skip notice is sent (Recommended)

B) No. Festival days should skip both charging and terms refresh, leaving the old snapshot untouched until the next eligible work day

C) Refresh terms only if the player edited the contract yesterday; otherwise skip the refresh

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 4
How should U-23 treat recurring contracts on rainy mornings when outdoor watering is rain-satisfied but other work may or may not remain?

A) Rebuild and charge the same fixed recurring price either way; rain only changes the actual amount of actionable work, not the billing or the need for any special rain mail (Recommended)

B) Charge normally only if at least one non-watering task remains actionable; otherwise skip the charge on pure-rain-no-work days

C) Keep the normal charge, but always send a same-day rain note when watering was a selected service

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 5
When a recurring contract has little or no actionable work at 6am on a non-festival day, how noisy should the lifecycle be?

A) Charge and run silently with no special mail; the worker may simply have very little to do because labor capacity was reserved (Recommended)

B) Charge normally, but send a same-day informational note whenever the work list is empty or nearly empty

C) Skip the charge when actionable work falls below a minimum threshold and notify the player

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 6
If the player edits an active recurring contract before 6am, when should the newly confirmed terms become authoritative for charging and runtime?

A) The next eligible 6am uses the newly saved scope immediately: rebuild from the edited scope/current config, charge that rebuilt price, and start the shift from those refreshed terms (Recommended)

B) The contract should carry the edit, but charging/runtime should still use the old terms for one more day before the new terms take effect

C) Editing before 6am should require explicit manual reactivation before the new terms can ever charge

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 7
How should U-23 handle pause/cancel requests that happen after 6am once the recurring day has already started?

A) Keep the current day committed: no refund or rollback, but the status change only affects future eligible days; if the worker is already out, the day continues to its normal U-21/U-22 stop behavior (Recommended)

B) Allow same-day cancellation with a pro-rated stop and immediate partial refund because the player changed their mind

C) Allow same-day pause to stop the worker immediately with no refund, but cancellation still waits for tomorrow

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 8
Which player-facing day-start notices should this unit actively support for recurring lifecycle clarity?

A) Only the blocking/exception cases: festival skip, cannot afford, and rebuild-invalid/needs-attention; normal rain and low-work days stay silent (Recommended)

B) Add proactive notices for rain-reduced work and low-work days too, so the player always knows why the worker seemed idle

C) Minimize mail further by dropping same-day notices except for affordability failures

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-23-recurring-billing-calendar-refresh/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-23-recurring-billing-calendar-refresh/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-23-recurring-billing-calendar-refresh/functional-design/business-rules.md`
- `aidlc-docs/construction/u-23-recurring-billing-calendar-refresh/functional-design/frontend-components.md` (if required by the approved answers)
