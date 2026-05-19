# U-05 Pricing Core — Business Rules

**Unit**: U-05 — Pricing Core
**Scope**: Hard rules and invariants governing the four pricing calculators

---

## Naming convention

Rules are grouped by calculator. Each rule ID is `BR-PRICE-NN`. Rules marked **[INVARIANT]** must hold for all valid inputs and are enforced by PBT-03 property tests (the full set of PBT obligations is listed at the end).

---

## RateCalculator rules

| ID | Rule | Source |
|---|---|---|
| BR-PRICE-01 | `rate = config.BaseRate + Σ(config.TaskIncrements[t] for t in enabledTasks where not excluded)` — exactly this formula, no other adjustments | FR-PAY-01 |
| BR-PRICE-02 | `rate >= config.BaseRate` always **[INVARIANT]** — each enabled task increments monotonically, and BaseRate is the floor | FR-PAY-01 |
| BR-PRICE-03 | When `isRaining = true` and `WaterCrops` is in `enabledTasks`, the `WaterCrops` increment is excluded from the sum | FR-PAY-07 |
| BR-PRICE-04 | When `isRaining = true`, all tasks other than `WaterCrops` are unaffected | FR-PAY-07 (by exclusion) |
| BR-PRICE-05 | When `isRaining = false`, all enabled tasks contribute their increment regardless of task type | FR-PAY-07 |
| BR-PRICE-06 | `rate(tasks, config, isRaining: true) == rate(tasks - {WaterCrops}, config, isRaining: false)` **[INVARIANT]** — rain exclusion is equivalent to removing WaterCrops from the set | FR-PAY-07 |
| BR-PRICE-07 | `rate` is independent of the iteration order of `enabledTasks` **[INVARIANT]** — addition is commutative | derived |

---

## HoursEstimator rules

| ID | Rule | Source |
|---|---|---|
| BR-HOURS-01 | `totalTiles = Σ(zone.Width * zone.Height)` for all zones — raw rectangle area, no terrain filtering | Q6:A |
| BR-HOURS-02 | `estimatedHours = (totalTiles * numEnabledTasks * config.AverageSpeedConstant) / 60.0` | Q1:D |
| BR-HOURS-03 | `estimatedHours >= 0.0` always **[INVARIANT]** — all inputs are non-negative | derived |
| BR-HOURS-04 | `estimatedHours` is monotonically non-decreasing in `totalTiles` holding other inputs fixed **[INVARIANT]** | derived from linear formula |
| BR-HOURS-05 | `estimatedHours` is monotonically non-decreasing in `numEnabledTasks` holding other inputs fixed **[INVARIANT]** | derived |
| BR-HOURS-06 | `estimatedHours = 0.0` when `zones` is empty OR `numEnabledTasks = 0` | derived |
| BR-HOURS-07 | Unit of `config.AverageSpeedConstant`: **real minutes per tile per task** — finalized from U-03's placeholder of "in-game minutes per actionable tile" (Q2:B). Default value subject to calibration in U-05 Code Generation. | Q2:B |

---

## DepositCalculator rules

| ID | Rule | Source |
|---|---|---|
| BR-DEP-01 | When `estimatedHours <= 0.0`, return `DepositResult.Zero` — degenerate contract (no zones or no tasks) | Q5 lower:B |
| BR-DEP-02 | When `estimatedHours > 0.0`, `amount = (int)Math.Ceiling(rate * estimatedHours)` | Q3:B |
| BR-DEP-03 | `amount >= 0` always when `Positive` **[INVARIANT]** — rate >= 0, estimatedHours > 0 | derived |
| BR-DEP-04 | `amount >= Math.Floor(rate * estimatedHours)` when `Positive` **[INVARIANT]** — ceiling is always >= floor | derived from Ceiling |
| BR-DEP-05 | `DepositResult.Positive.Amount` is the gold deducted at confirmation (one-time) or at 6am (recurring) | FR-PAY-03 |
| BR-DEP-06 | The `DepositResult.Zero` case does NOT block the recurring contract lifecycle — it signals the absence of billable work; the shift proceeds with a guaranteed full refund | FR-PAY-06 |

---

## RefundCalculator rules

