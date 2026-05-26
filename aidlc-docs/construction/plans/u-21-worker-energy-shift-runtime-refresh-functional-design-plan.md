# U-21 — Worker Energy + Shift Runtime Refresh: Functional Design Plan

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh  
**Phase**: CONSTRUCTION — Functional Design  
**Status**: Answers reviewed, no clarification round needed, and functional-design artifacts generated. Pending user review.

---

## Plan Checklist

- [x] Load unit definition, story map, refreshed requirements, refreshed user stories, and refreshed application design
- [x] Inspect the current brownfield runtime implementation in `Dayswork/Orchestration/`, `Dayswork/Worker/`, and `Dayswork.Core/`
- [x] Draft FD-Q1 through FD-Q9
- [x] Collect answers to FD-Q1 through FD-Q9
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [x] Generate `frontend-components.md`
- [x] Present completion message and await approval

---

## Context Loaded

- [unit-of-work.md](../../inception/application-design/unit-of-work.md) — U-21 definition and definition of done
- [unit-of-work-story-map.md](../../inception/application-design/unit-of-work-story-map.md) — U-21 story ownership for `S-07`, `S-08`, `S-09`, `S-10`, `S-15`, `S-16`, `S-17`, and `S-19`
- [requirements.md](../../inception/requirements/requirements.md) — redesigned worker-energy, no-refund, pacing, and runtime-scope requirements
- [stories.md](../../inception/user-stories/stories.md) — player-facing expectations for visible stamina, slower pacing, shift-end deposit behavior, and runtime safety/testability
- [application-design.md](../../inception/application-design/application-design.md) — redesign summary and runtime coverage highlights
- [components.md](../../inception/application-design/components.md)
- [component-methods.md](../../inception/application-design/component-methods.md)
- [services.md](../../inception/application-design/services.md)
- Brownfield implementation review:
  - [ShiftOrchestrator.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Orchestration/ShiftOrchestrator.cs)
  - [FarmhandNpc.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Worker/FarmhandNpc.cs)
  - [WorkerMovementDriver.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Worker/WorkerMovementDriver.cs)
  - [ToolSwapAnimator.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Worker/ToolSwapAnimator.cs)
  - [AnimalTaskHandler.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Orchestration/AnimalTaskHandler.cs)
  - [ShiftContext.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Shifts/ShiftContext.cs)
  - [WorkActionKind.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Energy/WorkActionKind.cs)
  - [CapabilityEvaluator.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Capabilities/CapabilityEvaluator.cs)
  - [ItemBuffer.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Inventory/ItemBuffer.cs)
  - [DepositPlanner.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Inventory/DepositPlanner.cs)

---

## What This Unit Must Define

U-21 is the runtime switchover from the old hourly/deposit/refund shift model to the redesign-era energy-limited labor model.

This unit owns the functional behavior of:
- `C-07 WorkerEnergyLedger`
- `C-11 ShiftStateMachine`
- `M-09 FarmhandNpc`
- `M-12 ShiftOrchestrator`
- the runtime-facing portions of `ShiftContext`

This unit must define:
- how the worker's stored `WorkerEnergyProfile` becomes live per-shift stamina state
- how and when energy is spent on concrete labor beats
- what counts as a work-unit boundary for zero-energy and 8pm stop behavior
- how slower movement and slower task cadence become explicit runtime behavior instead of accidental lag
- how shift-end deposit-and-exit now works without any refund or debt settlement
- how the reworked runtime preserves tool-capability snapshot behavior, stuck recovery, and invulnerability

Because this unit affects both world behavior and a visible stamina indicator, frontend interaction details for the runtime HUD belong in `frontend-components.md` even though this is primarily a runtime unit.

---

## Already Decided And Not Re-Decided Here

- Hourly billing, shift-end refund settlement, and hidden change/debt mechanics are gone.
- Contract confirmation and recurring day-start billing already use fixed `ContractTermsSnapshot` data from `ContractTermsBuilder`.
- Worker energy is spent on actual labor actions only. Walking and pathfinding do not consume energy.
- Energy costs generally mirror vanilla farmer energy usage: tool work spends energy per swing/use, and non-tool labor such as petting or harvesting also spends energy per interaction.
- The energy bar never goes below zero. If the worker hits zero during a current work unit, the bar stays at zero while that work unit finishes, then the worker deposits and leaves.
- Multi-stage objects resolve one stage at a time: for example, dropping a full tree to a stump is one work unit, while clearing the stump is a separate later work unit.
- The worker should feel like paid in-world labor, so movement speed and task animation tempo must be slower and more readable than the current implementation.
- Animal work remains the highest-level priority category, and selected-building animals are serviced wherever they roam on the farm.
- Player sleep already stops the worker and settles the day; U-21 must preserve that operational shape while removing refund semantics from the runtime.

