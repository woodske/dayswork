# U-05 Pricing Core — Domain Entities

**Unit**: U-05 — Pricing Core
**Scope**: Types introduced by and consumed in the Pricing layer

---

## New types introduced by U-05

### `DepositResult` (abstract record hierarchy)

**File**: `Dayswork.Core/Pricing/DepositResult.cs`
**Purpose**: Discriminated union returned by `IDepositCalculator.Calculate()`. Distinguishes a zero-estimated-hours condition (degenerate contract — no zones or no tasks) from a normal positive deposit.

```csharp
namespace Dayswork.Core.Pricing;

public abstract record DepositResult
{
    public sealed record Positive(int Amount) : DepositResult;
    public sealed record Zero : DepositResult;
}
```

**Design rationale** (Q5 lower:B): Returning `0` silently would allow a degenerate hire-flow to proceed without the caller knowing it has no actionable work. The discriminated union forces callers (U-09 `SummaryMenu`, U-10 `RecurringContractScheduler`) to handle the zero case explicitly — either blocking the confirmation or logging a warning.

**Relationship to FR-PAY-06**: `DepositResult.Zero` fires at *estimate time* when no tiles are in scope. FR-PAY-06's "full refund on empty zone day" fires at *shift end* when the worker actually finds no actionable objects — that case is handled by `RefundCalculator` returning `refund = deposit`, not by this type.

---

## Interfaces introduced by U-05

### `IRateCalculator`

**File**: `Dayswork.Core/Pricing/IRateCalculator.cs`

```csharp
namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public interface IRateCalculator
{
    int Calculate(IEnumerable<TaskKind> enabledTasks, IConfigSnapshot config, bool isRaining);
}
```

**Inputs**:
- `enabledTasks` — the tasks the player toggled on in Screen 1
- `config` — provides `BaseRate` and `TaskIncrements`
- `isRaining` — when `true`, Water Crops increment is excluded even if `WaterCrops` is in `enabledTasks` (FR-PAY-07, Q4:B)

**Output**: hourly rate as `int` gold per real-time hour

---

### `IHoursEstimator`

**File**: `Dayswork.Core/Pricing/IHoursEstimator.cs`

```csharp
namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public interface IHoursEstimator
{
    double Estimate(IEnumerable<Zone> zones, int numEnabledTasks, IConfigSnapshot config);
}
```

**Inputs**:
- `zones` — the player's drawn zones; tile area is summed as `Σ(zone.Width × zone.Height)` using raw rectangle area (Q6:A)
- `numEnabledTasks` — count of enabled tasks (passed pre-computed; avoids re-enumerating tasks)
- `config` — provides `AverageSpeedConstant` (real minutes per tile per task, Q2:B)

**Output**: estimated real-time hours as `double`

---

### `IDepositCalculator`

**File**: `Dayswork.Core/Pricing/IDepositCalculator.cs`

```csharp
namespace Dayswork.Core.Pricing;

public interface IDepositCalculator
{
    DepositResult Calculate(double estimatedHours, int rate);
}
```

**Output**: `DepositResult.Positive(int amount)` for normal cases, `DepositResult.Zero` when `estimatedHours <= 0`

---

### `IRefundCalculator`

**File**: `Dayswork.Core/Pricing/IRefundCalculator.cs`

```csharp
namespace Dayswork.Core.Pricing;

public interface IRefundCalculator
{
    int Calculate(int deposit, double hoursWorked, int rate);
}
```

**Output**: refund amount in gold, clamped to `[0, deposit]`

---

## Implementation classes

Each interface has a single stateless `sealed class` implementation.

| Interface | Implementation |
|---|---|
| `IRateCalculator` | `RateCalculator` |
| `IHoursEstimator` | `HoursEstimator` |
| `IDepositCalculator` | `DepositCalculator` |
| `IRefundCalculator` | `RefundCalculator` |

All four implementations: no static state, no `Game1` references, no SMAPI references (NFR-MAINT-03).

---

## Types consumed from prior units

| Type | Defined in | Role in U-05 |
|---|---|---|
| `TaskKind` (enum) | U-03 `Dayswork.Core/Domain/TaskKind.cs` | Identifies which tasks are enabled; key into `IConfigSnapshot.TaskIncrements` |
| `IConfigSnapshot` | U-03 `Dayswork.Core/Config/IConfigSnapshot.cs` | Source of `BaseRate`, `TaskIncrements`, `AverageSpeedConstant` |
| `Zone` (record) | U-04 `Dayswork.Core/Domain/Zone.cs` | `HoursEstimator` uses `Zone.Width × Zone.Height` for raw tile area |

**Note on `AverageSpeedConstant` units**: U-03's business rules noted "in-game minutes per actionable tile" as a placeholder. U-05's design finalizes the unit as **real minutes per tile per task** (Q2:B). The placeholder default of `5.0` in `ConfigDefaults.Build()` is subject to calibration during U-05 Code Generation — the field meaning changes but the field itself does not need to be renamed or restructured.

---

## Data flow diagram

```
Player inputs
    enabledTasks : TaskKind[]
    zones        : Zone[]
    config       : IConfigSnapshot
    isRaining    : bool

                        +-------------------+
                        | HoursEstimator    |
zones, numTasks, config | Estimate()        | --> estimatedHours : double
        +-------------->+-------------------+
        |
        |               +-------------------+
        |               | RateCalculator    |
enabledTasks, config    | Calculate()       | --> rate : int
isRaining  +----------->+-------------------+
        |
        |               +-------------------+
        +-------------> | DepositCalculator |
estimatedHours, rate    | Calculate()       | --> DepositResult
        +-------------->+-------------------+
                                 |
              [at shift end]     v
                        +-------------------+
                        | RefundCalculator  |
 deposit, hoursWorked   | Calculate()       | --> refund : int
 rate       +---------->+-------------------+
```

---

## Directory layout produced by U-05

```text
Dayswork.Core/
└── Pricing/
    ├── DepositResult.cs          <- new type (this unit)
    ├── IRateCalculator.cs        <- interface (this unit)
    ├── RateCalculator.cs         <- implementation (this unit)
    ├── IHoursEstimator.cs        <- interface (this unit)
    ├── HoursEstimator.cs         <- implementation (this unit)
    ├── IDepositCalculator.cs     <- interface (this unit)
    ├── DepositCalculator.cs      <- implementation (this unit)
    ├── IRefundCalculator.cs      <- interface (this unit)
    └── RefundCalculator.cs       <- implementation (this unit)

Dayswork.Tests/
└── Pricing/
    ├── Generators/
    │   └── PricingGen.cs         <- FsCheck arbitraries for PBT (PBT-07)
    ├── RateCalculatorTests.cs
    ├── HoursEstimatorTests.cs
    ├── DepositCalculatorTests.cs
    └── RefundCalculatorTests.cs
```
