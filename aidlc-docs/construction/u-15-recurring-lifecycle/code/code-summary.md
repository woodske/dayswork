# U-15 — Recurring Lifecycle + Calendar Handlers: Code Summary

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers
**Stage**: CONSTRUCTION — Code Generation (Part 2 complete)
**Approval**: APPROVED 2026-05-22
**Stories**: S-12 (completes), S-14 (full), S-15 (full), S-19 (PBT)
**Decisions**: FD-Q1=A, Q2=A superseded by DEV-U15-09 sleep-stop behavior, Q3→Clar-1a=C, Q4=B, Q5=A, Q6=C, Q7=A, Q8=C(+Clar-2), Q9=C(+Clar-3). Patterns Q–U. Deviations DEV-U15-01..09.

## Verification
- `dotnet build Dayswork.sln /p:EnableModDeploy=false`: **0 errors / 0 warnings**.
- `dotnet test Dayswork.sln`: **194 passed / 1 expected skip**. The removed fast-forward budget tests were replaced by sleep-stop refund examples; no regressions.
- `dotnet build Dayswork.sln`: **0 errors / 0 warnings**, auto-deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.
- In-game verification: pending user playtest (checklist below).

## Playtest fix
- **Festival no-worker mail timing**: playtest showed the festival courtesy letter arrived the day after the festival. `MailDispatcher` now has explicit delivery timing: settlement/refund/overflow letters remain next-morning, while morning no-worker notices (`QueueFestivalNotice` and `QueueCannotAffordNotice`) register as same-day MFM letters and are inserted into today's mailbox.
- **One-time festival refund**: the refund still uses the MFM credit-on-collection callback, but it rides the same-day festival notice instead of waiting until the following morning.
- **Recurring deposit inflation**: playtest showed recurring contracts could become unaffordable with impossible deposits (example: 36,755,660g). Root cause was U-15 day-start using raw `HoursEstimator` over saved zones, while the hire preview currently uses a flat 1.0-hour estimate; building placeholder zones `(0,0)..(999,999)` made the raw estimate explode. `DepositHoursPolicy` now centralizes the current flat estimate and is used by both hire preview and recurring scheduling.
- **Empty attachment slots**: MFM letters with no item attachments now register without a `dynamicItems` provider, so text-only/refund-only mail does not display an empty item slot. Settlement mail is suppressed if all requested attachments fail to create and there is no refund gold.
- **Sleep stops the worker**: playtest showed the sleep fast-forward path was not reliable enough for v1. `GameLoop.Saving` now stops the worker immediately: remaining tasks are not run headlessly, collected-but-undelivered items are mailed, any unearned refund is mailed, and the worker is cleared before day-rollover. The new sleep-stop refund tests also exposed and fixed `ShiftContext.ComputeRefund()` using HHMM values as if they were raw minutes.

## Files created
- `Dayswork/Orchestration/CalendarHandlers.cs` (M-14) — `IsFestivalToday()` / `IsRainyToday()` (fail-safe) + `OnSavingHook` → `ShiftOrchestrator.StopForSleepAndSettle()`. *(Pattern Q/S)*
- `Dayswork.Core/Pricing/DepositHoursPolicy.cs` — shared current deposit-hours policy (`FlatPreviewHours = 1.0`) used by hire preview and recurring day-start. *(DEV-U15-07)*
- `Dayswork.Tests/Shifts/ShiftContextTests.cs` — example coverage for sleep-stop refund math.
- `Dayswork.Tests/Pricing/DepositHoursPolicyTests.cs` — regression/PBT coverage for placeholder building zones and zone-shape independence.

