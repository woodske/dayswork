# Worker Routing and Dynamic Task Selection Requirements

## Intent Analysis

- **User Request**: Improve worker pathing so the worker chooses the shortest valid approach to tasks, does nearby eligible work before walking past it, and retries temporarily blocked tasks after other work may have cleared the path.
- **Request Type**: Gameplay bug fix and runtime routing enhancement.
- **Scope Estimate**: Multiple runtime components, primarily `WorkAreaScanner`, `AnimalTaskHandler`, `ShiftOrchestrator`, worker movement/path helpers, and focused route-ordering tests.
- **Complexity Estimate**: Moderate. The implementation touches route-cost selection, per-batch task ordering, animal work, feed/hopper work, and retry semantics.

## Answer Summary

| Question | Decision |
|---|---|
| Q1 | Use the shortest reachable route to any valid interaction tile using the worker navigation passability rules. |
| Q2 | Keep broad batch boundaries, but pick the closest reachable task inside the active batch. |
| Q3 | Temporarily defer blocked tasks and retry them after other same-location or same-batch work may change the world. |
| Q4 | Do not collect animal products unless `CollectAnimalProducts` is enabled; feeding may remain blocked if products block the route. |
| Q5 | Use the existing deterministic task priority order as the tie-breaker for equal path lengths. |
| Q6 | Security Baseline remains disabled for this game mod change. |
| Q7 | Property-Based Testing is fully enforced for this change. |

## Functional Requirements

### FR-WR-01 Shortest Valid Approach Tile

For any task that can be performed from multiple adjacent or interaction tiles, the worker must choose the reachable approach tile with the shortest actual route from the worker's current tile.

- The route check must use the same passability semantics as worker navigation.
- The worker must not prefer top, bottom, left, or right by hardcoded candidate order when another reachable side is closer.
- If the worker is already standing on a valid interaction tile, the worker must perform the task from that tile instead of walking around the object.
- If only one side is reachable, the worker must use that side.

### FR-WR-02 Active Batch Nearest-Task Selection

Within the currently active broad batch, the worker must choose the closest reachable next task rather than blindly following scan order or animal queue order.

Broad batch boundaries remain in effect:

- Animal building batch
- Outdoor animals batch
- Greenhouse batch
- Outdoor crops batch
- Outdoor clearing batch

The worker is not required to globally reorder work across those broad batches for this change.

### FR-WR-03 Animal Work Locality

Animal tasks inside the active animal batch must be selected by reachable route distance from the worker's current tile.

- The worker should not walk past a closer animal needing attention to service a farther animal in the same active batch.
- Pet and collect work for animals must still be revalidated before execution, since animal state can change during the shift.
- Equal-distance animal work uses existing deterministic task priority as the tie-breaker.

### FR-WR-04 Product and Egg Collection Locality

Animal product floor items, such as eggs, must be considered as tile work within the relevant active batch when `CollectAnimalProducts` is enabled.

- Eggs and other product tiles must choose the shortest reachable approach tile.
- A product must not be abandoned solely because one side of the product tile is blocked if another valid side is reachable.
- Product collection must be retried later in the batch if it was temporarily blocked and other enabled work may have cleared the path.

### FR-WR-05 Feed/Hopper Retry Semantics

Feed work must defer and retry temporarily blocked feed steps.

- If the hopper or trough path is blocked, the feed step is deferred instead of permanently skipped.
- Deferred feed work is retried after other enabled same-building work completes.
- If `CollectAnimalProducts` is not enabled, the worker must not collect eggs or other products merely to unblock feeding.
- If products still block feeding after all enabled work is complete, feeding may remain incomplete for the day.

### FR-WR-06 Blocked Task Deferral

Navigation failures for normal work items must create a temporary deferral, not an immediate permanent skip, when there is remaining work in the active batch.

- Deferred work is retried after other work in the same location or batch has had a chance to change passability.
- A task that remains unreachable after retry can be skipped for the day.
- Retry behavior must avoid infinite loops when no progress is possible.

### FR-WR-07 Deterministic Tie-Breaking

When multiple reachable tasks have the same route length, selection must be deterministic.

Tie-break order:

1. Existing task priority order.
2. Stable location/task scan order where task priority is identical.

### FR-WR-08 Current Behavior Preservation

The routing change must preserve existing approved runtime boundaries unless explicitly changed here.

- Worker stamina is still spent only on labor beats, not movement.
- Broad batch order remains unchanged.
- Task capability checks remain authoritative.
- Output routing, buffering, deposit, and overflow mail behavior remain task-owned.
- Sleeping, hard-cap stop, stuck-abort, and no-work-day behavior remain unchanged except where routing avoids false skips.

## Non-Functional Requirements

### NFR-WR-01 Performance

Route-cost evaluation must stay lightweight enough for SMAPI tick execution.

- Prefer bounded route checks and reuse existing worker path/passability helpers where possible.
- Avoid rescanning entire large locations on every tick.
- Reorder work at task boundaries, not continuously during movement.

### NFR-WR-02 Determinism

For the same world state, worker tile, enabled tasks, tool snapshot, and contract scope, task selection must produce the same result.

### NFR-WR-03 Reliability

The worker must not abandon reachable work because a preferred side, preferred hopper path, or initially chosen animal route is blocked.

### NFR-WR-04 Testability

Shortest-route selection, tie-breaking, and blocked-task deferral should be factored into testable helpers where practical.

- Example-based tests must cover the user-reported regressions.
- Property-based tests are required where route ordering or deferral invariants can be modeled without SMAPI runtime objects.

### NFR-WR-05 Security

Security Baseline is disabled for this change. No network, authentication, PII, or privileged filesystem behavior is introduced.

## Acceptance Criteria

- A worker standing next to a weed, rock, tree, fruit tree, egg, or comparable adjacent-interaction task performs it from the current valid side when possible.
- A worker does not skip an egg or product just because the top side is blocked when another side is reachable.
- In one active barn/coop batch, the worker services closer reachable animal work before walking to farther animal work.
- Feed work blocked by eggs is retried after enabled product collection or other enabled work has had a chance to clear paths.
- If product collection is not enabled, feeding may remain blocked by eggs; the worker must not silently collect unpaid product work.
- A permanently unreachable task does not trap the worker in an infinite retry loop.
- Existing build and test commands remain green after implementation.

## Out of Scope

- Global nearest-task routing across all broad batches.
- Collecting animal products when the contract did not enable animal-product collection.
- Multiple concurrent workers or multi-contract route optimization.
- New player-facing UI or GMCM controls.
- New mail or notification behavior for skipped blocked tasks.

## Extension Compliance

| Extension | Status | Requirements-stage compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - user selected no enforcement for this game mod change. |
| Property-Based Testing | Enabled, full | Applicable later in Functional Design, Code Generation, and Build/Test. No blocking PBT requirement applies directly to Requirements Analysis, but the full enforcement decision is recorded and must be honored in later stages. |
