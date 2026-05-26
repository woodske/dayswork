# U-21 — Worker Energy + Shift Runtime Refresh: Frontend Components

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A, FD-Q9=A

Even though U-21 is mostly a runtime unit, it changes what the player sees in-world: the worker now has visible stamina and intentionally slower task pacing.

---

## Component hierarchy

```text
ShiftOrchestrator
  -> FarmhandNpc
     -> overhead energy bar presentation
     -> idle / task-facing / hit-reaction presentation
  -> WorkerMovementDriver
  -> ToolSwapAnimator

FarmhandNpc
  <- WorkerEnergyState / FarmhandEnergyBarModel from runtime
```

Key separation:
- `ShiftOrchestrator` owns live runtime state transitions
- `FarmhandNpc` renders that state
- the overhead stamina bar is a presentation of runtime state, not an independent gameplay system

---

## Shared runtime UI state

### `FarmhandEnergyBarModel`

The visible stamina bar needs only a narrow view model:
- current stamina
- maximum stamina
- exhausted/not exhausted
- visible/not visible

This is intentionally smaller than the full `ShiftContext`.

### Update triggers

The visible model updates when:
- the shift begins
- a labor beat spends stamina
- the worker reaches zero
- the shift fully ends

It does **not** update on:
- walking progress
- path recalculation
- deposit travel

because those do not spend stamina.

---

## M-09 FarmhandNpc

### Responsibilities in this unit

- Render the worker in-world during the shift
- Show the overhead stamina bar while the shift is active
- Reflect slower, more readable action pacing
- Preserve hit reaction and normal NPC visibility behavior

### Non-responsibilities

`FarmhandNpc` does **not**:
- decide how much stamina to spend
- decide whether a new work unit may begin
- own refund/debt settlement logic

Those stay outside the NPC.

---

## Overhead stamina bar behavior

### Placement

The stamina bar appears above the worker NPC in-world so the player can read remaining labor capacity at a glance while watching the farmhand move and work.

### Visibility rules

- Visible while an active shift exists and the worker is present
- Visible during the morning entrance hold
- Visible during the final in-progress work unit even if stamina is already zero
- Visible during deposit-and-exit wrap-up
- Hidden once the worker has left and the shift is complete

### Value rules

- Starts full at shift start
- Drops only after actual labor beats
- Never displays a negative value
- May remain at zero while the worker finishes the last already-started unit

### Non-goal

U-21 does not add a second mirrored HUD widget elsewhere on screen.

---

## Pacing-facing presentation

### Arrival feel

The worker should not appear to blink into productivity instantly. The presentation should allow the player to register:
- the worker's arrival from the farm entrance
- the worker's first destination
- the presence of the new stamina bar

### Movement feel

Walking should read as intentional farm labor, not teleport-adjacent automation. The visible movement rate therefore follows the new slower pacing profile.

### Action feel

Task actions should have readable beats:
- discernible swing cadence for tools
- readable pauses between repeated identical actions
- no ultra-fast strobing through a long row of tasks

The visible result should communicate where the worker's stamina is going.

### Tool-swap / transition feel

If a tool swap or task transition is shown, that beat should also respect the pacing profile rather than happening instantly.

---

## Interaction states the player can observe

### Active work

Observable cues:
- worker walking at deliberate speed
- stamina bar visible
- task-facing orientation / tool beats

### Zero-stamina final unit

Observable cues:
- stamina bar already at zero
- worker still finishing the current in-progress unit
- worker then transitions into wrap-up instead of starting something new

### Deposit-and-exit

Observable cues:
- worker no longer begins fresh labor actions
- worker performs deposit trips
- worker leaves through the normal exit path

### Hit reaction

Observable cues:
- brief reaction/emote when struck by the player
- no collapse, no loss of stamina from the hit, no contract abort

---

## Accessibility and clarity goals

- The bar should communicate "remaining workday capacity" without requiring the player to open any menu.
- Slower pacing should improve readability without making the worker feel broken or sluggish for the wrong reason.
- The zero-stamina state should be visually understandable: the player sees the bar empty, the current unit finish, and then the worker stop taking on new work.

---

## Non-goals for this unit

U-21 does not introduce:
- a separate on-screen worker HUD panel
- new review-screen messaging when the worker runs out of stamina
- custom post-v1 worker art
- richer per-tool bespoke animation sets beyond what the existing worker presentation seams can reasonably support
