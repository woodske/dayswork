# U-15 — Recurring Lifecycle + Calendar Handlers: Code Generation Plan

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers
**Stories**: S-12 (completes — daily deposit deduction + can't-afford → cannot-afford mail), S-14 (full — festival skip+letter, rainy-day Water-Crops rate exclusion, empty-zone full refund), S-15 (full — early-sleep atomic fast-forward), S-19 (PBT obligations)
**Phase**: CONSTRUCTION — Code Generation (Part 1: Planning)

> This plan is the single source of truth for U-15 Code Generation. Generation (Part 2) executes these steps in order **after approval**.

---

## Unit Context

**Components owned (new files)**: M-14 `CalendarHandlers` (Mod).
**Components extended**: M-13 `RecurringContractScheduler` (full daily lifecycle), M-12 `ShiftOrchestrator` (`FastForwardAndSettle` + mailed refund; drops direct `Saving` sub), M-19 `ToolLevelReader` (verify missing→Basic), M-16 `MailDispatcher`/`IMailDispatcher` (settlement+refund / cannot-afford / festival; remove tool-missing), `MailFrameworkModApiAdapter` (money attachment + fallback), M-01 `ModEntry` (construct/inject + reorder `Saving`), the hiring entry point (single-active-contract guard), `i18n/default.json`.
**Components trimmed (removed)**: `ShiftContext.ToolMissingWarnings`, `MailDispatcher.QueueToolMissingWarning` + `TaskDisplayName`, `mail.warning.tool_missing`, the `BuildWorkList` warning bookkeeping, the orchestrator's direct `Saving` subscription, U-14 BR-MAIL-05.
**Reused unchanged**: pure pricing (`RateCalculator` with its rain flag, `HoursEstimator`, `DepositCalculator`, `RefundCalculator`), `DepositPlanner`, `ChestResolver`, the whole worker loop + deposit seam (Patterns L–P), `CapabilityEvaluator`/tier gating (Clar-2b), `ContractStore`, `MultiplayerGuard`.
**Dependencies satisfied**: U-05 (pricing), U-06 (Contract/ContractStore), U-10 (scheduler stub, orchestrator, ToolLevelReader, ShiftContext), U-11 (ChestResolver), U-14 (DepositPlanner, MailDispatcher, MFM adapter, Overflow). No forward deps.

**Decisions baked in**: FD-Q1=A (one active contract), FD-Q2=A (time-budgeted fast-forward), FD-Q3→Clar-1a=C (festival skip + letter), FD-Q4=B (rain rate-only, keep task), FD-Q5=A (can't-afford retry+mail daily), FD-Q6=C (empty-zone deduct→run→mailed refund), FD-Q7=A (CalendarHandlers owns `Saving`), FD-Q8=C+Clar-2 (missing→Basic, keep tier gating, remove warning), FD-Q9=C+Clar-3 (mail all refunds, text-only fallback). Patterns Q–U from nfr-design-patterns.md. Deviations DEV-U15-01..05.

> **Pre-existing reality confirmed during planning:** `ToolLevelReader.FindLevel<T>` **already returns `ToolLevel.Basic` when the tool is absent** (a U-13 decision), so Clar-2a (missing→lowest tier) is already satisfied — Step 6 only *verifies* it. The current "tool-missing warning" actually fires on insufficient *tier* for a target (e.g., basic pickaxe vs. boulder); Clar-2c removes that warning while Clar-2b keeps the tier gate (`DetectTask` still returns null for an out-of-tier target).

> **Onboarding note (new SMAPI surface this unit):** festival detection (`Utility.isFestivalDay` / equivalent) and weather flags (`Game1.IsRainingHere` / `Game1.isRaining`) — exact members confirmed at generation; MFM **money** attachment on a letter (confirmed against installed MFM 1.20.0, as `RegisterLetter` was in U-14). Explained at the relevant steps.

---

## Code Location
- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Core**: `Dayswork.Core\` · **Mod**: `Dayswork\` · **Tests**: `Dayswork.Tests\` (references Core only)
- **Docs**: `aidlc-docs\construction\u-15-recurring-lifecycle\code\`

---

## Steps

### A. Calendar predicates (Pattern Q)

**Step 1 — Create `CalendarHandlers` (M-14)**
[ ] Create `Dayswork/Orchestration/CalendarHandlers.cs`. Public `bool IsFestivalToday()` and `bool IsRainyToday()` — O(1) live-state lookups (festival-day check; farm weather flag), each wrapped to return the safe default (false) + log on failure (REL-U15-01 / SAFE-U15-04). Public `void OnSavingHook(object? sender, SavingEventArgs e)` that calls `ShiftOrchestrator.FastForwardAndSettle()` (wired in Steps 5/9). Confirm exact festival/weather APIs at generation. *S-14/S-15; Pattern Q; BR-CAL, BR-DAY-05.*

### B. Recurring lifecycle (Pattern R)

**Step 2 — Promote `RecurringContractScheduler.OnDayStarted` to the full guard chain**
[ ] Modify `Dayswork/Orchestration/RecurringContractScheduler.cs`. Inject `CalendarHandlers`, the pricing calculators (`RateCalculator`, `HoursEstimator`, `DepositCalculator`), a live `IConfigSnapshot` accessor, and `MailDispatcher` (constructor params). For the day's due contract, run the ordered chain (fail-safe, none aborts the day):
- MP guard (existing) → no-op in MP.
- **Festival gate** (`IsFestivalToday()`): skip the shift. Recurring → no deposit, stay `Active`, `QueueFestivalNotice(contract, refundGold:0)`. One-time → mark `Executed`, `QueueFestivalNotice(contract, refundGold: contract.DepositAmount)` (mailed refund, BR-CAL-03). Return.
- **Config lock** → snapshot live `IConfigSnapshot` (BR-DAY-04 / FR-PAY-08).
- **Rain-aware rate** → `RateCalculator` with `IsRainyToday()` (surcharge excluded; Water Crops task kept — DEV-U15-05).
- **Estimate + deposit** → `HoursEstimator` → `DepositCalculator` using the locked config + today's rate (recompute daily; rain varies day-to-day).
- **Affordability gate** (`Game1.player.Money < deposit`): `QueueCannotAffordNotice(contract, shortfall)`, skip, stay `Active`, retry next day (mail each unaffordable day — FD-Q5=A). Return.
- **Deduct + start** → `Game1.player.Money -= deposit`; `ShiftOrchestrator.StartShift(contract, todaysDeposit, todaysRate)`.

Keep existing one-time handling (mark `Executed` before `StartShift`); the festival gate is the only new pre-empt. *S-12/S-14; Pattern R; BR-DAY-*, BR-AFF-*, BR-EMP-01.*

**Step 3 — Single-active-contract hire guard (DEV-U15-01)**
[ ] In the hiring entry point (`BulletinBoardPatch` / `HiringFlowCoordinator` / `ContractListMenu` — exact spot confirmed at generation), refuse to begin a new hire when `ContractStore` already holds an `Active` or `Paused` contract (offer "Hire" only when none exists). *S-12; BR-CTR-01.*

### C. Orchestrator — deposit threading, fast-forward, mailed refund (Patterns S, U)

**Step 4 — Extend `StartShift` to carry today's deposit + rate**
[ ] Modify `ShiftOrchestrator.StartShift` to accept `todaysDeposit` and `todaysRate` and thread them into `ShiftContext` (so `ComputeRefund` is correct for recurring days where deposit/rate are recomputed). One-time callers pass `contract.DepositAmount` / `contract.HourlyRate`. *S-14; BR-DAY-04.*

**Step 5 — Replace `OnSaving` with `FastForwardAndSettle()` (Pattern S)**
[ ] In `ShiftOrchestrator`, remove the `OnSaving` `Saving` handler and add `public void FastForwardAndSettle()` (called by `CalendarHandlers.OnSavingHook`):
- No shift in flight → no-op.
- **(a) Mid-work** (`ShiftEndTime` unset): time-budgeted headless completion — iterate remaining work items, invoke each task action directly (reuse `InvokeTaskAction` + `CollectNewDebrisAtTile`, **skip navigation/animation**), charging each action's estimated in-game-minutes against the window `[timeOfDay, HardCapTime]`; stop at budget- or work-exhaustion. Then build+run the `DepositPlan` against live chests (reuse `BeginDeposit`/`HandleDeposit` logic), then settle (Step 6). Per-action minute estimate source confirmed at generation.
- **(b) Finished** (`ShiftEndTime` set): the U-14 interruption path — `AppendUndeliveredToOverflow()`; settle (Step 6). No bin dump.
- `ClearWorker()` / null the context at the end, as today. *S-15; Pattern S; BR-FF-01..05.*

**Step 6 — Mailed refund + single settlement letter (Pattern U)**
[ ] Route every refund through mail instead of `Game1.player.Money += refund`. Change the three credit sites — `HandleExit` (line ~625), and both former `OnSaving` branches (now inside `FastForwardAndSettle`) — to compute `refund = _ctx.ComputeRefund()` and hand it to the settlement flush. Rework `FlushShiftMail` into a single `QueueSettlement(items, reasons, refundGold)` call: overflow items (if any) **and** refund gold (if > 0) in one letter; no letter when both are zero/empty (BR-REF-03). The `IntentApplyRefund` handler no longer mutates gold (BR-REF-05). *S-14/S-15; Pattern U; BR-REF-01..05, DEV-U15-04.*

### D. Tool-missing warning removal (Pattern T)

**Step 7 — Remove the tool-missing warning path; keep tier gating**
[ ] Remove `ShiftContext.ToolMissingWarnings` (`Dayswork.Core/Shifts/ShiftContext.cs`). In `ShiftOrchestrator.BuildWorkList`, drop the `out toolMissingWarnings` param, `capSkippedKinds`/`anyItemForKind`, and the BR-TOOL-02 loop; drop the `_ctx.ToolMissingWarnings` population. Remove the tool-missing block from `FlushShiftMail`. **Keep** `DetectTask`'s capability gate (the `return null` on out-of-tier targets) and its skip logging — tier gating for owned tools is unchanged (Clar-2b). *S-09 reversal; Pattern T; BR-TOOL-02/03, DEV-U15-03.*

### E. Mail dispatcher (Pattern U)

**Step 8 — Extend `IMailDispatcher` / `MailDispatcher`; remove tool-missing**
[ ] In `Dayswork/Integration/IMailDispatcher.cs` + `MailDispatcher.cs`: generalize `QueueOverflowMail` into `QueueSettlement(IReadOnlyList<ItemStack> items, IReadOnlySet<OverflowReason> reasons, int refundGold)` — one letter carrying items (existing reason-line body) plus a refund line (`mail.settlement.refund_line`) and the gold; nothing sent when items empty and refundGold ≤ 0. Add `QueueCannotAffordNotice(Contract contract, int shortfall)` (text-only, body `mail.cannot_afford.body`) and `QueueFestivalNotice(Contract contract, int refundGold)` (text-only `mail.festival.body`, or `mail.festival.refund_body` + gold when refundGold > 0). **Remove** `QueueToolMissingWarning` + `TaskDisplayName`. All text via `I18nHelper`; reuse `mail.sender`. *S-12/S-14; Pattern U; BR-REF-03, BR-MAIL-*.*

**Step 9 — Money attachment + fallback in `MailFrameworkModApiAdapter`**
[ ] Extend `Dayswork/Integration/MailFramework/MailFrameworkModApiAdapter.cs` to attach **money** to a letter (confirm MFM 1.20.0 support against the installed DLL). If money attachment is unsupported, fallback to a text-only letter whose read/close callback credits `Game1.player.Money += refundGold` once (guarded so it credits exactly once — reuse the existing one-shot/`mailReceived` pattern). Items continue via the existing `dynamicItems` path. *Pattern U; REL-U15-04, BR-REF-04.*

### F. Composition root (Pattern S wiring)

**Step 10 — Modify `ModEntry`**
[ ] Construct `CalendarHandlers`; inject pricing calculators + `CalendarHandlers` + config accessor + `MailDispatcher` into `RecurringContractScheduler`; inject `CalendarHandlers` into wherever it must reach the orchestrator. Rewire `GameLoop.Saving`: **remove `orchestrator.OnSaving`**, add `calendarHandlers.OnSavingHook`, ordered so settlement runs **before** `persistAdapter.OnSaving` (REL-U15-02). *S-15; Pattern S.*

### G. i18n

**Step 11 — Modify `i18n/default.json`**
[ ] Add `mail.settlement.refund_line`, `mail.cannot_afford.body`, `mail.festival.body`, `mail.festival.refund_body`, `log.festival.skipped`. **Remove** `mail.warning.tool_missing`. Reuse `mail.sender` + `mail.overflow.*`. *UX-U15-01; NFR-UX-02.*

### H. Tests

**Step 12 — Tests + cleanup**
[ ] Add: refund-amount property reusing `RefundCalculator` invariants (PBT-U15-01); fast-forward time-budget + conservation properties against a small testable helper if the budget math is extracted to Core (PBT-U15-02/03); single-active-contract guard unit test (PBT-U15-04); all with U-02 seed logging (PBT-U15-05). Remove/update any tests referencing `ToolMissingWarnings` / `QueueToolMissingWarning`. *S-19; PBT-U15-01..05.*

### I. Build, test, docs, state

**Step 13 — `dotnet build Dayswork.sln`**
[ ] 0 errors / 0 warnings; mod auto-deploys to `Mods/Dayswork/`.

**Step 14 — `dotnet test Dayswork.sln`**
[ ] New U-15 tests green; full regression green (no `ToolMissingWarnings` references remain).

**Step 15 — Create `aidlc-docs/construction/u-15-recurring-lifecycle/code/code-summary.md`**
[ ] Files created/modified/removed; extension-compliance table; play-test checklist: (a) recurring contract deducts deposit at 6am over multiple days; (b) can't-afford morning → no worker + cannot-afford letter, retries when affordable; (c) festival day → no worker + festival letter (recurring no-charge; one-time Executed + mailed refund); (d) rainy day → no watering surcharge, worker still shows for other tasks; (e) empty zone → mailed full refund (net zero); (f) refund arrives by mail next morning (gold or fallback credit-on-collection); (g) sleep mid-work → shift fast-forwards, deposits, mails refund, atomically before day-rollover; (h) sleep after work done → undelivered items + refund mailed; (i) missing tool → worker uses basic tier, no warning mail; (j) out-of-tier target (basic pickaxe vs boulder) silently skipped, no warning; (k) only one settlement letter per shift; (l) U-10..U-14 scenarios regress clean.

**Step 16 — Update `aidlc-state.md` + `audit.md`**
[ ] Mark U-15 Code Generation complete; append audit entry.

---

## Story Traceability

| Story | Steps |
|---|---|
| S-12 daily deposit + can't-afford mail (completes) | 2, 3, 8, 11 |
| S-14 festival + rain + empty-zone (full) | 1, 2, 6, 8, 11 |
| S-15 early-sleep atomic fast-forward (full) | 1, 4, 5, 6, 10 |
| S-19 PBT obligations | 12 |

## Scope summary
**16 steps.** One new file (`CalendarHandlers.cs`). Extends the scheduler (full lifecycle), orchestrator (fast-forward + mailed refund), mail dispatcher + MFM adapter (settlement/cannot-afford/festival + money), ModEntry (`Saving` reorder), and the hiring guard. **Removes** the tool-missing warning path end-to-end (Core field + orchestrator bookkeeping + dispatcher method + i18n key). Pure pricing reused unchanged. Two engineering items resolved per NFR Design: headless fast-forward runs to completion in the save fade (no artificial cap), and MFM money attachment falls back to credit-on-collection. Play-test focus: festival/rain/affordability/empty-zone branches, mailed-refund timing, and the atomic sleep fast-forward.
