# Business Rules — U-WR Worker Routing and Dynamic Task Selection

## Route Selection Rules

### BR-WR-01 Actual route cost is authoritative

The worker must choose among candidates using actual reachable path length from the worker's current tile. Manhattan distance is not sufficient for final selection.

### BR-WR-02 Unreachable candidates are excluded from current selection

A candidate with no reachable interaction tile cannot be selected while another reachable candidate exists in the active batch.

### BR-WR-03 Current valid side wins with zero cost

If the worker is already standing on a valid interaction tile for a candidate, that candidate has route cost zero for that interaction tile.

### BR-WR-04 Fixed side order cannot decide the selected stand tile

Candidate enumeration can be deterministic, but top/bottom/left/right order cannot override actual route length.

### BR-WR-05 Equal route lengths use deterministic tie-breaks

When route lengths are equal, selection must use:

1. Existing task-priority order.
2. Stable scan/discovery order.

## Active Batch Rules

### BR-WR-06 Broad batch order remains unchanged

This unit does not reorder work globally across animal building, outdoor animal, greenhouse, outdoor crop, and outdoor clearing batches.

### BR-WR-07 Active batch work uses one candidate pool

Within the active batch, tile work, animal work, and actionable feed work are selected from one nearest-reachable candidate pool.

### BR-WR-08 Animal work must be revalidated before execution

Before petting or collecting from an animal, the worker must re-check that the animal is still present and still needs the selected work.

### BR-WR-09 Tile work must be revalidated before execution

Before performing tile work, the worker must re-check that the tile still contains the expected actionable work.

## Feed and Product Rules

### BR-WR-10 Hay collection gates trough placement

Trough placement cannot be actionable until hay has been collected from the hopper.

### BR-WR-11 Hopper and trough blockers defer feed work

If a hopper or trough route is blocked, the feed candidate is deferred instead of permanently skipped while the active batch still has progress opportunities.

### BR-WR-12 Product collection requires task authorization

Eggs and other animal products can be collected only when `CollectAnimalProducts` is enabled for the contract.

### BR-WR-13 Unpaid product clearing is forbidden

The worker must not collect, move, or delete products as a side effect of feeding when `CollectAnimalProducts` is disabled.

### BR-WR-14 Products may naturally clear feed paths when enabled

When `CollectAnimalProducts` is enabled, collected products can change passability and allow deferred feed work to succeed on a later retry.

## Deferral and Retry Rules

### BR-WR-15 Deferral is active-batch scoped

Deferred work belongs to the current active batch and is resolved before the batch completes.

### BR-WR-16 Progress unlocks another retry pass

Deferred work is retried after a pass with progress. Progress means completing work or changing tile, object, or animal state in the current batch.

### BR-WR-17 Movement alone is not progress

Worker movement without task completion or world-state change does not justify another retry pass.

### BR-WR-18 No-progress pass ends retry

If the active batch has a pass with no successful progress, remaining blocked deferred candidates are skipped for the day.

### BR-WR-19 Retry cannot loop forever

The retry design must guarantee that every pass either makes progress or terminates the deferred set for the current batch.

## Preservation Rules

### BR-WR-20 Stamina remains labor-only

Route evaluation and movement do not spend worker stamina. Stamina is spent only on labor beats as already approved.

### BR-WR-21 Output routing remains task-owned

Changing route selection must not change destination routing, buffering, deposit, or overflow mail behavior.

### BR-WR-22 Existing stop conditions remain authoritative

Sleep, energy exhaustion, hard-cap, stuck-abort, and no-work-day behavior remain unchanged except that reachable work should no longer be falsely skipped.

## Testable Properties

| Rule area | Property category | Required coverage |
|---|---|---|
| Route selection | Invariant | Selected reachable candidate has minimum actual route cost. |
| Tie-breaks | Invariant | Equal-cost candidate selection follows task priority and stable order. |
| Unreachable handling | Invariant | Unreachable candidates are skipped while reachable candidates exist. |
| Deferral termination | Invariant or example-based | Finite batches cannot retry forever; no-progress pass skips blocked remainder. |

## Extension Compliance

| Extension | Status | Functional-design compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - no security behavior is introduced. |
| Property-Based Testing | Enabled, full | Compliant - PBT-01 properties are identified for route selection and tie-break rules, with deferral termination required at least as example-based coverage. |
