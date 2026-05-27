# Business Logic Model — U-WR Worker Routing and Dynamic Task Selection

## Purpose

This unit changes how the farmhand chooses and retries work inside the currently active broad batch. The broad batch order stays the same, but the farmhand no longer follows fixed side preferences or separated animal/tile queues when a closer reachable task exists.

## Functional Decisions

| Decision area | Selected behavior |
|---|---|
| Route cost | Count actual reachable path length using the worker navigation passability model. Exclude unreachable candidates. |
| Animal-building batch selection | Combine all currently actionable enabled work into one nearest-reachable pool, except trough placement requires hay from the hopper first. |
| Blocked retry | Retry deferred work until the active batch has no successful progress left; skip remaining blocked work after a no-progress pass. |
| Progress definition | Progress means completing work or changing tile, object, or animal state in the current batch. Movement alone is not progress. |
| Equal route lengths | Use existing task priority, then stable scan/discovery order. |
| PBT core invariant | The selector always chooses the minimum reachable route cost and deterministic tie-break winner. |

## Active Batch Model

The existing broad batch sequence remains authoritative:

1. Animal building work.
2. Outdoor animal work.
3. Greenhouse work.
4. Outdoor crop work.
5. Outdoor clearing work.

Inside one active batch, selectable work is represented as a set of candidates rather than as separated FIFO queues.

### Candidate Families

- **Tile candidate**: Work against a tile, such as egg/product pickup, weed clearing, rock clearing, tree work, crop work, or trough placement.
- **Animal candidate**: Work against a live animal, such as petting or tool-harvest collection.
- **Feed hopper candidate**: Work against the hopper tile to pick up hay.
- **Feed trough candidate**: Work against a trough tile to place hay. This candidate is actionable only after hay is in hand.

## Route-Cost Selection Flow

1. Build all currently actionable candidates for the active batch.
2. For each candidate, enumerate every valid interaction tile.
3. Use worker navigation passability to compute actual route length from the worker's current tile to each interaction tile.
4. Drop candidates with no reachable interaction tile.
5. For each candidate, keep the reachable interaction tile with the lowest route cost.
6. Select the candidate with the lowest route cost.
7. Break equal route-cost ties with existing task priority.
8. Break remaining ties with stable scan/discovery order.
9. Dispatch the chosen candidate through the existing movement and task execution flow.

If the worker is already on a valid interaction tile, the route cost is zero and the worker performs the task from that tile.

## Feed Work Flow

Animal-building feed work has one prerequisite: hay must be collected before trough placement.

1. If the building does not need feed, no feed candidates are emitted.
2. If hay is not in hand, emit the feed hopper candidate.
3. If the hopper is unreachable, defer the hopper candidate and continue with other actionable enabled candidates.
4. If hay is collected, emit reachable trough placement candidates for remaining empty trough slots.
5. Trough candidates compete in the same nearest-reachable pool as other active candidates.
6. If products block feed routes and `CollectAnimalProducts` is enabled, product candidates can clear those blockers through normal paid product collection.
7. If products block feed routes and `CollectAnimalProducts` is disabled, products are not collected, and feed work may remain incomplete.

## Deferral and Retry Flow

Deferral is scoped to the active batch.

1. A candidate is deferred when it is not currently reachable or its movement fails.
2. The active batch continues selecting from remaining currently actionable candidates.
3. The batch records whether any progress happened during the pass.
4. When no immediately reachable candidate remains, deferred candidates are re-evaluated if progress occurred in the pass.
5. If a deferred candidate is now reachable, it returns to normal selection.
6. If no progress occurred during a pass, all still-blocked deferred candidates are skipped for the day.
7. The batch completes after all reachable work is done and all deferred work is either completed or skipped.

This guarantees termination because every pass either completes at least one unit of work, changes world state, or ends with no progress and skips the remaining blocked set.

## Product Collection Boundary

`CollectAnimalProducts` remains the authorization boundary for eggs and other products.

- If enabled, floor products are normal paid tile candidates and can clear paths naturally.
- If disabled, products are blockers, not work. The worker must not collect them to make feed work possible.
- No special unpaid product-routing path is introduced.

## Integration With Existing Components

| Existing component | Functional responsibility in U-WR |
|---|---|
| `WorkAreaScanner` | Detect tile candidates and enumerate valid interaction tiles without fixed side preference. |
| `AnimalTaskHandler` | Expose animal interaction candidates and feed candidates for the active building/location. |
| `WorkerMovementDriver` | Provide route length/reachability behavior consistent with worker navigation. |
| `ShiftOrchestrator` | Own active-batch selection, deferral, retry, execution dispatch, and progress tracking. |
| `TaskPriorityOrderer` | Remain the deterministic tie-breaker for equal route lengths. |

## Testable Properties

### PBT-01 Property Identification

The unit contains algorithmic selection and retry behavior, so PBT-01 applies.

| Component/behavior | Property category | Property |
|---|---|---|
| Active-batch selector | Invariant | For every generated candidate set with at least one reachable candidate, the selected candidate has the minimum route cost. |
| Tie-break ordering | Invariant | For equal route costs, the selected candidate is the deterministic task-priority then stable-order winner. |
| Unreachable filtering | Invariant | A candidate with no reachable interaction tile is never selected while any reachable candidate exists. |
| Current-tile interaction | Invariant | If the worker's current tile is a valid interaction tile, that candidate has route cost zero. |

### Deferral Testing

The user selected selector minimum-cost behavior as the core PBT invariant. Deferral termination still remains a required business rule and must have example-based coverage. If implementation extracts a pure deferral helper, a property test should also verify that finite candidate sets terminate after a no-progress pass.

## Extension Compliance

| Extension | Status | Functional-design compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - no security behavior is introduced. |
| Property-Based Testing | Enabled, full | Compliant - PBT-01 properties are identified and must be carried into code generation planning. |
