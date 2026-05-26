# U-21 — Logical Components

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh

NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A apply. Functional-design decisions FD-Q1=A through FD-Q9=A apply throughout.

---

## Component Map

```text
Dayswork.Core / Runtime Decisions
  WorkerEnergyLedger                 [new/expanded pure seam]
  WorkerEnergyState                  [pure runtime state carrier]
  WorkUnitBoundaryClassifier         [logical helper seam]
  ShiftStateMachine                  [existing pure seam, expanded]
  WorkerPacingProfile                [pure config/runtime carrier]

Dayswork / Runtime Shell
  ShiftOrchestrator                  [existing live world adapter]
  FarmhandNpc                        [existing presentation seam, expanded]
  WorkerMovementDriver               [existing movement seam]
  ToolSwapAnimator                   [existing task-beat presentation seam]

Dayswork.Core / Output Safety
  ItemBuffer                         [existing dependency]
  DepositPlanner                     [existing dependency]

Dayswork.Tests / Runtime
  U21ExampleTests                    [test-side grouping]
  U21PropertyGenerators              [test-side helper]
  U21PropertyTests                   [test-side grouping]
  U21StateSequenceModel              [optional test-side helper]
```

No new async subsystem, HUD framework, concurrency layer, or infrastructure component is introduced. The design deliberately keeps the retrofit inside the current runtime shell plus clearer pure seams and stronger test-side support.

---

## LC-U21-01 — WorkerEnergyLedger

**Layer**: Core / pure runtime decision seam  
**Kind**: New or newly authoritative production seam

**Purpose under U-21**:
- own stamina arithmetic for actual labor beats
- clamp remaining stamina at zero
- decide whether a new work unit may begin

**Responsibilities**:
1. Start shift energy state from `WorkerEnergyProfile`
2. Apply one `WorkActionKind` cost at a time
3. Produce deterministic updated energy state/results
4. Preserve the "finish current unit, but don't start another" rule once zero is reached

**Important design constraints**:
- no SMAPI/Stardew references
- no pathfinding knowledge
- no output/deposit knowledge
- no refund/debt settlement logic

This is the primary owner of U-21’s new runtime budget model.

---

## LC-U21-02 — WorkUnitBoundaryClassifier

**Layer**: Core / pure helper seam  
**Kind**: Logical helper behavior

**Purpose**:
- define and recognize the smallest player-visible resolved work unit for stop-boundary decisions

**Responsibilities**:
1. Distinguish one labor beat from a whole resolved work unit
2. Distinguish separate stages of multi-stage objects
3. Tell the stateful runtime decision layer when a boundary has actually been reached

**Not responsible for**:
- spending stamina
- choosing the next target
- drawing the worker

This seam is what keeps zero-stamina and 8pm behavior understandable and testable.

---

## LC-U21-03 — ShiftStateMachine (Expanded Runtime Ownership)

**Layer**: Core / pure stateful seam  
**Kind**: Existing production seam with expanded redesign ownership

**Purpose under U-21**:
- own phase transitions around working, wrap-up, and exit
- consume stop/boundary events from the pure runtime decision layer
- stay free of refund/debt settlement semantics

**Responsibilities**:
1. Represent working, stuck, recovering, depositing, exiting, and done phases
2. Accept boundary-aware stop reasons
3. Emit intents for move, perform, deposit, recover, and exit behavior
4. Preserve deterministic transition behavior across equivalent inputs

**Important constraint**:
- U-21 should not let refund/debt settlement remain embedded in phase/intent decisions

---

## LC-U21-04 — WorkerPacingProfile

**Layer**: Core/App boundary carrier  
**Kind**: Lightweight production-side configuration model

**Purpose**:
- centralize the slower readable pacing behavior that U-21 requires

**Responsibilities**:
1. Carry movement pacing values
2. Carry labor-cadence values for action beats
3. Keep these values explicit, deterministic, and easy to test

**Why it matters in NFR design**:
- it separates "slower by design" from "slower because the loop is inefficient"
- it creates a natural later hook for GMCM/config without another redesign

---

## LC-U21-05 — ShiftOrchestrator (Thin Runtime Shell)

**Layer**: App / SMAPI runtime integration seam  
**Kind**: Existing production seam with constrained redesign ownership

