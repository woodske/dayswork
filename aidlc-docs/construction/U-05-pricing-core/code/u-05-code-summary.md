# U-05 Code Summary — Pricing Core

## Files created

### Production (Dayswork.Core)

| File | Type | Purpose |
|---|---|---|
| `Dayswork.Core/Pricing/DepositResult.cs` | abstract record + 2 sealed records | `PositiveDeposit(int Amount)` / `ZeroDeposit.Instance` — discriminated union forcing explicit handling of zero-deposit case |
| `Dayswork.Core/Pricing/IRateCalculator.cs` | interface | `Calculate(tasks, config, isRaining) : int` |
| `Dayswork.Core/Pricing/RateCalculator.cs` | sealed class | `BaseRate + Σ increments`, skipping WaterCrops when `isRaining` |
| `Dayswork.Core/Pricing/IHoursEstimator.cs` | interface | `Estimate(zones, numEnabledTasks, config) : double` |
| `Dayswork.Core/Pricing/HoursEstimator.cs` | sealed class | `(Σ zone area × numTasks × AverageSpeedConstant) / 60.0` |
| `Dayswork.Core/Pricing/IDepositCalculator.cs` | interface | `Calculate(estimatedHours, rate) : DepositResult` |
| `Dayswork.Core/Pricing/DepositCalculator.cs` | sealed class | `Math.Ceiling(rate × hours)` → `PositiveDeposit`; guard returns `ZeroDeposit` |
| `Dayswork.Core/Pricing/IRefundCalculator.cs` | interface | `Calculate(deposit, hoursWorked, rate) : int` |
| `Dayswork.Core/Pricing/RefundCalculator.cs` | sealed class | `Math.Clamp(deposit - Math.Ceiling(rate × hoursWorked), 0, deposit)` |

### Modified (Dayswork.Core)

| File | Change |
|---|---|
| `Dayswork.Core/Config/ConfigDefaults.cs` | `AverageSpeedConstant` updated from placeholder `5.0` to calibrated `0.3` (pricing-min per raw tile per task). Produces ~285g deposit for a 300-tile 2-task contract at 95g/hr. |

### Tests (Dayswork.Tests)

| File | Tests | Type |
|---|---|---|
| `Dayswork.Tests/Generators/PricingGen.cs` | — | FsCheck arbitraries: `TaskSubset`, `ValidRate`, `ValidEstimatedHours`, `ValidDeposit`, `ValidHoursWorked` |
| `Dayswork.Tests/Pricing/RateCalculatorTests.cs` | 7 Fact + 4 Property | PBT-03: monotonicity, sum identity, rain equivalence, order independence |
| `Dayswork.Tests/Pricing/HoursEstimatorTests.cs` | 4 Fact + 3 Property | PBT-03: non-negativity, monotone in tiles, monotone in tasks |
| `Dayswork.Tests/Pricing/DepositCalculatorTests.cs` | 5 Fact + 2 Property | PBT-03: non-negativity, ceiling lower-bound |
| `Dayswork.Tests/Pricing/RefundCalculatorTests.cs` | 5 Fact + 3 Property | PBT-03: [0,deposit] range, full refund on zero hours, net-charge bound |

## Test results

```
Total tests: 71
     Passed: 70
    Skipped:  1  (PBT-08 demo — expected)
      Failed:  0
```

New U-05 tests: **33** (21 Fact + 12 Property, each Property at MaxTest=1000)
Prior tests (U-02/03/04): all 37 still passing — no regressions.

## PBT compliance

| Rule | Status | Details |
|---|---|---|
| PBT-02 | N/A | No serialization in U-05 |
| PBT-03 | Compliant | 12 properties across 4 calculators; 1000 inputs each; all pass |
| PBT-07 | Compliant | `PricingGen` in `Dayswork.Tests/Generators/`; 5 arbitraries composing with `ConfigSnapshotGen` (U-03) and `ZoneGen` (U-04) |
| PBT-08 | Compliant | Inherited from U-02 seed-logging wiring |
| PBT-09 | Compliant | FsCheck.Xunit 2.16.5 — no new packages needed |

## NFR-MAINT-03 verification

`dotnet build Dayswork.Core` succeeded with 0 errors. All 9 files in `Dayswork.Core/Pricing/` contain only `Dayswork.Core.*` and `System` namespaces — no SMAPI, StardewValley, or Harmony references.

## Build deviation notes

Test files originally used C# 12 collection expression syntax (`[]`, `[x]`). Corrected to C# 10 compatible syntax (`Array.Empty<T>()`, `new[] { x }`) during build verification. FsCheck's `Prop.ForAll` max arity is 3 arbitraries + 1 lambda; the 4-arbitrary test was refactored to combine two inputs into a tuple generator.

## What U-06 inherits

- `IRateCalculator`, `IHoursEstimator`, `IDepositCalculator`, `IRefundCalculator` interfaces available for injection in U-09 and U-10 composition roots
- `DepositResult` discriminated union available for switch-expressions in `SummaryMenu` (U-09) and `RecurringContractScheduler` (U-10)
- `AverageSpeedConstant` default finalized at `0.3`; GMCM exposure deferred to U-16
