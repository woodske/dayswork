# U-21 — Worker Energy + Shift Runtime Refresh: Code Summary

## Outcome

U-21 switched the live worker runtime from the old hourly/refund settlement model to a stamina-limited shift model with explicit wrap-up semantics:

- actual labor beats now spend worker stamina from the saved `ContractTermsSnapshot`
- stamina clamps at zero and blocks new work units, but the worker still finishes the current visible unit
- 8pm and zero-stamina stops now respect the same work-unit boundary rule
- shift wrap-up still preserves output safety, but no longer computes refund/debt settlement
- the worker now shows an overhead stamina bar and uses slower explicit pacing for both locomotion and task beats

## Modified files

### Core runtime model

- `Dayswork.Core/Config/IConfigSnapshot.cs`
- `Dayswork.Core/Config/ConfigDefaults.cs`
- `Dayswork.Core/Config/ConfigSnapshot.cs`
- `Dayswork.Core/Config/ConfigSnapshotFactory.cs`
- `Dayswork.Core/Shifts/ShiftContext.cs`
- `Dayswork.Core/Shifts/ShiftStateMachine.cs`

### Runtime shell

- `Dayswork/Orchestration/ShiftOrchestrator.cs`
- `Dayswork/Orchestration/RecurringContractScheduler.cs`
- `Dayswork/Integration/ModConfig.cs`
- `Dayswork/Integration/RuntimeConfigSnapshotMapper.cs`
- `Dayswork/ModEntry.cs`
- `Dayswork/Worker/FarmhandNpc.cs`
- `Dayswork/Worker/ToolSwapAnimator.cs`
- `Dayswork/Worker/WorkerMovementDriver.cs`

### Existing test/config areas updated

- `Dayswork.Tests/Config/ConfigSnapshotFactoryTests.cs`
- `Dayswork.Tests/Config/ConfigSnapshotGenSmokeTests.cs`
- `Dayswork.Tests/Config/RuntimeConfigSnapshotMapperTests.cs`
- `Dayswork.Tests/Generators/ConfigSnapshotGen.cs`

## Created files

### New pure energy/pacing seams

- `Dayswork.Core/Energy/WorkerEnergyState.cs`
- `Dayswork.Core/Energy/WorkerEnergySpendResult.cs`
- `Dayswork.Core/Energy/WorkerEnergyLedger.cs`
- `Dayswork.Core/Energy/WorkUnitBoundaryDecision.cs`
- `Dayswork.Core/Energy/WorkUnitBoundaryClassifier.cs`
- `Dayswork.Core/Energy/WorkerPacingProfile.cs`
- `Dayswork.Core/Shifts/ShiftStopReason.cs`

### Dedicated U-21 regression coverage

- `Dayswork.Tests/U21/U21PropertyGenerators.cs`
- `Dayswork.Tests/U21/WorkerEnergyLedgerTests.cs`
- `Dayswork.Tests/U21/WorkerEnergyPropertyTests.cs`
- `Dayswork.Tests/U21/WorkUnitBoundaryClassifierTests.cs`
- `Dayswork.Tests/U21/ShiftStateMachineStopReasonTests.cs`
- `Dayswork.Tests/U21/ShiftContextTests.cs`

## Implementation notes

- `ShiftContext` no longer owns `DepositAmount`, `HourlyRate`, or `ComputeRefund()`. It now carries authoritative `ContractTerms`, live `WorkerEnergyState`, and `WorkerPacingProfile`.
- `ShiftStateMachine` gained explicit stop-reason ownership through `ShiftStopReason` plus `BeginWrapUp(...)`.
- `ShiftOrchestrator` now:
  - resolves runtime energy from the stored contract terms
  - spends stamina on each labor beat only
  - distinguishes unresolved multi-beat work from resolved work-unit boundaries
  - treats `full tree -> stump` as a resolved unit and `stump removal` as a later unit
  - queues wrap-up on zero stamina / 8pm / stuck abort / cancel without refund semantics
  - settles only overflow-output mail at shift end
- `WorkerMovementDriver` and `ToolSwapAnimator` now consume an explicit `WorkerPacingProfile`.
- `FarmhandNpc` now renders an overhead stamina bar tied directly to runtime stamina state.

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` passed with `0` errors and `0` warnings.
- `dotnet test Dayswork.sln` passed with `255` tests passing and `1` expected skip.

## Deliberate deferrals

- U-21 keeps the current task-discovery / compatibility planning bridge in place. Full typed-scope runtime alignment for outdoor zones, animal buildings, and greenhouse execution still belongs to `U-22`.
- Recurring day-start billing semantics, low-work-day charging behavior, and remaining hourly/deposit compatibility cleanup outside the live shift runtime still belong to `U-23`.