**Purpose under U-21**:
- stay the live world adapter for the worker day
- translate world events into pure runtime decisions
- execute the resulting intents

**Responsibilities**:
1. Start the shift with tool snapshot, energy state, and pacing state
2. Execute movement and labor beats in the live world
3. Map labor beats to `WorkActionKind`
4. Feed pure energy/boundary/stop results back into phase progression
5. Reuse the existing output/deposit pipeline for wrap-up

**Important design constraints**:
- do not become the primary owner of stamina arithmetic
- do not rebuild refund/debt settlement logic locally
- do not introduce async job infrastructure just to satisfy pacing or HUD updates

This component remains essential, but it is intentionally not the redesign’s source of truth for the hardest new logic.

---

## LC-U21-06 — FarmhandNpc (Expanded Presentation Ownership)

**Layer**: App / world presentation seam  
**Kind**: Existing presentation seam with expanded redesign ownership

**Purpose under U-21**:
- render the worker in-world
- show the overhead stamina bar
- reflect slower readable labor pacing

**Responsibilities**:
1. Display the worker during the active shift
2. Display the overhead stamina bar from authoritative runtime state
3. Preserve hit-reaction behavior
4. Remain presentation-focused rather than becoming a logic owner for stamina or stop decisions

**Important constraint**:
- the bar is an NPC-attached presentation of runtime state, not a second gameplay system

---

## LC-U21-07 — Existing Output Safety Components

**Layer**: Core / existing dependency seams  
**Kind**: Reused production dependencies

**Members**:
- `ItemBuffer`
- `DepositPlanner`

**Purpose under U-21**:
- preserve the already-landed output-safety guarantees while the worker shift semantics change

**Why they are listed here**:
- the NFR bar for U-21 depends on stop-path consistency
- that consistency is achieved by reusing these existing components, not by inventing a second settlement mechanism

U-21 changes when wrap-up begins, not how output safety fundamentally works.

---

## LC-U21-08 — Test-Side Runtime Support

**Layer**: `Dayswork.Tests` only  
**Kind**: Dedicated regression-support helpers

### `U21PropertyGenerators`

**Purpose**:
- generate beat sequences, work-unit boundaries, and stop-reason scenarios that exercise the new stamina/runtime rules

### `U21ExampleTests`

**Purpose**:
- pin concrete important stories such as:
  - stamina reaches zero on the last beat of a tree stage
  - zero stamina forbids a new unit
  - sleep-stop still settles buffered output without refund math
  - stuck-abort still preserves output safety

### `U21PropertyTests`

**Purpose**:
- express invariants with FsCheck:
  - stamina stays within bounds
  - deterministic pure-runtime results
  - boundary-only stop transitions
  - no-refund wrap-up semantics

### `U21StateSequenceModel`

**Purpose**:
- optional test-side model/helper for command-sequence or state-machine style checks across repeated beats and stop transitions

These are explicit logical components because U-21’s NFR bar is more about stateful runtime correctness than about simple one-shot function output.

---

## Interaction Summary

```text
Live runtime beat
  -> ShiftOrchestrator executes world action
  -> maps to WorkActionKind
  -> WorkerEnergyLedger updates energy state
  -> WorkUnitBoundaryClassifier determines whether a boundary was reached
  -> ShiftStateMachine decides continue vs wrap-up
  -> FarmhandNpc updates overhead stamina presentation

Stop path
  -> ShiftStateMachine enters wrap-up
  -> ShiftOrchestrator reuses ItemBuffer + DepositPlanner pipeline
  -> FarmhandNpc remains presentation-only during deposit-and-exit
```

---

## Why no additional runtime infrastructure was introduced

The NFR design intentionally does **not** add:
- a threaded worker executor
- a background event queue for HUD updates
- a dedicated HUD framework
- a caching/memoization subsystem for per-tick runtime work
- a second settlement/output pipeline

Reason:
- only one worker exists in this mod’s current design
- the live state space is small enough for synchronous deterministic decisions
- the hardest risk is correctness and clarity, not distributed load
- the existing runtime shell is sufficient if the pure seams are strengthened

That keeps U-21’s runtime redesign sharp, incremental, and testable.
