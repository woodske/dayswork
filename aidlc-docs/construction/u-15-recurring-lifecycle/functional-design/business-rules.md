# U-15 — Recurring Lifecycle + Calendar Handlers: Business Rules

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: FD-Q1=A, FD-Q2=A superseded by DEV-U15-09, FD-Q3→Clarification-1a=C, FD-Q4=B, FD-Q5=A, FD-Q6=C, FD-Q7=A, FD-Q8=C (+Clar-2a/2b/2c=A), FD-Q9=C (+Clar-3=A)

Enforceable rules for U-15. Each cites its source requirement/story/decision. See [business-logic-model.md](business-logic-model.md) for flows and [domain-entities.md](domain-entities.md) for types.

---

## Deviations introduced by U-15

| ID | Rule | Deviates from | Reason |
|---|---|---|---|
| **DEV-U15-01** | At most one contract may be `Active`/`Paused` at a time; enforced at hire time. | broadens FR-HIRE (multi-contract was implied) | Resolves TODO-04 for v1; the orchestrator manages a single worker. |
| **DEV-U15-02** | Festival days are skipped **and a courtesy letter is sent**. | FR-DAY-01 ("no mail") | Player-requested: keep the player informed instead of silent skip. |
| **DEV-U15-03** | A missing tool degrades to the lowest-tier tool; no skip, no warning; the tool-missing warning path is removed. | FR-TOOL-03; S-09 skip/warning; supersedes U-14 BR-MAIL-05 | Player-requested: the worker should still do the job with a basic tool. |
| **DEV-U15-04** | All refunds are delivered as gold-bearing mail next morning, not credited at exit. | FR-PAY-05 ("added directly to player gold at worker exit") | Player-requested immersion ("change with the morning post"). |
| **DEV-U15-05** | On rainy days only the Water Crops *surcharge* is excluded; the task is **not** force-skipped. | FR-PAY-07 ("the watering task is skipped") | Keeps watering available for building-interior crops (TODO-05); outdoor crops are rain-watered so the worker skips them naturally. |
| **DEV-U15-06** | Morning no-worker notices are same-day mailbox letters. | Earlier U-15 "all letters next morning" wording | Playtest showed a festival no-worker letter arriving after the festival was not useful. |
| **DEV-U15-07** | Recurring daily deposits use the same current flat 1.0-hour estimate as the hire preview. | Earlier U-15 plan wording that routed day-start through raw `HoursEstimator` | Playtest showed saved zones, especially building placeholders, could inflate the recurring deposit to impossible values. |
| **DEV-U15-08** | No-attachment letters register without MFM `dynamicItems`, and empty settlement letters are suppressed. | U-14 diagnostic behavior that always supplied `dynamicItems` | Playtest showed empty item slots on letters that carried no item; text-only/refund-only mail must not render an item slot. |
| **DEV-U15-09** | Sleeping stops the worker instead of running remaining work headlessly. | FD-Q2=A / earlier U-15 fast-forward design | Playtest showed sleep fast-forward was unreliable for v1; hard-stop settlement is simpler and predictable. |

---

## Single active contract (DEV-U15-01)

**BR-CTR-01 — One active contract.** At most one contract is in `Active` or `Paused` state at any time. The hiring flow refuses to create a new contract while one exists; the bulletin board offers "Hire" only when none is Active/Paused. *(FD-Q1=A, resolves TODO-04)*

**BR-CTR-02 — Scheduler processes ≤1 contract per day.** `OnDayStarted` may safely assume a single due contract; it never starts a second `ShiftOrchestrator.StartShift` in the same morning. *(FD-Q1=A)*

---

## Morning lifecycle (Service S-D)

**BR-DAY-01 — Multiplayer no-op.** `OnDayStarted` does nothing in a multiplayer session. *(REL-U10-01, FR-MP-01)*

**BR-DAY-02 — Festival gate runs first.** If `CalendarHandlers.IsFestivalToday()`, the worker does not show up; no shift is started. *(DEV-U15-02, FR-DAY-01)*

**BR-DAY-03 — Festival: recurring.** On a festival day a recurring contract deducts **no** deposit, stays `Active`, and a **text-only** festival-notice letter is available in the mailbox the same day. *(DEV-U15-02, DEV-U15-06, FR-DAY-01)*

**BR-CAL-03 — Festival: one-time.** A one-time contract whose scheduled day is a festival is **marked `Executed`**, its already-paid deposit is **refunded by same-day festival mail**, and the festival letter carries that refund gold. *(Sub-decision from FD-Q3=A semantics under Clar-1a=C skip; surfaced for approval — alternative is to roll the contract to the next non-festival day.)*

