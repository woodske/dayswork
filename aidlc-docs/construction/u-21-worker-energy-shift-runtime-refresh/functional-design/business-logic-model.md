# U-21 — Worker Energy + Shift Runtime Refresh: Business Logic Model

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A, FD-Q9=A

Technology-agnostic runtime flows for switching the farmhand from the old hourly settlement model to the redesign-era stamina-limited workday.

This unit introduces three big runtime changes at once:
- the worker now runs on a visible daily stamina budget
- work stops at work-unit boundaries rather than at arbitrary billing checkpoints
- shift wrap-up no longer includes refund or debt settlement

This unit intentionally does **not** finish the broader typed-scope runtime switchover. U-21 focuses on energy, pacing, and shift semantics. Scope-driven runtime alignment for animal buildings and greenhouse work completes in U-22.

See [domain-entities.md](domain-entities.md) for data shapes, [business-rules.md](business-rules.md) for enforceable rules, and [frontend-components.md](frontend-components.md) for the visible stamina bar and pacing-facing NPC behavior.

---

## 0. Where this plugs into the redesign

U-18 created the pure contract-terms model:

```text
ContractScopeSelection + config
  -> ContractTermsBuilder
  -> ContractTermsSnapshot
     - PricingSnapshot
     - WorkerEnergyProfile
```

U-19 persisted that snapshot, and U-20 exposed it in the hire/review flow.

U-21 is where the live runtime starts consuming the stored `WorkerEnergyProfile` instead of treating shift duration as a billing problem:

```text
Approved contract
  -> ShiftOrchestrator.StartShift(...)
  -> Tool snapshot + capability snapshot
  -> WorkerEnergyLedger.StartShift(profile)
  -> ShiftContext / state machine / NPC runtime loop
  -> deposit-and-exit
```

The important redesign difference is that the shift loop is no longer responsible for answering:
- how much should the player be refunded?
- how many billable hours actually happened?

It is now responsible for answering:
- can a new work unit start?
- what stamina is left?
- has the worker reached a stop boundary?

---

## 1. Shift bootstrap flow

### 1.1 Start-shift inputs

At the moment the worker day begins, the orchestrator receives:
- the active `Contract`
- the contract's stored `ContractTermsSnapshot`
- the current immutable config snapshot for runtime-only knobs

The stored terms snapshot is authoritative for:
- the worker's daily stamina capacity
- the worker's per-action energy-cost table

Current config still matters for operational runtime values such as:
- stuck thresholds
- pacing knobs
- other non-billing live-execution settings

### 1.2 Spawn-time snapshotting

Before any work begins, the runtime performs two one-time snapshots:

1. `ToolLevelReader` captures the player's tool tiers.
2. `CapabilityEvaluator` converts that tool snapshot into a shift-stable capability view.

That capability snapshot is then frozen for the rest of the shift. Mid-day player tool upgrades or swaps do not change what the worker can do that day.

### 1.3 Runtime context creation

The orchestrator creates live shift state with:
- contract identity
- enabled tasks
- output destinations
- compatibility planning inputs still used by the current runtime
- capability snapshot
- `WorkerEnergyState` started from the contract's stored `WorkerEnergyProfile`
- `WorkerPacingProfile` built from runtime config
- item buffer / overflow state
- current stop-reason state

This is also the point where the redesign removes the runtime's dependency on:
- `DepositAmount`
- `HourlyRate`
- `ComputeRefund()`

Those are no longer runtime budget inputs in the U-21 model.

### 1.4 NPC arrival and visible entrance

The farmhand still spawns at the farm entrance, but U-21 keeps an intentional short entrance hold and slower pacing so the player can register:
- the worker's arrival
- the worker's first task direction
- the presence of the new overhead stamina bar

---

## 2. Target selection and work routing

### 2.1 Broad priority order remains stable

U-21 preserves the approved broad task-order feel:
- animal work first
- crop work second
- clearing work third

Within the active non-animal family, the runtime continues to use nearest-next routing rather than returning to a rigid static task-kind order.

This keeps the worker behavior familiar while adding the new stamina budget.

### 2.2 Scope/planning bridge remains in place for this unit

U-21 does not redefine how every task target is discovered. It reuses the current runtime's planning inputs and task scanning shape, then adds:
- stamina accounting
- slower pacing
- work-unit boundaries
- stop semantics

That means U-21 can land without waiting for U-22's typed-scope runtime alignment.

### 2.3 Intent-driven execution still applies

The runtime loop remains intent-driven:

```text
select next target
  -> emit move/perform/deposit/exit intent
  -> execute the live world action
  -> feed the resulting event back into shift state
```

What changes in U-21 is the event vocabulary around work completion:
- a labor beat may reduce stamina
- a completed work unit may forbid starting the next one
- zero stamina or 8pm only force wrap-up at work-unit boundaries

---

## 3. Work-unit model

### 3.1 Smallest player-visible resolved unit

The current work unit is the smallest player-visible resolved piece of labor, not a whole batch and not a whole object chain.

Examples:
- water one crop tile
- harvest one crop tile
- collect one fruit interaction
- pet one animal
- collect from one animal
- clear one weed / rock / grass object
- resolve one tree stage such as `full tree -> stump`
- resolve stump removal as its own later work unit

### 3.2 Multi-beat work units

Some work units resolve in a single beat, and some resolve through multiple labor beats.

Examples:
- watering one tile may consume one `WaterTile` beat
- petting one animal may consume one `PetAnimal` beat
- breaking one tougher rock may consume multiple `PickaxeSwing` beats
- dropping a full tree to a stump may consume multiple `AxeSwing` beats

