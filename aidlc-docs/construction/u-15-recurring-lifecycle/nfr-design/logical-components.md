# U-15 — Logical Components

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers

U-15 adds one Mod component (`CalendarHandlers`), promotes the scheduler to the full daily lifecycle, adds an at-save settlement method to the orchestrator, changes `ToolLevelReader`'s missing-tool semantics, and extends `MailDispatcher` for mailed refunds — while removing the tool-missing warning path. The worker behavioural loop and the deposit planner are reused unchanged.

## Component Map

```
SMAPI Events
    │
    ├─ DayStarted ─► RecurringContractScheduler.OnDayStarted   ← Pattern R (guard chain), promoted from stub
    │     ├─ MultiplayerGuard → no-op in MP
    │     ├─ CalendarHandlers.IsFestivalToday()?  ── yes ─► skip + queue festival letter (Pattern U)
    │     │        recurring: no deposit, stay Active │ one-time: Executed + mailed refund
    │     ├─ snapshot IConfigSnapshot                  (FR-PAY-08 config lock)
    │     ├─ RateCalculator(rain = CalendarHandlers.IsRainyToday())   ← Pattern Q (rate only; task kept)
    │     ├─ HoursEstimator → DepositCalculator
    │     ├─ gold < deposit?  ── yes ─► QueueCannotAffordNotice (Pattern U); stay Active; return
    │     └─ deduct gold ─► ShiftOrchestrator.StartShift(contract)
    │              └─ ToolLevelReader.ReadCurrent(): missing tool → lowest tier   ← Pattern T
    │                       (CapabilityEvaluator unchanged; no ToolMissingWarnings)
    │
    ├─ UpdateTicked / TimeChanged ─► ShiftOrchestrator   (worker loop + deposit seam unchanged: L–P)
    │     └─ [Exiting] IntentApplyRefund ─► settlement step (mail refund, Pattern U) — NOT gold += refund
    │
    └─ Saving ─►  CalendarHandlers.OnSavingHook            ← Pattern S (FIRST: fast-forward + settle)
    │                └─ ShiftOrchestrator.FastForwardAndSettle():
    │                      (a) mid-work  → time-budgeted headless tasks → deposit (M/N) → mail refund (U)
    │                      (b) finished  → buffer → Overflow(NotDelivered); mail refund (U)
    └─►          ContractPersistenceAdapter.OnSaving        ← THEN: persist contracts (ordered, REL-U15-02)
```

---

## Component Responsibilities

### CalendarHandlers *(Mod — M-14, new)*
- `IsFestivalToday()` / `IsRainyToday()` — O(1) live-state predicates, safe-default + log on failure (Pattern Q).
- `OnSavingHook(sender, args)` — subscribed to `GameLoop.Saving`; calls `ShiftOrchestrator.FastForwardAndSettle()` ahead of persistence (Pattern S).
- Lives in `Dayswork/Orchestration/`. No Harmony patches. *(MAINT-U15-01/03)*

### RecurringContractScheduler *(Mod — M-13, promoted from stub)*
- `OnDayStarted` is the full guard chain (Pattern R): festival gate → config lock → rain-rate → estimate/deposit → affordability gate → deduct/start. Single-active-contract invariant assumed (DEV-U15-01). *(BR-DAY-*, BR-AFF-*, BR-CTR-02)*

### ShiftOrchestrator *(Mod — M-12, extended)*
- **Removes** the direct `Saving` subscription; **adds** `FastForwardAndSettle()` (Pattern S, branches a/b).
- `IntentApplyRefund` handling routes the refund to the settlement step (mailed, Pattern U) instead of `Game1.player.Money += refund`.
- Reuses `DepositPlanner`/multi-trip loop (M/N) and the whole worker loop unchanged. *(BR-FF-*, BR-REF-05)*

### ToolLevelReader *(Mod — M-19, changed semantics)*
- `ReadCurrent()` reports a **missing** tool at the lowest tier (Pattern T). Owned-tool tiers unchanged. *(BR-TOOL-01)*

### MailDispatcher *(Mod — M-16, extended + trimmed)*
- **Extended**: settlement letter carries overflow items **and** refund gold in one letter (`QueueSettlement(items, reasons, refundGold)` — generalizes U-14's `QueueOverflowMail`); new `QueueCannotAffordNotice(...)` and `QueueFestivalNotice(..., refundGold)` (Pattern U). Money-attachment fallback to text-only credit-on-collection (REL-U15-04).
- **Removed**: `QueueToolMissingWarning(...)` (Pattern T / DEV-U15-03).
- Reads all text from `I18nHelper`; reuses the MFM adapter (Pattern P). *(BR-REF-03/04, BR-MAIL-*)*

### CapabilityEvaluator / RateCalculator / HoursEstimator / DepositCalculator / RefundCalculator *(Core — reused unchanged)*
- Pure pricing reused as-is; `RateCalculator`'s existing rain flag is now fed by `IsRainyToday()`. `RefundCalculator` math unchanged (delivery changed, not amount). *(SAFE-U15-02)*

### ShiftContext *(Core, trimmed)*
- `ToolMissingWarnings` **removed** (Pattern T). Other fields (`DepositAmount`, `ComputeRefund()`, `Overflow`, deposit plan, `ShiftEndTime`) unchanged.

### ModEntry *(Mod — M-01, composition root)*
- Constructs `CalendarHandlers`; rewires `GameLoop.Saving` to **`CalendarHandlers.OnSavingHook` then `ContractPersistenceAdapter.OnSaving`** (drops `orchestrator.OnSaving`); injects `CalendarHandlers` into the scheduler/orchestrator. *(Pattern S, REL-U15-02)*

### i18n/default.json
- New keys: `mail.settlement.refund_line`, `mail.cannot_afford.body`, `mail.festival.body`, `mail.festival.refund_body`, `log.festival.skipped`. Reuses `mail.sender`, `mail.overflow.*`. Removes `mail.warning.tool_missing` usage. *(UX-U15-01)*

### Reused unchanged
- `DepositPlanner` (C-11), `ChestResolver` (M-20), `WorkerMovementDriver`/`FarmhandWorker`/`WorkerRenderer`/`ToolSwapAnimator` (U-13B), `StuckDetector`/`TaskPriorityOrderer` (Core), `ItemBuffer` (C-10), `ContractStore`/`SaveDataSerializer` (Core), `MultiplayerGuard`.

### Removed
- `MailDispatcher.QueueToolMissingWarning`, `ShiftContext.ToolMissingWarnings`, U-14 BR-MAIL-05 (tool-missing letter), and `ShiftOrchestrator`'s direct `Saving` subscription. *(DEV-U15-03, Pattern S/T)*

---

## Extension Compliance Summary

| Rule | Status | Rationale |
|---|---|---|
| PBT-02 (blocking) | N/A | No new round-trip-serialized type (mail/refund ride the platform queue) |
| PBT-03 (blocking) | Compliant | Refund-formula reuse (U-05) + fast-forward time-budget/conservation properties (PBT-U15-01/02/03) |
| PBT-07 (blocking) | Compliant (reuse) | Reuses existing generators; single-active-contract guard is a unit test (PBT-U15-04) |
| PBT-08 (blocking) | Compliant | New properties follow the U-02 seed + shrunk-input logging convention (PBT-U15-05) |
| PBT-09 (blocking) | N/A | Framework established in U-02; no change |
| PBT-01/04/05/06/10 | Advisory | No action required |
| Security Baseline | N/A | Extension disabled (Q28) — no network/PII/auth/external-input surface |
