# U-15 — Recurring Lifecycle + Calendar Handlers: Business Rules

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3→Clarification-1a=C, FD-Q4=B, FD-Q5=A, FD-Q6=C, FD-Q7=A, FD-Q8=C (+Clar-2a/2b/2c=A), FD-Q9=C (+Clar-3=A)

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

---

## Single active contract (DEV-U15-01)

**BR-CTR-01 — One active contract.** At most one contract is in `Active` or `Paused` state at any time. The hiring flow refuses to create a new contract while one exists; the bulletin board offers "Hire" only when none is Active/Paused. *(FD-Q1=A, resolves TODO-04)*

**BR-CTR-02 — Scheduler processes ≤1 contract per day.** `OnDayStarted` may safely assume a single due contract; it never starts a second `ShiftOrchestrator.StartShift` in the same morning. *(FD-Q1=A)*

---

## Morning lifecycle (Service S-D)

**BR-DAY-01 — Multiplayer no-op.** `OnDayStarted` does nothing in a multiplayer session. *(REL-U10-01, FR-MP-01)*

**BR-DAY-02 — Festival gate runs first.** If `CalendarHandlers.IsFestivalToday()`, the worker does not show up; no shift is started. *(DEV-U15-02, FR-DAY-01)*

**BR-DAY-03 — Festival: recurring.** On a festival day a recurring contract deducts **no** deposit, stays `Active`, and a **text-only** festival-notice letter is queued. *(DEV-U15-02, FR-DAY-01)*

**BR-CAL-03 — Festival: one-time.** A one-time contract whose scheduled day is a festival is **marked `Executed`**, its already-paid deposit is **refunded by mail**, and the festival letter carries that refund gold. *(Sub-decision from FD-Q3=A semantics under Clar-1a=C skip; surfaced for approval — alternative is to roll the contract to the next non-festival day.)*

**BR-DAY-04 — Config lock-in.** The day's deposit and refund use the `IConfigSnapshot` captured at `DayStarted`; later GMCM edits apply only from the next morning. *(FR-PAY-08)*

**BR-DAY-05 — Rain-aware rate.** Today's rate is computed via `RateCalculator` with `IsRainyToday()` as the rain flag, excluding the Water Crops surcharge on rainy days. *(FR-PAY-07, FD-Q4=B)*

**BR-DAY-06 — Water Crops not force-skipped on rain.** The Water Crops task remains in the enabled set on rainy days; only its surcharge is removed from the rate. *(DEV-U15-05, FD-Q4=B)*

**BR-DAY-07 — Deposit deducted at 6am.** A recurring contract's daily deposit is deducted from player gold at `DayStarted` before the shift starts. *(FR-PAY-03)*

---

## Affordability (FR-PAY-04)

**BR-AFF-01 — Cannot afford → skip + notice.** If player gold < today's deposit, no deposit is deducted, no shift starts, and a text-only cannot-afford notice is queued. *(FR-PAY-04, FD-Q5=A, S-12)*

**BR-AFF-02 — Stay active and retry daily.** A contract skipped for affordability stays `Active` and is re-evaluated every morning; the worker resumes automatically the first affordable morning. *(FD-Q5=A)*

**BR-AFF-03 — Notice each unaffordable day.** The cannot-afford notice is sent on **every** unaffordable morning; there is no de-duplication. *(FD-Q5=A)*

---

## Empty zone (FR-PAY-06)

**BR-EMP-01 — Deduct, run, mail full refund.** An empty-zone day still deducts the deposit at 6am and spawns the worker; finding no tasks, the worker exits and the full deposit is refunded **by mail** next morning (net zero, one-day lag). No pre-scan/skip in the scheduler. *(FD-Q6=C, FD-Q9=C, FR-PAY-06)*

---

## Sleep fast-forward (Service S-C, FR-DAY-02)

**BR-FF-01 — CalendarHandlers owns the save hook.** `CalendarHandlers.OnSavingHook` (subscribed to `GameLoop.Saving`) drives the fast-forward by calling `ShiftOrchestrator.FastForwardAndSettle()`. `ShiftOrchestrator` does not subscribe to `Saving` directly. *(FD-Q7=A)*

**BR-FF-02 — Ordering / atomicity.** The fast-forward and full settlement (deposit to chests, refund mail, overflow mail) complete **before** the contract segment is persisted and before the day rolls over. *(FD-Q7=A, FR-DAY-02)*

**BR-FF-03 — Mid-work: time-budgeted headless completion.** When the worker is still working (`ShiftEndTime` unset), remaining detected tasks are performed headlessly (no walk/animation) in normal order, each charging its estimated in-game-minutes against the window from the current time to the 8pm cap. Work stops at window exhaustion or task exhaustion; unfinished work is left undone. *(FD-Q2=A, FR-DAY-02)*

