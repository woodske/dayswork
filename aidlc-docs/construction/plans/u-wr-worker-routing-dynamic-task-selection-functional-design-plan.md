# Functional Design Plan — U-WR Worker Routing and Dynamic Task Selection

## Unit Context

- **Unit**: U-WR — Worker Routing and Dynamic Task Selection
- **Purpose**: Make the farmhand choose shortest reachable approach tiles, select nearest reachable work inside the active broad batch, and defer/retry temporarily blocked work without collecting unpaid products.
- **Primary requirements**: `FR-WR-01` through `FR-WR-08`, `NFR-WR-02`, `NFR-WR-04`
- **Primary stories**: S-08, S-16, S-19
- **Primary components**: `WorkAreaScanner`, `AnimalTaskHandler`, `WorkerMovementDriver`, `ShiftOrchestrator`, `TaskPriorityOrderer`, routing-focused test helpers.

## Design Approach

The functional design will define:

- A route-cost model for choosing valid interaction tiles.
- A unified active-batch work selection model covering tile work, animal work, and feed work where applicable.
- A blocked-work deferral lifecycle with explicit retry and termination rules.
- Product-collection boundaries so feeding never performs unpaid product work.
- Testable properties required by full PBT enforcement.

## Planning Questions

Please answer each question by filling in the letter after `[Answer]:`. If none fit, choose `X` and describe your preference.

## Question 1
How should route cost be measured when choosing between valid interaction tiles?

A) Count actual reachable path length using the same passability model as worker navigation; unreachable candidates are excluded.
B) Count Manhattan distance, but verify the chosen candidate is reachable before using it.
C) Use actual path length for adjacent-interaction tasks and Manhattan distance only for broad task ordering.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
Inside an active animal-building batch, how should feed steps, animals, and floor products be selected?

A) Combine all currently actionable enabled work into one nearest-reachable pool, except trough placement still requires hay to have been collected from the hopper first.
B) Always do feed work first, then pet/collect animals and floor products by nearest reachable route.
C) Always do pet/collect animals and floor products first, then feed work by nearest reachable route.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3
When a task is blocked and deferred, how many retry passes should happen before skipping it for the day?

A) Retry deferred work until the active batch has no successful progress left; skip whatever remains blocked after a no-progress pass.
B) Retry each deferred task exactly once at the end of the active batch, then skip if still blocked.
C) Retry each deferred task up to two times after progress, then skip if still blocked.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4
What counts as "progress" for allowing another retry pass?

A) Completing a task, collecting a product, placing hay, or otherwise changing a tile/object/animal state in the current batch.
B) Any movement by the worker, even if no task was completed.
C) Only removing a blocking object or floor product from the current location.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5
How should the worker handle equal route lengths across different work categories inside the active batch?

A) Use the existing task-priority order, then stable scan/discovery order.
B) Prefer the same category as the previous task, then task-priority order.
C) Prefer tile work before animal work, then task-priority order.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 6
For property-based testing design, which behavior should be treated as the core invariant?

A) The selector always chooses the minimum reachable route cost and deterministic tie-break winner.
B) The deferral loop always terminates and never drops deferred work before the retry rule allows it.
C) Both A and B are required properties.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Mandatory Functional Design Artifacts

- [x] Create `business-logic-model.md`.
- [x] Create `business-rules.md`.
- [x] Create `domain-entities.md`.
- [x] Mark frontend/UI artifact as N/A because this unit has no UI changes.
- [x] Include a "Testable Properties" section satisfying PBT-01.
- [x] Include extension compliance for Security Baseline and full Property-Based Testing.

## Answer Summary

- **Route cost**: A - actual reachable path length using worker navigation passability.
- **Animal-building selection**: A - combine actionable enabled work into one nearest-reachable pool, with trough placement gated by hopper hay.
- **Blocked retry passes**: A - retry while progress exists; skip blocked remainder after a no-progress pass.
- **Progress definition**: A - task completion or tile/object/animal state change, not movement alone.
- **Equal route lengths**: A - existing task priority, then stable scan/discovery order.
- **PBT core invariant**: A - selector always chooses minimum reachable route cost and deterministic tie-break winner.
- **Ambiguity review**: No contradictions or unresolved ambiguities found. Deferral termination remains a required business rule and example-based test target.

## Execution Checklist

- [x] Load approved worker-routing execution plan.
- [x] Load approved worker-routing requirements and stories.
- [x] Load Functional Design stage rule.
- [x] Load full PBT rules because PBT is enabled in full mode.
- [x] Create this functional design plan with context-appropriate questions.
- [x] Read all user answers from this plan.
- [x] Analyze answers for ambiguity or contradictions.
- [x] Create follow-up clarification questions if needed.
- [x] Generate `business-logic-model.md`.
- [x] Generate `business-rules.md`.
- [x] Generate `domain-entities.md`.
- [x] Update this plan's checkboxes immediately as steps complete.
- [x] Update `aidlc-state.md` to Functional Design review gate.
- [x] Present Functional Design completion for review.

## Extension Compliance

| Extension | Status | Planning-stage compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - no security enforcement for this game mod change. |
| Property-Based Testing | Enabled, full | PBT-01 applies in Functional Design. This plan includes explicit property-identification work before completion. |
