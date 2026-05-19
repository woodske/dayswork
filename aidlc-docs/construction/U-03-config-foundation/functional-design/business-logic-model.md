# U-03 Config Foundation — Business Logic Model

**Unit**: U-03 Config Foundation
**Scope**: Configuration data model and lifecycle for all downstream Core components

---

## Lifecycle

```text
┌────────────────────┐     ┌───────────────────────┐     ┌────────────────────────────┐
│ Game launch        │ ──▶ │ ConfigDefaults        │ ──▶ │ IConfigSnapshot            │
│ (ModEntry, U-16)   │     │ .Build()              │     │ (immutable instance)       │
└────────────────────┘     └───────────────────────┘     └──────────────┬─────────────┘
                                                                         │
                                                                         ▼
                            ┌───────────────────────────────────────────────────────┐
                            │ Consumers (read-only)                                  │
                            │  • U-05 IRateCalculator, IDepositCalculator,          │
                            │        IRefundCalculator, IHoursEstimator             │
                            │  • U-10 IShiftStateMachine (HardCapTime)              │
                            │  • U-13 IStuckDetector (Stuck*WaitMinutes)            │
                            │  • U-16 IGMCMRegistrar (reflects every property)      │
                            └───────────────────────────────────────────────────────┘
```

The snapshot is constructed once at game launch and never mutated for the lifetime of the play session — except as described in *GMCM edit semantics* below.

---

## GMCM edit semantics (satisfies FR-PAY-08)

> **FR-PAY-08**: Config-driven rate changes (via GMCM) for active recurring contracts apply starting the next morning. The current day's deposit and refund are at the rate in effect when that day began.

**Mechanism** (final shape lands in U-16, but the U-03 contract enables it):

1. U-16's `GMCMRegistrar` holds a writable backing object (`Config` POCO) parallel to `IConfigSnapshot`.
2. On GMCM "Save" click, `GMCMRegistrar` validates the writable POCO against the INV-CFG-* invariants, then builds a fresh `IConfigSnapshot` from it and stores the new instance in a module-level field.
3. **Active shifts continue with the snapshot they captured against** — because `IConfigSnapshot` is immutable, existing references see old values until they next read the module-level field.
4. The U-15 `RecurringContractScheduler` reads the module-level field at 6am DayStarted, capturing whatever snapshot is current. This is the "applies starting the next morning" point.

**No locking required** because:
- All SMAPI events fire on the main thread (no concurrent reads).
- Immutability eliminates any "what if GMCM saved mid-shift" race condition by construction — captured references are stable.

---

## Data flow per shift day

| Time | Component | Read from snapshot |
|---|---|---|
| 6am DayStarted | U-15 `RecurringContractScheduler` | Capture current `IConfigSnapshot` for the day |
| 6am Deposit | U-05 `IRateCalculator.Calculate(enabledTasks, snapshot)` | `BaseRate`, `TaskIncrements` |
| 6am Deposit | U-05 `IDepositCalculator.Calculate(snapshot, hoursEstimated)` | (rate from previous step) |
| 6am Deposit | U-05 `IHoursEstimator.Estimate(zoneTileCount, taskCount, snapshot)` | `AverageSpeedConstant` |
| Shift loop | U-10 `IShiftStateMachine.Step(...)` | `HardCapTime` (8pm cap) |
| Shift loop | U-13 `IStuckDetector.Tick(...)` | `StuckInitialWaitMinutes`, `StuckPostTeleportWaitMinutes` |
| Shift end | U-05 `IRefundCalculator.Calculate(deposit, hoursWorked, rate)` | (rate already captured) |

The snapshot is **never mutated** by shift execution. The morning-after refresh is the only mutation point in the whole system, and it happens before any of the day's calculations run.

---

## Why immutable?

- **Determinism**: shift state machine and pricing become pure functions of `(snapshot, inputs)`. This is what makes U-05's PBT-03 invariants (rate-table consistency) and U-10's PBT-03 state-machine properties tractable — every call with the same inputs produces the same output.
- **Concurrency robustness**: even though SMAPI is single-threaded today, immutability removes the "mutated mid-shift" failure mode by construction.
- **Testability**: tests construct alternative snapshots with the record `with` expression — e.g., `ConfigDefaults.Build() with { BaseRate = 99 }` — without touching the production factory.

---

## Why an interface (IConfigSnapshot) AND a record (ConfigSnapshot)?

- **Consumers depend on the interface**: every U-05+ component takes `IConfigSnapshot` in its constructor. This decouples them from the concrete record.
- **Tests construct minimal fakes**: tests that exercise only one or two config fields can build a dictionary-backed `IConfigSnapshot` fake without instantiating the full record.
- **GMCM can swap implementations**: if U-16 ever needs a "live-editing" snapshot wrapper (e.g., to support GMCM real-time preview), the consumers don't change — the interface absorbs the variation.

---

## Out of scope for U-03

- Persistence to/from `config.json` — that's SMAPI helper plumbing, lands in U-16 `GMCMRegistrar`.
- GMCM UI registration — U-16.
- Per-task rate-table extensions (e.g., conditional bonuses) — explicitly not in v1.
- Live reload / file-watching — explicitly not in v1.
