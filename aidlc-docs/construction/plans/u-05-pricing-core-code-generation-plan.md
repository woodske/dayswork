# U-05 Pricing Core — Code Generation Plan

**Unit**: U-05 — Pricing Core
**Stage**: Code Generation
**Status**: Part 2 — executing

## Unit context

**Stories implemented**: Foundation for S-02 (live rate display), S-06 (estimate + deposit), S-14 (rain handling), S-19 (PBT-03 obligations)
**Dependencies**: U-03 (`IConfigSnapshot`, `TaskKind`), U-04 (`Zone`)
**Consumers (wired in later units)**: U-09 `SummaryMenu`, U-09 `HiringFlowCoordinator`, U-10 `RecurringContractScheduler`, U-10 `ShiftOrchestrator`

## Code locations

- **Production**: `C:\Users\kwood\Repos\dayswork\Dayswork.Core\Pricing\` (9 new files)
- **Config update**: `C:\Users\kwood\Repos\dayswork\Dayswork.Core\Config\ConfigDefaults.cs` (update `AverageSpeedConstant`)
- **Tests**: `C:\Users\kwood\Repos\dayswork\Dayswork.Tests\Pricing\` (4 new test files)
- **Generator**: `C:\Users\kwood\Repos\dayswork\Dayswork.Tests\Generators\PricingGen.cs` (1 new file, follows ZoneGen convention)
- **Summary doc**: `C:\Users\kwood\Repos\dayswork\aidlc-docs\construction\U-05-pricing-core\code\u-05-code-summary.md`

## AverageSpeedConstant calibration

**Formula**: `estimatedHours = (totalTiles × numEnabledTasks × AverageSpeedConstant) / 60`
**Unit**: pricing-minutes per raw tile per task (determines deposit scale)
**Default**: `0.3` (replaces U-03 placeholder of `5.0`)

Calibration examples at default 0.3:

| Scenario | Tiles | Tasks | Rate | Est. hours | Deposit |
|---|---|---|---|---|---|
| Small watering plot | 100 | 1 (WaterCrops, 70g/hr) | 70 | 0.50 hr | 35g |
| Medium crop zone | 300 | 2 (Water+Harvest, 95g/hr) | 95 | 3.00 hr | 285g |
| Large multi-task | 600 | 3 (Water+Harvest+Rocks, 115g/hr) | 115 | 9.00 hr | 1035g |

The constant is player-tunable via GMCM in U-16. Small single-task contracts are cheap; large multi-task contracts are proportionally more expensive.

---

## Steps

### Step 1 — `DepositResult.cs`
- [x] Create `Dayswork.Core/Pricing/DepositResult.cs`
- Abstract record `DepositResult`; sealed records `PositiveDeposit(int Amount)` and `ZeroDeposit` (with `Instance` singleton, mirroring `DestinationKey` pattern from U-04)
- Namespace: `Dayswork.Core.Pricing`

### Step 2 — `IRateCalculator.cs`
- [x] Create `Dayswork.Core/Pricing/IRateCalculator.cs`
- Method: `int Calculate(IEnumerable<TaskKind> enabledTasks, IConfigSnapshot config, bool isRaining)`
- Namespace: `Dayswork.Core.Pricing`; usings: `Dayswork.Core.Config`, `Dayswork.Core.Domain`

### Step 3 — `RateCalculator.cs`
- [x] Create `Dayswork.Core/Pricing/RateCalculator.cs`
- Sealed class implementing `IRateCalculator`
- Algorithm: start with `config.BaseRate`; for each task in `enabledTasks`, skip `WaterCrops` when `isRaining`, else add `config.TaskIncrements[task]`
- Implements BR-PRICE-01..07

### Step 4 — `IHoursEstimator.cs`
- [x] Create `Dayswork.Core/Pricing/IHoursEstimator.cs`
- Method: `double Estimate(IEnumerable<Zone> zones, int numEnabledTasks, IConfigSnapshot config)`
- Namespace: `Dayswork.Core.Pricing`; usings: `Dayswork.Core.Config`, `Dayswork.Core.Domain`

### Step 5 — `HoursEstimator.cs`
- [x] Create `Dayswork.Core/Pricing/HoursEstimator.cs`
- Sealed class implementing `IHoursEstimator`
- Algorithm: `totalTiles = Σ(zone.Width * zone.Height)`; `return (totalTiles * numEnabledTasks * config.AverageSpeedConstant) / 60.0`
- `Zone.Width` and `Zone.Height` are computed from `TopLeft`/`BottomRight` as `(BottomRight.X - TopLeft.X + 1)` and `(BottomRight.Y - TopLeft.Y + 1)` respectively
- Implements BR-HOURS-01..07

### Step 6 — `IDepositCalculator.cs`
- [x] Create `Dayswork.Core/Pricing/IDepositCalculator.cs`
- Method: `DepositResult Calculate(double estimatedHours, int rate)`

### Step 7 — `DepositCalculator.cs`
- [x] Create `Dayswork.Core/Pricing/DepositCalculator.cs`
- Sealed class implementing `IDepositCalculator`
- Algorithm: if `estimatedHours <= 0.0` return `ZeroDeposit.Instance`; else return `new PositiveDeposit((int)Math.Ceiling(rate * estimatedHours))`
- Implements BR-DEP-01..06

### Step 8 — `IRefundCalculator.cs`
- [x] Create `Dayswork.Core/Pricing/IRefundCalculator.cs`
- Method: `int Calculate(int deposit, double hoursWorked, int rate)`

### Step 9 — `RefundCalculator.cs`
- [x] Create `Dayswork.Core/Pricing/RefundCalculator.cs`
- Sealed class implementing `IRefundCalculator`
- Algorithm: `billable = (int)Math.Ceiling(rate * hoursWorked)`; `return Math.Clamp(deposit - billable, 0, deposit)`
- Implements BR-REF-01..08

### Step 10 — Update `ConfigDefaults.cs`
- [x] Edit `Dayswork.Core/Config/ConfigDefaults.cs`
- Change `AverageSpeedConstant: 5.0` to `AverageSpeedConstant: 0.3`
- Update the inline comment to reflect the finalized unit: "pricing-min per raw tile per task; see U-05 HoursEstimator for formula"

### Step 11 — `PricingGen.cs` (FsCheck generator, PBT-07)
- [x] Create `Dayswork.Tests/Generators/PricingGen.cs`
- Static class in `Dayswork.Tests.Generators` namespace, mirroring `ZoneGen` and `ConfigSnapshotGen` style
- Arbitraries:
  - `ValidRate(IConfigSnapshot config)` — `int` in `[config.BaseRate, config.BaseRate + Σ all increments]`
  - `ValidEstimatedHours()` — `double` in `(0.001, 20.0)` (positive, bounded for practical deposits)
  - `ValidDeposit()` — `int` in `[1, 100_000]`
  - `ValidHoursWorked(double maxHours)` — `double` in `[0.0, maxHours]`
  - `TaskSubset()` — random subset of all `TaskKind` values as `IReadOnlyList<TaskKind>`

### Step 12 — `RateCalculatorTests.cs`
- [x] Create `Dayswork.Tests/Pricing/RateCalculatorTests.cs`
- **[Fact] tests**:
  1. `EmptyTasks_ReturnsBaseRate` — no tasks → `config.BaseRate`
  2. `SingleTask_ReturnsBaseRatePlusIncrement` — one concrete task
  3. `WaterCrops_IncludedWhenNotRaining`
  4. `WaterCrops_ExcludedWhenRaining` — rate equals rate without WaterCrops
  5. `OtherTasks_UnaffectedByRain` — HarvestCrops unaffected when raining
  6. `AllTasks_NotRaining_ReturnsSumOfAll` — 245g/hr (50 base + 195 all increments)
- **[Property] tests** (PBT-03, MaxTest=1000, using `ConfigSnapshotGen` + `PricingGen.TaskSubset()`):
  1. `Rate_AlwaysAtLeastBaseRate` — for any snapshot + any task subset
  2. `Rate_EqualsBasePlusSumWhenNotRaining`
  3. `Rain_EquivalentToRemovingWaterCrops` — `rate(tasks, isRaining:true) == rate(tasks-{WaterCrops}, isRaining:false)`
  4. `Rate_IndependentOfEnumerationOrder` — shuffled task list gives same result

### Step 13 — `HoursEstimatorTests.cs`
- [x] Create `Dayswork.Tests/Pricing/HoursEstimatorTests.cs`
- **[Fact] tests**:
  1. `EmptyZones_ReturnsZero`
  2. `ZeroTasks_ReturnsZero`
  3. `SingleZone_SingleTask_MatchesFormula` — 10×10 zone, 1 task, AverageSpeedConstant=0.3 → `100*1*0.3/60 = 0.5`
  4. `TwoZones_AreasAddUp` — result equals sum of individual zone estimates
- **[Property] tests** (PBT-03, using `ZoneGen.ZoneList()` + `ConfigSnapshotGen`):
  1. `Estimate_AlwaysNonNegative`
  2. `Estimate_NonDecreasingWithMoreTiles` — appending a zone never decreases estimate
  3. `Estimate_NonDecreasingWithMoreTasks` — increasing `numEnabledTasks` never decreases estimate

### Step 14 — `DepositCalculatorTests.cs`
- [x] Create `Dayswork.Tests/Pricing/DepositCalculatorTests.cs`
- **[Fact] tests**:
  1. `ZeroEstimatedHours_ReturnsZeroDeposit`
  2. `NegativeEstimatedHours_ReturnsZeroDeposit`
  3. `PositiveHours_ReturnsPositiveDeposit`
  4. `CeilingApplied` — 70 rate × 0.5001 hours → Ceiling(35.007) = 36 (not 35)
  5. `ExactHours_NoCeilingEffect` — 70 × 0.5 = 35.0 → Ceiling = 35
- **[Property] tests** (PBT-03, using `PricingGen.ValidRate()` + `PricingGen.ValidEstimatedHours()`):
  1. `Amount_AlwaysNonNegative_WhenPositive`
  2. `Amount_AtLeastFloor_WhenPositive` — `amount >= (int)Math.Floor(rate * hours)`

### Step 15 — `RefundCalculatorTests.cs`
- [x] Create `Dayswork.Tests/Pricing/RefundCalculatorTests.cs`
- **[Fact] tests**:
  1. `ZeroHoursWorked_FullRefund` — refund == deposit
  2. `FullHoursWorked_ApproximatelyZeroRefund` — same hours as estimate → small refund (0 or 1g rounding)
  3. `PartialHoursWorked_PartialRefund` — concrete: deposit=285, hoursWorked=1.5, rate=95 → billable=Ceiling(142.5)=143 → refund=142
  4. `NegativeClamp` — hoursWorked that would produce billable > deposit → refund clamped to 0
  5. `UpperClamp` — refund never exceeds deposit (hoursWorked=0)
- **[Property] tests** (PBT-03, using `PricingGen.ValidDeposit()`, `PricingGen.ValidHoursWorked(max)`):
  1. `Refund_AlwaysInRange` — `0 <= refund <= deposit`
  2. `NetCharge_NeverExceedsBillable` — `deposit - refund <= Math.Ceiling(rate * hoursWorked)` (with 1g tolerance for ceiling edge)
  3. `FullRefund_WhenZeroHoursWorked` — `Calculate(deposit, 0.0, rate) == deposit`

### Step 16 — Build verification
- [x] Run `dotnet build Dayswork.Core\Dayswork.Core.csproj` — 0 errors, 0 warnings
- [x] Run `dotnet build Dayswork.Tests\Dayswork.Tests.csproj` — 0 errors, 0 warnings (after fixing C#10 collection-expression syntax and ForAll arity)
- [x] Verify `Dayswork.Core/Pricing/` contains no `using StardewValley` or `using StardewModdingAPI` imports (NFR-MAINT-03)

### Step 17 — Test execution
- [x] Run `dotnet test Dayswork.Tests\Dayswork.Tests.csproj`
- [x] Confirm all new U-05 tests pass — 33 new tests (19 Fact + 14 Property covering 1000 inputs each)
- [x] Confirm prior U-02/U-03/U-04 tests still pass — 70 passed, 1 skipped (PBT-08 demo), 0 failed
- [x] Confirm PBT-03 properties run with MaxTest=1000 inputs each

### Step 18 — Code summary
- [x] Create `aidlc-docs/construction/U-05-pricing-core/code/u-05-code-summary.md`
- Record all files created, test counts, PBT compliance, build results, calibration note

---

## Story traceability

| Story | Delivered by | Step(s) |
|---|---|---|
| S-02 (live rate display) | `IRateCalculator` + `RateCalculator` | Steps 2–3 |
| S-06 (estimate + deposit + summary) | `IHoursEstimator`, `IDepositCalculator`, `IRefundCalculator` | Steps 4–9 |
| S-14 (rain rate exclusion) | `RateCalculator.isRaining` branch | Step 3 |
| S-19 (PBT-03 invariants) | All `[Property]` tests | Steps 12–15 |
