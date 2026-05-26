# U-21 — Worker Energy + Shift Runtime Refresh: Domain Entities

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A, FD-Q9=A

This file defines the pure and bridge runtime data shapes introduced or locked by the stamina-limited shift model. These types should remain split cleanly between:
- pure Core decision/data seams
- SMAPI/Stardew runtime adapters

See [business-logic-model.md](business-logic-model.md) for flows and [business-rules.md](business-rules.md) for enforceable rules.

---

## Existing types reused

| Type | Role in U-21 |
|---|---|
| `Contract` | Supplies enabled tasks, output destinations, saved compatibility scope inputs, and authoritative `ContractTermsSnapshot`. |
| `ContractTermsSnapshot` | Source of the shift's `WorkerEnergyProfile`; pricing remains informational here, not a runtime budget input. |
| `WorkerEnergyProfile` | Saved daily stamina capacity plus the full per-action cost table. |
| `TaskKind` | Top-level service/task family key used by work planning and output routing. |
| `WorkActionKind` | Fine-grained labor beat key used when stamina is actually spent. |
| `ToolSnapshot` | Spawn-time captured player tool tiers. |
| `WorkItem` / `WorkBatch` | Existing bridge work-planning units still used by the runtime until U-22 finishes scope-driven runtime alignment. |
| `ItemBuffer` / `OverflowItem` / `DepositTrip` | Existing output-safety pipeline reused for shift wrap-up. |
| `ShiftPhase` / `ShiftIntent` | Existing state-machine bridge concepts retained, but their semantics change under the no-refund runtime model. |

---

## New or locked pure runtime types

### `WorkerEnergyState`

Live per-shift stamina state derived from a saved `WorkerEnergyProfile`.

```text
WorkerEnergyState
  DailyCapacity        : int
  RemainingEnergy      : int
  Exhausted            : bool
  CanStartNewWorkUnit  : bool
```

Interpretation:
- `RemainingEnergy` is clamped to `[0, DailyCapacity]`
- `Exhausted` means the worker has reached zero during this shift
- `CanStartNewWorkUnit` becomes false once zero has been reached

### `WorkerEnergySpendResult`

Pure result of applying one labor beat cost.

```text
WorkerEnergySpendResult
  PreviousState        : WorkerEnergyState
  UpdatedState         : WorkerEnergyState
  Action               : WorkActionKind
  AppliedCost          : int
  HitZeroOnThisBeat    : bool
```

This lets the orchestrator react to:
- normal stamina consumption
- first transition to zero
- unchanged zero state while finishing the current unit

### `WorkerPacingProfile`

Pure runtime pacing settings built from config.

```text
WorkerPacingProfile
  EntranceHoldTicks        : int
  MovementSpeedMultiplier  : decimal
  ActionCadenceTicks       : IReadOnlyDictionary<WorkActionKind, int>
  ToolSwapCadenceTicks     : int
```

This type keeps "slower worker feel" explicit and testable instead of scattering timing constants across the orchestrator.

### `WorkUnitKind`

```text
WorkUnitKind
  { TileInteraction, AnimalInteraction, ObjectStage, BuildingInteraction }
```

The key idea is not the exact enum labels, but that a work unit represents the smallest player-visible resolved unit.

### `WorkUnitDescriptor`

Pure description of the in-progress unit whose completion boundary matters for zero-stamina and 8pm behavior.

```text
WorkUnitDescriptor
  Task             : TaskKind
  Kind             : WorkUnitKind
  LocationName     : string
  TargetTile       : TileCoord
  StageKey         : string?
  ActionSequence   : IReadOnlyList<WorkActionKind>
```

Interpretation:
- `StageKey` distinguishes sequential stages of the same world object when needed
- `ActionSequence` records the ordered labor beats needed to resolve the unit

Examples:
- one watered crop tile -> one `WaterTile`
- one animal pet -> one `PetAnimal`
- one full tree stage -> several `AxeSwing` beats

### `ShiftStopReason`

```text
ShiftStopReason
  { None, WorkComplete, EnergyExhausted, TimeCapReached, SleepStop, StuckAbort }
```

This is the business reason the live work loop stops. It is separate from output-settlement details.

### `ShiftEvent`

Pure events that matter to the shift decision layer.

```text
ShiftEvent
  SpawnCompleted
  ArrivedAtWorkTarget
  LaborBeatApplied(WorkActionKind, WorkerEnergySpendResult)
  WorkUnitResolved(WorkUnitDescriptor)
  StuckDetected
  RecoveryResolved
  DepositTripCompleted
  ExitReached
  SleepStopRequested
  WorkBoundaryStop(ShiftStopReason)
```

The important modeling choice is that the shift logic reacts to abstract events, not directly to SMAPI-specific APIs.

### `ShiftTransitionResult`

Pure output of one state-machine step.

```text
ShiftTransitionResult
  NewPhase     : ShiftPhase
  Intents      : IReadOnlyList<ShiftIntent>
  StopReason   : ShiftStopReason
```

This becomes the bridge between pure runtime decisions and orchestrator-side world effects.

---

## Bridge runtime types extended by U-21

### `ShiftContext`

U-21 keeps `ShiftContext` as the live bridge object, but its authoritative budget fields change.

```text
ShiftContext
  ContractId             : ContractId
  CompatibilityZones     : IReadOnlyList<Zone>
  EnabledTasks           : IReadOnlySet<TaskKind>
  TaskDestinations       : IReadOnlyDictionary<TaskKind, DestinationKey>
  ToolSnapshot           : ToolSnapshot
  TermsSnapshot          : ContractTermsSnapshot
  EnergyState            : WorkerEnergyState
  PacingProfile          : WorkerPacingProfile
  StopReason             : ShiftStopReason
  StateMachine           : ShiftStateMachine
  Batches                : IReadOnlyList<WorkBatch>
  WorkList               : Queue<WorkItem>
  CurrentWorkUnit        : WorkUnitDescriptor?
  Buffer                 : ItemBuffer
  Overflow               : List<OverflowItem>
  ShiftStartTime         : int
  ShiftEndTime           : int?
  RecoveryAttempts       : int
```

Important U-21 change:
- `DepositAmount`
- `HourlyRate`
- `ComputeRefund()`

are no longer part of the active runtime budget model.

The compatibility zone/planning inputs remain temporarily because U-22 still owns the deeper runtime scope-alignment retrofit.

### `ShiftIntent`

The intent vocabulary still covers:
- move
- perform work
- emote
- teleport / recover
- deposit
- exit

But the intent set no longer needs any billing or refund settlement concept.

### `FarmhandEnergyBarModel`

Frontend/runtime bridge state for the visible stamina indicator.

```text
FarmhandEnergyBarModel
  Current      : int
  Maximum      : int
  Visible      : bool
  Exhausted    : bool
```

This model is intentionally tiny because the bar is only an in-world projection of authoritative `WorkerEnergyState`.

---

## Ownership boundaries locked by this unit

| Concern | Primary owner |
|---|---|
| Per-beat stamina arithmetic | `WorkerEnergyLedger` + `WorkerEnergyState` |
| Current work-unit boundary description | pure `WorkUnitDescriptor` classification seam |
| Stop reason and shift transition decisions | pure shift/state seam |
| NPC drawing and animation | `FarmhandNpc` + worker integration layer |
| Pathfinding and real-world interaction | `ShiftOrchestrator` and worker adapters |
| Output settlement | `ItemBuffer` + `DepositPlanner` + orchestrator |

This is what preserves testability for U-21 instead of letting the stamina model collapse into ad hoc orchestrator branches.