**BR-FF-04 — Already-finished: U-14 interruption path.** When the worker has finished working (`ShiftEndTime` set, Depositing/Exiting), all still-buffered items are mailed (reason `NotDelivered`) with no shipping-bin dump, exactly as U-14 BR-INT-01 — except the refund is now mailed (BR-REF-01). *(FD-Q5/U-14 carryover, DEV-U15-04)*

**BR-FF-05 — Deposits use real game state.** Headless completion still deposits into the **live** chests/bin and respects chest-full / chest-missing fallbacks (U-14 BR-OUT-06/07); conservation (NFR-SAFE-01) holds. *(FR-DAY-02, NFR-SAFE-01)*

---

## Missing tools (DEV-U15-03)

**BR-TOOL-01 — Missing → lowest tier.** `ToolLevelReader` reports a tool the player does not own at the lowest (basic) tier; the worker performs the task with the basic tool. No skip for a missing tool. *(FD-Q8=C, Clar-2a)*

**BR-TOOL-02 — Owned-tool tiers still gate.** Capability for tools the player owns is unchanged: lower-tier owned tools still cannot perform higher-tier-only actions (e.g., basic pickaxe cannot break boulders/meteorites), and fruit trees remain always-skip. Only the missing-tool branch changes. *(Clar-2b, FR-SKIP-03)*

**BR-TOOL-03 — No tool-missing warning.** `ShiftContext.ToolMissingWarnings` and `MailDispatcher.QueueToolMissingWarning` are removed; no tool-missing letter is ever sent. *(Clar-2c; removes FR-TOOL-03 warning clause and U-14 BR-MAIL-05)*

---

## Mailed settlement & refunds (DEV-U15-04)

**BR-REF-01 — Refunds are mailed.** Every refund (normal exit, fast-forward, empty-zone, one-time-festival) is delivered as gold-bearing mail the next morning, not credited at exit. The integer-clamped formula `deposit − (hoursWorked × rate)` is unchanged; deposit-run walking is still unbilled. *(FD-Q9=C, FR-PAY-05 deviated, NFR-SAFE-02)*

**BR-REF-02 — Deposit still leaves immediately.** The deposit is deducted from gold at confirmation (one-time) or 6am (recurring); only the refund is delayed by one day. *(FR-PAY-03)*

**BR-REF-03 — One settlement letter per shift.** A shift queues at most one settlement letter carrying any overflow items (with U-14 reason-line body) and any refund gold. With neither overflow nor a positive refund, no letter is sent. *(DEV-U15-04, S-11 single-letter spirit)*

**BR-REF-04 — Money-attachment fallback.** If MFM cannot attach money, the refund is sent as a text-only "here's your change" letter that credits the gold when collected (still next morning). Items always use MFM's multi-attachment path. *(Clar-3=A)*

**BR-REF-05 — IntentApplyRefund routes to mail.** The state machine's refund intent no longer mutates `player gold`; it supplies the refund amount to the settlement step. *(DEV-U15-04)*

---

## Mail (general)

**BR-MAIL-01 — Sender + no fee.** All farmhand mail is from "Your farmhand" (`mail.sender`), charges nothing, and applies no rate adjustment. *(S-11, NFR-UX-02, NFR-SAFE-02)*

**BR-MAIL-02 — i18n-routed bodies.** Every user-visible mail/log string routes through `I18nHelper`; new keys are added to `i18n/default.json` (see domain-entities.md). *(NFR-UX-02, S-20)*

**BR-MAIL-03 — Queued for tomorrow, platform-persisted.** All U-15 letters are queued for next-morning delivery and rely on Stardew/MFM persistence; Dayswork keeps no custom mail save data. *(DEV-U14-03 carryover)*

---

## Safety

**BR-SAFE-01 — No items lost across new branches.** Festival skip, can't-afford skip, empty-zone, and both fast-forward branches preserve conservation: every collected item is deposited or mailed; no refund or item is dropped on save/day-rollover. *(NFR-SAFE-01)*

**BR-SAFE-02 — Tolerate absent data.** Missing weather/festival/contract data degrades gracefully (treated as non-festival / non-rain / no contract), never a crash. *(NFR-SAFE-03)*

---

## PBT obligations (Property-Based Testing extension — enabled)

U-15 is largely SMAPI-event-driven (play-tested), but any predicates reducible to Core helpers carry PBT/unit coverage:

| Rule | Property / test |
|---|---|
| BR-REF-01 | Refund formula remains `clamp(deposit − hoursWorked × rate, 0, deposit)` for all generated (deposit, hours, rate) — reuses U-05 RefundCalculator invariants. |
| BR-FF-03 | Time-budgeted completion never charges more in-game-minutes than the remaining window; tasks completed ≤ tasks available. |
| BR-CTR-01 | Hiring guard: given an existing Active/Paused contract, a second create is rejected (unit test). |
| BR-SAFE-01 | Conservation across the fast-forward path: {deposited} ∪ {mailed} == {collected} (extends U-14 PBT). |

Security Baseline extension is **disabled** project-wide (no network/PII/auth surface); all its rules are N/A for U-15.
