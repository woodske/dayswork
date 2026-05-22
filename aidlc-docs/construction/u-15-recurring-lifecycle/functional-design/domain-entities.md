# U-15 — Recurring Lifecycle + Calendar Handlers: Domain Entities

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3→Clarification-1a=C, FD-Q4=B, FD-Q5=A, FD-Q6=C, FD-Q7=A, FD-Q8=C (+Clar-2), FD-Q9=C (+Clar-3)

This file defines the data shapes U-15 introduces, extends, or removes. U-15 is mostly *behaviour* over existing types; the new types are small. SMAPI/Stardew classes (weather, festival, mail, money) are named only to anchor the model and live behind `CalendarHandlers` / `MailDispatcher`.

---

## Existing types reused (no change)

| Type | Role in U-15 |
|---|---|
| `Contract` | The unit of the daily lifecycle. `Schedule` (OneTime/Recurring), `Status` (Active/Paused/Executed), `Zones`, `EnabledTasks`, `TaskDestinations`. |
| `ContractStatus` (enum) | `Active` / `Paused` / `Executed`. The single-active-contract invariant (DEV-U15-01) constrains how many may be Active/Paused at once. |
| `IConfigSnapshot` | Snapshotted at `DayStarted` to lock today's rates (FR-PAY-08). |
| `RateCalculator` / `HoursEstimator` / `DepositCalculator` / `RefundCalculator` | Pure pricing; `RateCalculator` already accepts the rain flag U-15 supplies. |
| `ShiftContext` | Carries `DepositAmount`, `ComputeRefund()`, `Overflow`, the deposit plan, `ShiftEndTime`. |
| `ToolSnapshot` / `CapabilityMatrix` | Built per shift; U-15 changes only how a *missing* tool maps into `ToolSnapshot` (DEV-U15-03). |
| `OverflowItem` / `OverflowReason` / `ItemStack` | The U-14 overflow set, reused; the settlement letter (below) carries them. |

---

## New types

### `M-14 CalendarHandlers` (Mod orchestration component)

```
CalendarHandlers
  IsFestivalToday() : bool        // true on a festival calendar day
  IsRainyToday()    : bool        // true when today's weather is rain/storm
  OnSavingHook(sender, args)      // GameLoop.Saving handler: drives the sleep fast-forward, then yields to persistence
```

- `IsFestivalToday()` → consumed by the scheduler's festival gate (skip + letter, DEV-U15-02).
- `IsRainyToday()` → consumed by the scheduler's rate computation only (DEV-U15-05); it is **not** used to remove the Water Crops task.
- `OnSavingHook` → calls `ShiftOrchestrator.FastForwardAndSettle()` in a guaranteed order ahead of `ContractPersistenceAdapter.OnSaving` (FD-Q7=A).

The two predicates wrap live game state so the scheduler/orchestrator stay free of direct weather/festival lookups.

---

## Extended types

### `RecurringContractScheduler` (M-13 — promoted from stub)

The `OnDayStarted` body gains, per due contract: the festival gate, today's config lock, rain-aware rate, hours/deposit, the affordability check, and deduct-then-start. Shape of `OnDayStarted` is unchanged (still a `DayStarted` handler); the *single-active-contract* invariant (DEV-U15-01) means the loop processes ≤1 contract.

### `ShiftOrchestrator` (M-12)

| Member | Change |
|---|---|
| `OnSaving` (handler) | **Removed as a `Saving` subscriber** (FD-Q7=A). |
| `FastForwardAndSettle()` (new method) | Called by `CalendarHandlers.OnSavingHook`. Branch (a) time-budgeted headless fast-forward for mid-work; branch (b) the U-14 interruption path for already-finished work. Both end by mailing the refund (DEV-U15-04). |
| `IntentApplyRefund` handling | Changes meaning: instead of `player gold += refund`, the refund amount is routed to the settlement letter (mailed gold, DEV-U15-04). |

### `ToolLevelReader` (M-19, owned by U-10) — DEV-U15-03

| Member | Change |
|---|---|
| `ReadCurrent()` | A tool the player does **not** own is reported at the **lowest tier** (basic), not "absent / level 0". Owned tools keep their real tier. |

### `MailDispatcher` (M-16) — interface additions/removal

```
IMailDispatcher
  // U-14 (kept): item-bearing overflow letter. EXTENDED to optionally carry refund gold so a shift
  // sends at most ONE settlement letter (DEV-U15-04).
  QueueSettlement(items : IReadOnlyList<ItemStack>,
                  reasons : IReadOnlySet<OverflowReason>,
                  refundGold : int)            // 0 ⇒ no gold attached; items empty + refundGold 0 ⇒ no letter

  // NEW (FR-PAY-04, FD-Q5=A): text-only, no items, delivered next morning. One per unaffordable morning.
  QueueCannotAffordNotice(contract : Contract, shortfall : int)

  // NEW (DEV-U15-02): festival courtesy letter. Text-only for recurring; carries refundGold for a
  // refunded one-time contract.
  QueueFestivalNotice(contract : Contract, refundGold : int)   // 0 ⇒ text-only

  // REMOVED (DEV-U15-03): tool-missing warnings no longer occur.
  // QueueToolMissingWarning(...)   ← deleted
```

> Naming note: U-14's `QueueOverflowMail(items, reasons)` is generalized to `QueueSettlement(items, reasons, refundGold)`. If keeping the U-14 method name is preferred at code time, the added `refundGold` parameter is the only behavioural change; the consolidation rule (one settlement letter per shift) is what matters.

---

## Removed types / fields (DEV-U15-03)

| Removed | Where it lived | Why |
|---|---|---|
| `ShiftContext.ToolMissingWarnings` | Core shift state (U-13) | Missing tools no longer cause skips, so the set is never populated. |
| `MailDispatcher.QueueToolMissingWarning(...)` | M-16 (U-14) | No tool-missing warning is ever sent. |
| U-14 rule **BR-MAIL-05** (tool-missing letter) | U-14 business-rules | Superseded; no longer reachable. |

---

## Letter inventory after U-15

| Letter | Trigger | Carries | Delivery |
|---|---|---|---|
| **Settlement** | Shift end / fast-forward, when overflow items and/or refund > 0 | Overflow items (MFM attachments) + refund gold | Next morning, one per shift |
| **Cannot-afford notice** | Recurring deposit unaffordable at 6am | Text only | Next morning, each unaffordable day |
| **Festival notice** | Festival day (any contract) | Text only (recurring) / refund gold (one-time) | Next morning |

All from sender "Your farmhand" (`mail.sender`), no fee, i18n-routed.

---

## New i18n keys (added to `i18n/default.json`)

| Key | Use |
|---|---|
| `mail.settlement.refund_line` | Settlement body line stating the returned gold amount. |
| `mail.cannot_afford.body` | Cannot-afford notice body (names the contract / shortfall). |
| `mail.festival.body` | Festival courtesy body (recurring). |
| `mail.festival.refund_body` | Festival body when a one-time deposit is refunded. |
| `log.festival.skipped` | SMAPI log line when a contract is skipped for a festival. |

(Existing `mail.sender` and `mail.overflow.*` reason lines from U-14 are reused by the settlement letter.)
