# U-14 — Logical Components

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail

U-14 adds the deposit-planning and mail components and extends the deposit/exit/save seam of the orchestrator. The worker behavioural loop (movement, render, stuck, invuln, tool visuals) is reused unchanged.

## Component Map

```
SMAPI Events
    │
    ├─ DayStarted ─► RecurringContractScheduler (unchanged) ─► ShiftOrchestrator.StartShift()
    │                    └─ ShiftContext now carries TaskDestinations (from Contract)        ← changed
    │
    ├─ UpdateTicked (EVERY tick) ─► WorkerMovementDriver.Update()            (unchanged)
    │
    ├─ UpdateTicked (÷4) ─► ShiftOrchestrator
    │     ├─ [Working] task action ─► InvokeTaskAction + CollectNewDebris
    │     │        └─ ItemBuffer.Add(itemId, qty, _pendingTask)             ← Pattern L (tagged)
    │     ├─ work list empty / stuck-end ─► BeginDeposit()
    │     │        └─ DepositPlanner.Plan(buffer, TaskDestinations,         ← Pattern M (C-11, pure)
    │     │                 shippingBinTile, workerStart, Manhattan)
    │     │             → DepositPlan { Trips, PreMailedOverflow }
    │     │        └─ seed ShiftContext.Overflow ← PreMailedOverflow
    │     └─ [Depositing] trip queue (Pattern N):
    │             ├─ [IntentDepositAtChest]      ← NEW intent
    │             │     └─ ChestResolver.ResolveChest(ref):
    │             │           null → Overflow(ChestMissing)
    │             │           partial → deposit fit + Overflow(ChestFull)
    │             │           full → deposit all
    │             ├─ [IntentDepositInShippingBin] → deposit all (no overflow)
    │             └─ queue empty → Transition(Exiting)
    │
    ├─ TimeChanged (8pm) ─► ShiftOrchestrator (unchanged: → Depositing; trips still complete)
    │
    ├─ [Exiting] ─► refund at entrance (unchanged) + flush mail (Pattern O):
    │        ├─ Overflow non-empty → MailDispatcher.QueueOverflowMail(items, reasons)   ← M-16 / MFM
    │        └─ ToolMissingWarnings non-empty → MailDispatcher.QueueToolMissingWarning(tasks) ← vanilla
    │
    └─ Saving (mid-deposit) ─► ShiftOrchestrator.OnSaving:
             └─ remaining buffer → Overflow(NotDelivered); flush mail; refund   ← Pattern O (no bin dump)
```

---

## Component Responsibilities

### DepositPlanner *(Core — C-11, new)*
- Pure `Plan(snapshot, taskDestinations, shippingBinTile, workerStart, distance) → DepositPlan`.
- Resolves task→destination (absent→mail), groups+consolidates by destination, nearest-neighbor orders via the injected Manhattan oracle (Pattern M). Zero Stardew refs (MAINT-U14-01). PBT target (PBT-U14-01..05).

### MailDispatcher *(Mod — M-16, new)*
- `IMailDispatcher`: `QueueOverflowMail(items, reasons)` (MFM multi-attachment, deliver-tomorrow) and `QueueToolMissingWarning(tasks)` (vanilla, deliver-tomorrow). Reads all text from `I18nHelper`. Holds the vendored MFM API acquired via `GetApi`. Null-API → log+continue (Pattern P / REL-U14-05).

### ItemBuffer *(Core — C-10, extended)*
- `Add(itemId, qty, sourceTask)`; `Snapshot()`/`TakeAll()` return `IReadOnlyList<BufferedItem>` carrying `SourceTask` (Pattern L). Stays pure Core. *(Deviation: matrix listed C-10 as not-extended.)*

### ShiftStateMachine *(Core — C-08, extended)*
- No new phases. `IntentDepositAtChest(ChestRef)` added to the intent set. Multi-trip handled by re-issuing intents within Depositing via `SetIntent` (Pattern N / BR-SM-01). Existing transition table + PBT invariants unchanged.

### ShiftContext *(Core, extended)*
- New `TaskDestinations : IReadOnlyDictionary<TaskKind, DestinationKey>` (threaded from the contract at StartShift) and `Overflow : List<OverflowItem>` (the single undeliverable sink, Pattern O). `ToolMissingWarnings` (existing) now read at exit.

### ShiftOrchestrator *(Mod — M-12, extended)*
- `StartShift` threads `contract.TaskDestinations` into the context.
- `BeginDeposit` builds the `DepositPlan`, seeds Overflow, drives the multi-trip loop (Pattern N) using `ChestResolver`.
- Exit + `OnSaving` flush mail via `MailDispatcher` (Pattern O); the old shipping-bin force-dump on save is removed.
- All worker-loop behaviour (movement, stuck, invuln, tool visuals, throttle, refund math) retained (BR-PRESERVE-01).

### ModEntry *(Mod — M-01, composition root)*
- Constructs `DepositPlanner` + `MailDispatcher`; acquires the MFM API at startup; injects the Manhattan distance oracle into the deposit flow.

### manifest.json
- Adds MFM (DIGUS' Mail Framework Mod) as a required `Dependencies` entry (COMPAT-U14-01 / BR-DEP-01).

### i18n/default.json
- New keys: `mail.sender`, `mail.overflow.chest_full|chest_missing|no_chest_assigned|not_delivered`, `mail.warning.tool_missing` (+ task-name keys the warning body enumerates).

### Reused unchanged
- `ChestResolver` (M-20, from U-11 — `ResolveChest` is the chest-liveness oracle), `WorkerMovementDriver`/`FarmhandWorker`/`WorkerRenderer`/`ToolSwapAnimator` (U-13B), `StuckDetector`/`TaskPriorityOrderer`/`CapabilityEvaluator` (Core), `ConfigSnapshot`, `RefundCalculator`, `WorkItem`.

### Removed
- None (no files deleted; the shipping-bin force-dump *behaviour* on save is removed within `OnSaving`).

---

## Extension Compliance Summary

| Rule | Status | Rationale |
|---|---|---|
| PBT-02 (blocking) | N/A | FD-Q4=A introduces no new round-trip-serialized type |
| PBT-03 (blocking) | Compliant | `DepositPlanner` conservation / trip-count / no-empty-trip / resolution-totality properties (PBT-U14-01..04) |
| PBT-07 (blocking) | Compliant | New shared generator for `(BufferedItem list, TaskDestinations)` planner inputs (PBT-U14-05) |
| PBT-08 (blocking) | Compliant | New properties follow the U-02 seed + shrunk-input logging convention |
| PBT-09 (blocking) | N/A | Framework established in U-02; no change |
| PBT-01/04/05/06/10 | Advisory | No action required |
| Security Baseline | N/A | Extension disabled (Q28) — no network/PII/auth/external-input surface |
