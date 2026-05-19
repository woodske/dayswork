# U-05 Pricing Core — Business Logic Model

**Unit**: U-05 — Pricing Core
**Scope**: Algorithms for RateCalculator, HoursEstimator, DepositCalculator, RefundCalculator

---

## Overview

All four calculators are **pure functions**: given the same inputs they always return the same output. They hold no state, mutate nothing, and have no side effects. This makes them directly testable with FsCheck property-based tests (PBT-03).

The four calculators compose sequentially at hire time (Rate → Hours → Deposit) and the RefundCalculator fires independently at shift end.

---

## RateCalculator

**Purpose**: Compute the hourly gold rate for a given task selection.

**Algorithm**:

```
function Calculate(enabledTasks, config, isRaining):
    rate = config.BaseRate          // always charged; minimum possible rate

    for each task in enabledTasks:
        if isRaining AND task == WaterCrops:
            continue                // FR-PAY-07: water task excluded on rain days
        rate = rate + config.TaskIncrements[task]

    return rate
```

**Key properties**:
- Rate is monotonically non-decreasing relative to `BaseRate` — each additional task can only add, never subtract
- Rain exclusion is scoped exactly to `WaterCrops`; all other tasks are unaffected by `isRaining`
- Iteration order over `enabledTasks` does not affect the result (addition is commutative)
- `config.TaskIncrements[task]` lookup is guaranteed safe by INV-CFG-03 (enforced in `ConfigDefaults.Build()`)

**Return type**: `int` (gold per real-time hour)

---

## HoursEstimator

**Purpose**: Estimate the real-time hours the worker will need to complete the contract.

**Algorithm**:

```
function Estimate(zones, numEnabledTasks, config):
    totalTiles = 0
    for each zone in zones:
        totalTiles = totalTiles + (zone.Width * zone.Height)   // raw rectangle area (Q6:A)

    estimatedHours = (totalTiles * numEnabledTasks * config.AverageSpeedConstant) / 60.0

    return estimatedHours   // may be 0.0 if zones is empty or numEnabledTasks is 0
```

**Unit of `AverageSpeedConstant`**: real minutes per tile per task (finalized from U-03 placeholder, Q2:B). The formula converts to hours by dividing by 60.

**Scaling behavior**:
- Doubling the zone area doubles the estimate (linear in tiles)
- Adding one more task increases the estimate proportionally (linear in task count)
- The speed constant is the single calibration lever; the player can tune it via GMCM in U-16

**"Pessimistic" by design**: Using raw zone area (not actionable tiles) means the estimate typically overshoots. The refund mechanism (FR-PAY-05) corrects for this — the player pays based on actual hours worked, not the overestimate. The deposit is a ceiling, not a precise bill.

**Return type**: `double` (real-time hours; may be 0.0)

---

## DepositCalculator

**Purpose**: Convert estimated hours + rate into a gold deposit amount, detecting the degenerate zero case.

**Algorithm**:

```
function Calculate(estimatedHours, rate):
    if estimatedHours <= 0.0:
        return DepositResult.Zero       // degenerate contract (Q5 lower:B)

    rawDeposit = (double)rate * estimatedHours
    amount = (int)Math.Ceiling(rawDeposit)  // always round up (Q3:B, NFR-SAFE-02)
    return DepositResult.Positive(amount)
```

**Why `Math.Ceiling`**: Rounding up guarantees the mod never charges less than the mathematically exact billable amount. Combined with the refund clamping rule, the player never loses gold beyond `rate × hoursWorked`. The ceiling is a ceiling on the *deposit* (what the player pays upfront); the refund brings them back to a fair settlement.

**Zero-hours trigger**: If `zones` is empty or `numEnabledTasks` is 0, `HoursEstimator.Estimate` returns 0.0, and `DepositCalculator` returns `DepositResult.Zero`. Callers must handle this explicitly. The `SummaryMenu` (U-09) will surface a clear message; the `RecurringContractScheduler` (U-10) will skip the day without deducting gold.

**Return type**: `DepositResult` (discriminated union)

---

## RefundCalculator

**Purpose**: Compute the gold returned to the player at shift end based on how many hours the worker actually worked.

**Algorithm**:

```
function Calculate(deposit, hoursWorked, rate):
    billable = (int)Math.Ceiling((double)rate * hoursWorked)   // round up, same as deposit (Q3:B)
    refund = Math.Clamp(deposit - billable, 0, deposit)        // clamped to [0, deposit] (Q5 upper:A)
    return refund
```

**Why `Math.Ceiling` for billable**: Billing is also rounded up. This means for the same rate, `billable ≤ deposit` when `hoursWorked ≤ estimatedHours` (the normal case). Rounding both the same direction prevents gold creation from rounding artifacts.

**Why `Math.Clamp`**: In theory `hoursWorked ≤ estimatedHours` always holds (the worker stops when done or at 8pm). In practice, floating-point arithmetic in the estimate and actual timing could cause `billable > deposit` by 1g due to rounding. Clamping silently absorbs this without crashing or producing negative refunds (NFR-SAFE-02, Q5 upper:A).

**Full refund on empty-zone day** (FR-PAY-06): If the worker finds no actionable objects, `hoursWorked = 0.0`. Then `billable = Ceiling(rate × 0) = 0`, and `refund = Clamp(deposit - 0, 0, deposit) = deposit`. The player receives a full refund. This is distinct from `DepositResult.Zero` (which fires at hire time before the shift starts).

**Deposit-run exclusion** (FR-PAY-05): The `hoursWorked` value passed in must not include time the worker spent walking to chests after shift completion. The `ShiftOrchestrator` (U-10) owns this exclusion — RefundCalculator itself has no knowledge of what activities were included.

**Return type**: `int` (gold refund; always in `[0, deposit]`)

---

## Composition example (hire flow)

```
// Player chooses: HarvestCrops + ClearWeeds, 1 zone (50x30 tiles), not raining
config = ConfigDefaults.Build()     // BaseRate=50, HarvestCrops=+25, ClearWeeds=+20, AverageSpeedConstant=X

rate    = RateCalculator.Calculate([HarvestCrops, ClearWeeds], config, isRaining: false)
        = 50 + 25 + 20 = 95 g/hr

hours   = HoursEstimator.Estimate([zone(50,30)], numEnabledTasks: 2, config)
        = (1500 * 2 * X) / 60.0
        // calibrated X determines deposit magnitude; see Code Generation for default

deposit = DepositCalculator.Calculate(hours, rate)
        = DepositResult.Positive( Math.Ceiling(95 * hours) )

// ... shift runs ... worker finishes at 6.2 real hours out of 8.0 estimated

refund  = RefundCalculator.Calculate(deposit.Amount, hoursWorked: 6.2, rate: 95)
        = Clamp(deposit.Amount - Ceiling(95 * 6.2), 0, deposit.Amount)
        = Clamp(deposit.Amount - 589, 0, deposit.Amount)
```