The work unit stays "open" until that resolved unit finishes.

### 3.3 Boundary semantics

The worker may only decide whether to begin another unit **between** work units.

So the runtime question is never:
- "did stamina hit exactly zero mid-swing, so should we stop immediately?"

It is:
- "did the current work unit just resolve, and if so, may a new one start?"

That is the seam where zero stamina and 8pm both take effect.

---

## 4. Labor-beat execution and stamina spending

### 4.1 Spend on actual labor beats only

Every actual labor beat spends stamina when that beat executes:
- each tool swing
- each watering interaction
- each harvest interaction
- each petting interaction
- each animal-product collection interaction

Walking, pathfinding, and deposit travel do not spend stamina.

### 4.2 Beat-by-beat loop

For each in-progress work unit:

1. The orchestrator executes one live labor beat.
2. That beat maps to a `WorkActionKind`.
3. `WorkerEnergyLedger.ApplyActionCost(...)` consumes the configured cost for that beat.
4. The ledger clamps remaining stamina at zero if needed.
5. The worker stamina bar updates immediately after the beat.
6. If the work unit is not yet resolved, another beat may continue the same unit even while stamina now shows zero.

This design intentionally avoids:
- pre-charging a whole work unit
- charging only once at the end of the unit
- letting walk distance distort stamina cost

### 4.3 Hitting zero during a unit

If a labor beat drops stamina to zero while a work unit is already in progress:
- the bar stays at zero
- the current unit is allowed to finish
- no new unit may begin after that

This keeps the behavior legible:
- no negative stamina
- no mid-action abort
- no "free" additional units after exhaustion

---

## 5. Pacing model

### 5.1 Movement pacing

Movement is intentionally slowed through an explicit pacing profile rather than through accidental lag.

This profile owns knobs such as:
- movement speed multiplier / slower walk rate
- entrance hold duration
- optional recovery/turnaround beats

### 5.2 Action pacing

Task execution is also intentionally slowed through explicit action cadence values rather than hidden delays.

Examples:
- time between consecutive axe swings
- readable pause between watering tiles
- brief interaction cadence for petting/collecting
- tool-swap timing if a visual handoff is shown

This makes the worker feel like in-world labor instead of instant automation.

### 5.3 Frame-rate independence

Pacing is modeled as deliberate runtime timing, not as a side effect of slow update code. The intended slow feel must survive stable frame rates and typical hardware differences.

---

## 6. Stop conditions and wrap-up

### 6.1 Stop reasons

The live shift can stop because:
- all work is complete
- stamina is exhausted at a work-unit boundary
- 8pm is reached at a work-unit boundary
- stuck recovery escalates to end-shift
- player sleep triggers the existing stop-and-settle path

### 6.2 Deposit is always honored

Once the runtime enters wrap-up:
- deposit planning still runs normally
- chest/bin delivery still happens even if stamina is zero
- the worker exits only after deposit handling finishes

Stamina limits work output, not end-of-shift safety behavior.

### 6.3 No refund/debt settlement

U-21 removes refund semantics from shift completion.

End-of-shift logic now answers:
- where do buffered items go?
- which deposit trips still need to run?
- has the worker reached the exit?

It does not answer:
- how much of the deposit should be returned?
- how many hours should be billed?

---

## 7. Sleep-stop, stuck recovery, and invulnerability

### 7.1 Sleep-stop preserved in shape

U-21 keeps the U-15 operational sleep-stop shape:
- player sleep interrupts live work
- the current wrap-up/settlement path still handles buffered outputs safely
- remaining world work stays undone

The U-21 change is only that refund/debt semantics are removed from that path.

### 7.2 Stuck recovery preserved under the new budget model

Stuck detection and escalation still behave as runtime safety systems, but they now operate alongside stamina state instead of hourly settlement state.

The stuck path must preserve:
- confused-emote first signal
- teleport-and-resume recovery
- final early-shift termination if recovery keeps failing
- safe output handling without refund math

### 7.3 Invulnerability preserved

The worker remains invulnerable to player attacks:
- hit reaction still plays
- stamina is not damaged by player attacks
- the shift is not abandoned because of combat interaction

---

## 8. Testable properties

Property-Based Testing is enabled in partial mode for this project. U-21 keeps its strongest runtime decision logic in pure Core seams so later code generation can attach FsCheck coverage to meaningful invariants rather than to SMAPI glue.

| Component / seam | Category | Property to carry into code generation |
|---|---|---|
| `WorkerEnergyLedger` | Invariant | Remaining stamina never goes below zero, never exceeds daily capacity, and walking events never change stamina. |
| `WorkerEnergyLedger` | Invariant | Once `CanStartNewWorkUnit = false` because stamina is zero, it never flips back to true during the same shift. |
| `WorkerEnergyLedger` | Oracle / easy verification | Applying a known sequence of `WorkActionKind` costs matches a simple arithmetic model with clamp-at-zero behavior. |
| Work-unit boundary classification | Invariant | Multi-stage objects produce separate boundaries (`tree -> stump` and `stump removed` are distinct units). |
| Shift event / transition logic | Invariant | Energy depletion and 8pm only trigger wrap-up at work-unit boundaries, never mid-unit. |
| Shift transition logic | Invariant | Wrap-up paths contain deposit-and-exit behavior but never billing/refund intents. |
| Pacing profile normalization | Invariant | Pacing values remain non-negative and structurally complete for every supported labor action family. |

These are design-time property identifications per PBT-01, even though PBT-01 is advisory under the project's chosen partial-enforcement mode.