**BR-DAY-04 — Config lock-in.** The day's deposit and refund use the `IConfigSnapshot` captured at `DayStarted`; later GMCM edits apply only from the next morning. *(FR-PAY-08)*

**BR-DAY-04a — Deposit-hours policy consistency.** Until the tile-based pricing estimator is revisited, the hire preview and recurring day-start scheduler both use `DepositHoursPolicy.FlatPreviewHours` (1.0 billable hour). Saved zone geometry must not change the deposit on later recurring mornings. *(DEV-U15-07)*

**BR-DAY-05 — Rain-aware rate.** Today's rate is computed via `RateCalculator` with `IsRainyToday()` as the rain flag, excluding the Water Crops surcharge on rainy days. *(FR-PAY-07, FD-Q4=B)*

**BR-DAY-06 — Water Crops not force-skipped on rain.** The Water Crops task remains in the enabled set on rainy days; only its surcharge is removed from the rate. *(DEV-U15-05, FD-Q4=B)*

**BR-DAY-07 — Deposit deducted at 6am.** A recurring contract's daily deposit is deducted from player gold at `DayStarted` before the shift starts. *(FR-PAY-03)*

---

## Affordability (FR-PAY-04)

**BR-AFF-01 — Cannot afford → skip + notice.** If player gold < today's deposit, no deposit is deducted, no shift starts, and a text-only cannot-afford notice is available in the mailbox the same day. *(FR-PAY-04, FD-Q5=A, DEV-U15-06, S-12)*

**BR-AFF-02 — Stay active and retry daily.** A contract skipped for affordability stays `Active` and is re-evaluated every morning; the worker resumes automatically the first affordable morning. *(FD-Q5=A)*

**BR-AFF-03 — Notice each unaffordable day.** The cannot-afford notice is sent on **every** unaffordable morning; there is no de-duplication. *(FD-Q5=A)*

---

## Empty zone (FR-PAY-06)

**BR-EMP-01 — Deduct, run, mail full refund.** An empty-zone day still deducts the deposit at 6am and spawns the worker; finding no tasks, the worker exits and the full deposit is refunded **by mail** next morning (net zero, one-day lag). No pre-scan/skip in the scheduler. *(FD-Q6=C, FD-Q9=C, FR-PAY-06)*

---

## Sleep stop settlement (Service S-C, FR-DAY-02)

**BR-FF-01 — CalendarHandlers owns the save hook.** `CalendarHandlers.OnSavingHook` (subscribed to `GameLoop.Saving`) stops and settles the worker by calling `ShiftOrchestrator.StopForSleepAndSettle()`. `ShiftOrchestrator` does not subscribe to `Saving` directly. *(FD-Q7=A, DEV-U15-09)*

**BR-FF-02 — Ordering / atomicity.** The sleep stop and settlement mail queuing complete **before** the contract segment is persisted and before the day rolls over. *(FD-Q7=A, FR-DAY-02)*

**BR-FF-03 — Mid-work: hard stop.** When the worker is still working (`ShiftEndTime` unset), sleep sets `ShiftEndTime` to the current sleep time, performs no remaining task actions, and leaves remaining world tasks undone. Any collected-but-undelivered items are mailed as `NotDelivered`; any unearned refund is mailed. *(DEV-U15-09, FR-DAY-02)*

**BR-FF-04 — Already-finished: U-14 interruption path.** When the worker has finished working (`ShiftEndTime` set, Depositing/Exiting), all still-buffered items are mailed (reason `NotDelivered`) with no shipping-bin dump, exactly as U-14 BR-INT-01 — except the refund is now mailed (BR-REF-01). *(FD-Q5/U-14 carryover, DEV-U15-04)*

**BR-FF-05 — No sleep-time deposit run.** The sleep-stop path does not build a new deposit plan, touch chests, or dump to the shipping bin. Only items already collected by the worker are mailed back; remaining world objects stay in the world for later. *(DEV-U15-09, NFR-SAFE-01)*

---

## Missing tools (DEV-U15-03)

**BR-TOOL-01 — Missing → lowest tier.** `ToolLevelReader` reports a tool the player does not own at the lowest (basic) tier; the worker performs the task with the basic tool. No skip for a missing tool. *(FD-Q8=C, Clar-2a)*

**BR-TOOL-02 — Owned-tool tiers still gate.** Capability for tools the player owns is unchanged: lower-tier owned tools still cannot perform higher-tier-only actions (e.g., basic pickaxe cannot break boulders/meteorites), and fruit trees remain always-skip. Only the missing-tool branch changes. *(Clar-2b, FR-SKIP-03)*