This plan focuses only on the remaining functional-design choices that still shape the runtime refresh.

---

## Design Questions

> Answer each question by writing after its `[Answer]:` tag. Pick the letter that best matches your preference. If none fit, choose `X` and describe your preference after the tag.

## Question 1
How should the worker's visible stamina be presented during a live shift?

A) Show a single overhead energy bar attached to the worker NPC, with no separate screen HUD mirror (Recommended)

B) Show both an overhead energy bar and a separate HUD mirror elsewhere on screen

C) Show the energy only in a screen HUD element and not attached to the worker NPC

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 2
How should U-21 preserve the current task-order feel while adding energy-limited runtime behavior?

A) Keep the broad priority order `animals -> crops -> clearing`, and within the currently active non-animal family continue using nearest-next routing as the runtime already does today (Recommended)

B) Return to a fully static task-kind order within each family, even if a farther target is chosen before a nearer one

C) Ignore the broad family order and always choose the globally nearest target regardless of task family

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 3
What should count as the "current work unit" for zero-energy and 8pm cutoff behavior?

A) The smallest player-visible resolved unit: one watered tile, one harvested tile, one petted animal, one cleared object, or one object stage such as `tree -> stump` (Recommended)

B) The entire logical object chain, such as `full tree + stump` or `all drops for one target`, counts as one work unit

C) The whole current batch/zone slice counts as one work unit, so the worker finishes a larger chunk before stopping

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 4
When should the orchestrator spend worker energy for a labor action?

A) Spend energy on each actual labor beat as it executes: every tool swing and every non-tool interaction consumes its configured action cost when that beat happens (Recommended)

B) Spend energy only once when the entire work unit finishes, regardless of how many swings/interactions it took

C) Pre-charge all expected energy for the entire work unit before the worker starts it

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 5
How should the slower pacing requirement be modeled in the runtime refresh?

A) Use explicit config-driven pacing knobs for both movement speed and per-action cadence/cooldown beats, so slower pacing is intentional and frame-rate-independent (Recommended)

B) Slow only the worker's movement speed, but keep current task/action tempo

C) Keep movement speed mostly unchanged and add delay only around task animations/interactions

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 6
Once the worker reaches zero energy or the 8pm cutoff at a work-unit boundary, how should wrap-up behavior treat deposit runs?

A) Deposit runs ignore worker energy entirely: the worker always completes the normal deposit plan after the final work unit, then exits the farm (Recommended)

B) If energy is zero, skip chest/bin delivery and convert everything still buffered into next-morning mail instead

C) Deposit only to the first destination, then mail any remaining buffered output to avoid a long wrap-up

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 7
How aggressively should U-21 remove the legacy hourly/deposit/refund settlement model from the runtime seam?

A) Remove it from the active shift runtime now: `ShiftContext`, state-machine intents, and orchestrator wrap-up should stop carrying refund/debt settlement concepts in U-21 (Recommended)

B) Keep the legacy settlement fields in the runtime through U-21 and defer the cleanup to a later retrofit unit

C) Remove refund calculation, but keep legacy settlement fields around as inert compatibility data inside runtime state for now

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 8
How should U-21 treat the existing sleep-stop behavior while reworking energy-limited runtime execution?

A) Preserve the U-15 operational shape: sleep stops the worker, the current wrap-up path settles buffered output, and U-21 only removes refund semantics from that path (Recommended)

B) Revisit sleep-stop now and make it follow exactly the same finish-current-work-unit flow as natural energy depletion

C) Defer all sleep-stop interaction details until U-23, even if U-21 leaves the runtime seams partially inconsistent in the meantime

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 9
For maintainability and property-test coverage, where should the redesign-era energy/runtime decision logic primarily live?

A) Keep `WorkerEnergyLedger`, work-unit-boundary classification, and energy-depletion transition decisions in pure Core seams, with `ShiftOrchestrator` only translating live world actions into those pure events/results (Recommended)

B) Let `ShiftOrchestrator` own most of the energy and work-unit decision logic directly, with Core holding only simple data records

C) Move more of the decision logic into `FarmhandNpc`, letting the NPC own stamina and stop conditions directly

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/functional-design/business-rules.md`
- `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/functional-design/frontend-components.md`