## Files modified
- `Dayswork/Orchestration/RecurringContractScheduler.cs` (M-13) — full daily guard chain (Pattern R): festival gate (skip + courtesy/refund letter), config lock, rain-aware rate, shared preview-hours deposit, affordability gate (cannot-afford mail, stay Active, retry), deduct + `StartShift`. Now injected with `CalendarHandlers`, pricing calculators, config, `MailDispatcher`.
- `Dayswork/Orchestration/ShiftOrchestrator.cs` (M-12) — `StartShift(contract, dayDeposit, dayRate)` (empty-zone → mailed full refund); `OnSaving` replaced by `StopForSleepAndSettle()`; sleep stops the worker without running remaining tasks, then mails collected-but-undelivered items plus refund; refund routed through `SettleShiftMail` (one settlement letter, gold mailed); tool-missing population + `BuildWorkList` warning bookkeeping removed (tier gate kept).
- `Dayswork.Core/Shifts/ShiftContext.cs` — removed `ToolMissingWarnings`.
- `Dayswork/Integration/IMailDispatcher.cs` + `MailDispatcher.cs` (M-16) — `QueueSettlement(items, reasons, refundGold)`, `QueueCannotAffordNotice`, `QueueFestivalNotice`; removed `QueueOverflowMail`/`QueueToolMissingWarning`/`TaskDisplayName`; MFM-unavailable fallback deposits items to bin and credits gold directly. Playtest fixes: explicit `DeliveryTiming` keeps settlements next-morning and makes morning skip notices same-day; empty settlement letters are suppressed.
- `Dayswork/Integration/MailFramework/MailFrameworkModApiAdapter.cs` — `RegisterLetter(..., earliestDeliveryDay, moneyReward)`; read/close callback credits the refund on collection (Clar-3=A fallback) and prunes the letter; no-attachment letters pass no `dynamicItems` provider.
- `Dayswork/UI/HiringFlowCoordinator.cs` — single-active-contract hire guard (DEV-U15-01).
- `Dayswork/UI/SummaryMenu.cs` — uses `DepositHoursPolicy` for the displayed hours/deposit, keeping preview and recurring day-start aligned.
- `Dayswork/ModEntry.cs` — construct `CalendarHandlers`; inject scheduler deps; `Saving` rewired to `CalendarHandlers.OnSavingHook` **then** `ContractPersistenceAdapter.OnSaving` (drops `orchestrator.OnSaving`).
- `Dayswork/i18n/default.json` — added `mail.settlement.refund_line`, `mail.cannot_afford.body`, `mail.festival.body`, `mail.festival.refund_body`, `log.festival.skipped`, `ui.error.one_contract`; removed `mail.warning.tool_missing`.

## Removed (DEV-U15-03)
`ShiftContext.ToolMissingWarnings`, `MailDispatcher.QueueToolMissingWarning` + `TaskDisplayName`, `mail.warning.tool_missing`, the `BuildWorkList` `capSkippedKinds`/`anyItemForKind`/BR-TOOL-02 loop, `ShiftOrchestrator.OnSaving`'s direct `Saving` subscription, and the Step 20-superseded `FastForwardBudget` helper/tests. **Kept**: `DetectTask` tier gate (out-of-tier targets still skipped — Clar-2b); `ToolLevelReader` already maps missing→Basic (Clar-2a — unchanged).

## Extension compliance
| Rule | Status | Rationale |
|---|---|---|
| PBT-02 | N/A | No new round-trip-serialized type (mail/refund ride the platform queue) |
| PBT-03 | Compliant | Existing `RefundCalculator` invariants remain; Step 20 adds example-based `ShiftContextTests` because the sleep-stop path is SMAPI event orchestration, not a new pure property |
| PBT-07 | Compliant (reuse) | Reuses existing generators; no new shared generator needed |
| PBT-08 | Compliant | New properties use FsCheck.Xunit default seed + shrunk-input logging |
| PBT-09 | N/A | Framework established in U-02 |
| Security Baseline | N/A | Extension disabled (Q28) |

## Play-test checklist (in-game)
- (a) Recurring contract deducts the deposit at 6am over multiple days.
- (a2) Recurring contract with selected buildings/large zones keeps the daily deposit near the hire-preview amount (rain may lower the Water Crops surcharge); it must not jump to millions.
- (b) Can't-afford morning → no worker + same-day cannot-afford letter; resumes automatically when affordable; letter each unaffordable day.
- (c) Festival day → no worker + same-day festival letter (recurring: no charge, stays Active; one-time: Executed + mailed refund).
- (d) Rainy day → no Water Crops surcharge in the deposit; worker still shows for other tasks.
- (e) Empty zone → mailed full refund next morning (net zero).
- (f) Refund arrives by mail next morning (gold credited when the letter is read).
- (g) Sleep mid-work → worker stops immediately; remaining world work is left undone; collected-but-undelivered items + refund are mailed before day-rollover.
- (h) Sleep after work done → undelivered items + refund mailed.
- (i) Missing tool → worker uses basic tier, no warning mail.
- (j) Out-of-tier target (basic pickaxe vs boulder) silently skipped, no warning.
- (k) Exactly one settlement letter per shift; none when no overflow and zero refund.
- (k2) Text-only/refund-only mail shows no empty item attachment slot.
- (l) Second hire attempt while a contract exists → blocked with the one-contract HUD message.
- (m) U-10..U-14 scenarios regress clean.