**BR-TOOL-03 — No tool-missing warning.** `ShiftContext.ToolMissingWarnings` and `MailDispatcher.QueueToolMissingWarning` are removed; no tool-missing letter is ever sent. *(Clar-2c; removes FR-TOOL-03 warning clause and U-14 BR-MAIL-05)*

---

## Mailed settlement & refunds (DEV-U15-04)

**BR-REF-01 — Shift refunds are mailed next morning.** Normal-exit, sleep-stop, and empty-zone refunds are delivered as gold-bearing mail the next morning, not credited at exit. The one-time-festival refund is the exception because it rides the same-day no-worker festival letter (BR-CAL-03). The integer-clamped formula `deposit − (hoursWorked × rate)` is unchanged; deposit-run walking is still unbilled. *(FD-Q9=C, FR-PAY-05 deviated, NFR-SAFE-02, DEV-U15-06/09)*

**BR-REF-02 — Deposit still leaves immediately.** The deposit is deducted from gold at confirmation (one-time) or 6am (recurring); only the refund is delayed by one day. *(FR-PAY-03)*

**BR-REF-03 — One settlement letter per shift.** A shift queues at most one settlement letter carrying any overflow items (with U-14 reason-line body) and any refund gold. With neither overflow nor a positive refund, no letter is sent. *(DEV-U15-04, S-11 single-letter spirit)*

**BR-REF-04 — Money-attachment fallback.** If MFM cannot attach money, the refund is sent as a text-only "here's your change" letter that credits the gold when collected (still next morning). Items always use MFM's multi-attachment path. *(Clar-3=A)*

**BR-REF-05 — IntentApplyRefund routes to mail.** The state machine's refund intent no longer mutates `player gold`; it supplies the refund amount to the settlement step. *(DEV-U15-04)*

---

## Mail (general)

**BR-MAIL-01 — Sender + no fee.** All farmhand mail is from "Your farmhand" (`mail.sender`), charges nothing, and applies no rate adjustment. *(S-11, NFR-UX-02, NFR-SAFE-02)*

**BR-MAIL-02 — i18n-routed bodies.** Every user-visible mail/log string routes through `I18nHelper`; new keys are added to `i18n/default.json` (see domain-entities.md). *(NFR-UX-02, S-20)*

**BR-MAIL-03 — Timing by purpose.** Shift settlement letters (overflow items and/or refund gold) are queued for next-morning delivery and rely on Stardew/MFM persistence; morning no-worker notices (festival and cannot-afford) are registered with MFM and inserted into the current day's mailbox so the player can read them before the skipped day is over. Dayswork keeps no custom mail save data. *(DEV-U14-03 carryover, DEV-U15-06)*

**BR-MAIL-04 — No empty attachment slots.** Letters with no item attachments must register with no MFM `dynamicItems` provider. Settlement mail is not registered if all requested item attachments fail to materialize and there is no refund gold. *(DEV-U15-08)*

---

## Safety

**BR-SAFE-01 — No collected items lost across new branches.** Festival skip, can't-afford skip, empty-zone, and sleep-stop settlement preserve conservation for collected items: every collected item is deposited or mailed; no refund or collected item is dropped on save/day-rollover. Remaining undone world tasks are intentionally left in place when the farmer sleeps. *(NFR-SAFE-01, DEV-U15-09)*

**BR-SAFE-02 — Tolerate absent data.** Missing weather/festival/contract data degrades gracefully (treated as non-festival / non-rain / no contract), never a crash. *(NFR-SAFE-03)*

---

## PBT obligations (Property-Based Testing extension — enabled)

U-15 is largely SMAPI-event-driven (play-tested), but any predicates reducible to Core helpers carry PBT/unit coverage:

| Rule | Property / test |
|---|---|
| BR-REF-01 | Refund formula remains `clamp(deposit − hoursWorked × rate, 0, deposit)` for all generated (deposit, hours, rate) — reuses U-05 RefundCalculator invariants. |
| BR-DAY-04a | Deposit-hours policy remains independent of saved zone shape, including building placeholder zones. |
| BR-FF-03 | Sleep-stop refund examples pin that refund is based on the sleep stop time and full refund applies when stopped at shift start. |
| BR-CTR-01 | Hiring guard: given an existing Active/Paused contract, a second create is rejected (unit test). |
| BR-SAFE-01 | Conservation across sleep-stop settlement remains an integration/playtest concern: collected items are mailed, remaining unworked world objects are not claimed. |

Security Baseline extension is **disabled** project-wide (no network/PII/auth surface); all its rules are N/A for U-15.