| ID | Rule | Source |
|---|---|---|
| BR-REF-01 | `billable = (int)Math.Ceiling(rate * hoursWorked)` | Q3:B |
| BR-REF-02 | `refund = Math.Clamp(deposit - billable, 0, deposit)` | Q5 upper:A, NFR-SAFE-02 |
| BR-REF-03 | `0 <= refund <= deposit` always **[INVARIANT]** — the clamp guarantees both bounds | NFR-SAFE-02 |
| BR-REF-04 | `deposit - refund <= billable` always **[INVARIANT]** — the net charge never exceeds what was billed | derived |
| BR-REF-05 | When `hoursWorked = 0.0`, `refund = deposit` (full refund — empty zone day, FR-PAY-06) **[INVARIANT]** | FR-PAY-06 |
| BR-REF-06 | `hoursWorked` must exclude deposit-run time (walking to chests); that exclusion is enforced by the caller (`ShiftOrchestrator`, U-10) — RefundCalculator is unaware | FR-PAY-05 |
| BR-REF-07 | The rate passed to `RefundCalculator` must be the rate in effect when the shift **began** (locked at spawn), not the current GMCM value | FR-PAY-08 |
| BR-REF-08 | Refund is added directly to player gold at worker exit, not via mail or inventory | spec §Deposit |

---

## Cross-calculator invariants

| ID | Rule | Source |
|---|---|---|
| BR-XPRICE-01 | `deposit - refund <= Math.Ceiling(rate * hoursWorked)` — the net charge never exceeds the exact billable amount; rounding adds at most 1g of excess charge, which the clamp absorbs | NFR-SAFE-02 |
| BR-XPRICE-02 | No gold is ever created: `refund <= deposit` (BR-REF-03) and deposit was deducted from the player before the shift | NFR-SAFE-02 |
| BR-XPRICE-03 | On rain days: the `rate` passed to `DepositCalculator` and `RefundCalculator` must be the rain-adjusted rate (Water Crops excluded) — the rate is recalculated each morning for recurring contracts | FR-PAY-07 |

---

## PBT obligations summary

These are the property-based test obligations that Code Generation must satisfy for U-05 (PBT-03 enforcement).

| Property | Rule IDs | Calculator |
|---|---|---|
| `rate(emptyTasks, config, *) == config.BaseRate` | BR-PRICE-02 | RateCalculator |
| `rate(tasks, config, false) == config.BaseRate + Σ increments` | BR-PRICE-01 | RateCalculator |
| `rate(tasks, config, true) == rate(tasks - {WaterCrops}, config, false)` | BR-PRICE-06 | RateCalculator |
| `rate is order-independent on enabledTasks` | BR-PRICE-07 | RateCalculator |
| `estimatedHours >= 0` for all valid inputs | BR-HOURS-03 | HoursEstimator |
| `estimatedHours` non-decreasing in totalTiles | BR-HOURS-04 | HoursEstimator |
| `estimatedHours` non-decreasing in numEnabledTasks | BR-HOURS-05 | HoursEstimator |
| `deposit >= 0` for all Positive results | BR-DEP-03 | DepositCalculator |
| `deposit >= floor(rate * estimatedHours)` for all Positive results | BR-DEP-04 | DepositCalculator |
| `0 <= refund <= deposit` | BR-REF-03 | RefundCalculator |
| `deposit - refund <= billable` | BR-REF-04 | RefundCalculator |
| `refund == deposit` when `hoursWorked == 0.0` | BR-REF-05 | RefundCalculator |

**FsCheck generators needed** (PBT-07): A `PricingGen` module in `Dayswork.Tests/Pricing/Generators/PricingGen.cs` should provide:
- `ValidRate()` — `int` in `[config.BaseRate, config.BaseRate + Σall increments]`
- `ValidHours()` — `double` in `(0.0, double.MaxValue)` (excludes zero and negative)
- `ValidDeposit()` — `int` in `[0, int.MaxValue]`
- `ValidHoursWorked(estimatedHours)` — `double` in `[0.0, estimatedHours]`

These compose with `ConfigSnapshotGen.Snapshot()` from U-03 and `ZoneGen.Zone()` from U-04.

---

## Rain-day rate recalculation note (recurring contracts)

For recurring contracts (U-15), the rate is recalculated fresh each morning:

```
1. At 6am: check IsFestivalToday() — skip if festival (FR-DAY-01)
2. Check IsRainyToday()
3. Compute rate = RateCalculator.Calculate(contract.EnabledTasks, config, isRaining)
4. Compute deposit = DepositCalculator.Calculate(HoursEstimator.Estimate(...), rate)
5. Deduct deposit from player gold (or send cannot-afford mail per FR-PAY-04)
6. Spawn worker with rate locked at step 3's result
```

The `rate` locked at spawn is passed to `RefundCalculator` at shift end regardless of any GMCM changes made during the day (BR-REF-07, FR-PAY-08).
