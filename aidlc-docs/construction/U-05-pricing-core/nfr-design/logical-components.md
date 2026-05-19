# U-05 Pricing Core — Logical Components

**Unit**: U-05 — Pricing Core

---

## Infrastructure component assessment

| Component type | Applicable? | Rationale |
|---|---|---|
| Message queue | No | No async or inter-process communication |
| Cache / memoization | No | Calculators are called once per hire-flow or once per morning; result is not reused |
| Circuit breaker | No | No external dependencies that can fail |
| Rate limiter | No | Not a service; no external callers |
| Retry logic | No | Pure functions; either succeed or throw on invalid input |
| Event bus / pub-sub | No | No events emitted; results returned synchronously |
| Connection pool | No | No I/O |

**Conclusion**: No infrastructure components needed. All NFR design is expressed through code-level patterns (Ceiling-Clamp, Pure Function Isolation, Discriminated Union) — no runtime components.

---

## Logical component map

The four calculators and their single supporting type form the entire logical surface of U-05:

```
Dayswork.Core/Pricing/
+---------------------------+     +---------------------------+
|      IRateCalculator      |     |     IHoursEstimator       |
|---------------------------|     |---------------------------|
| Calculate(                |     | Estimate(                 |
|   tasks, config, isRaining|     |   zones, numTasks, config |
| ) : int                   |     | ) : double                |
+---------------------------+     +---------------------------+
           |                                 |
           +----------+    +----------------+
                      |    |
                      v    v
             +---------------------------+
             |    IDepositCalculator     |
             |---------------------------|
             | Calculate(                |
             |   estimatedHours, rate    |
             | ) : DepositResult         |
             +---------------------------+
                         |
           [at shift end]|
                         v
             +---------------------------+
             |    IRefundCalculator      |
             |---------------------------|
             | Calculate(                |
             |   deposit, hoursWorked,   |
             |   rate                    |
             | ) : int                   |
             +---------------------------+

Supporting type:
             +---------------------------+
             |       DepositResult       |
             |---------------------------|
             | abstract record           |
             |   Positive(int Amount)    |
             |   Zero                    |
             +---------------------------+
```

---

## Dependency injection wiring (planned for U-09/U-10 ModEntry)

The four calculators are stateless and created once at mod startup in `ModEntry.Entry()`. They are constructor-injected into consumers:

| Consumer | Receives | Unit where wired |
|---|---|---|
| `HiringFlowCoordinator` (M-03) | `IRateCalculator`, `IHoursEstimator`, `IDepositCalculator` | U-09 |
| `SummaryMenu` (M-07) | `IRateCalculator`, `IHoursEstimator`, `IDepositCalculator` | U-09 |
| `RecurringContractScheduler` (M-13) | `IRateCalculator`, `IHoursEstimator`, `IDepositCalculator` | U-10 |
| `ShiftOrchestrator` (M-12) | `IRefundCalculator` | U-10 |

All four calculators are pure and thread-safe (no mutable state), so they can be shared as singletons across all consumers without any synchronization.

---

## Test component map

```
Dayswork.Tests/Pricing/
+----------------------------------+
| PricingGen.cs (Generators/)      |
|----------------------------------|
| ValidRate()     : Arbitrary<int> |
| ValidHours()    : Arbitrary<double> |
| ValidDeposit()  : Arbitrary<int> |
| ValidHoursWorked(max)            |
|              : Arbitrary<double> |
+----------------------------------+
        |
        | composes with
        v
+------------------+  +-----------+  +-----------+
| ConfigSnapshotGen|  | ZoneGen   |  | xUnit     |
| (U-03)           |  | (U-04)    |  | [Property]|
+------------------+  +-----------+  +-----------+
        |                  |               |
        +------------------+---------------+
                           |
                           v
        +------------------------------------------+
        | RateCalculatorTests.cs                   |
        | HoursEstimatorTests.cs                   |
        | DepositCalculatorTests.cs                |
        | RefundCalculatorTests.cs                 |
        |------------------------------------------|
        | [Fact] tests for spec'd edge cases       |
        | [Property] tests for PBT-03 invariants   |
        +------------------------------------------+
```

`PricingGen` follows the same pattern as `ConfigSnapshotGen` (U-03) and `ZoneGen` (U-04): a static class in `Dayswork.Tests/Pricing/Generators/` exporting `Arbitrary<T>` methods, usable via `Prop.ForAll(PricingGen.ValidRate(), ...)` in `[Property]` tests.
