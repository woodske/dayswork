# Worker Routing Requirement Verification Questions

Please answer each question by filling in the letter choice after the `[Answer]:` tag. If none of the options match your preference, choose `X` and describe the behavior you want after the tag.

## Context Summary

The current implementation has these routing constraints:

- Object approach tiles are chosen from fixed directional candidate lists instead of by route cost from the worker's current position.
- Outdoor tile work is greedily ordered by task tile distance, but animal work and batch ordering still use fixed priority groups.
- Animal work is queued before tile work inside a batch, so the worker can pass closer actionable animals or egg/product tiles.
- Feed work tries the hopper first and skips failed navigation instead of retrying after product collection or other work changes passability.
- A failed navigation attempt generally skips the task permanently for the current shift.

## Question 1
When choosing where to stand for one task tile or animal, how strict should "shortest path" be?

A) Use the shortest reachable route to any valid interaction tile using the same passability rules as worker navigation.
B) Use Manhattan distance to pick a side, but still require the chosen side to be reachable.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
How broadly should nearest-task selection override the current task/batch priority order?

A) Across all currently actionable work in the same location, pick the closest reachable next task regardless of task type.
B) Keep broad batches such as animal building, outdoor animals, greenhouse, outdoor crops, and outdoor clearing, but pick the closest reachable task inside the active batch.
C) Keep animal work before crop/clearing work, but pick the closest reachable animal or animal product before moving to the next animal task.
X) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 3
How should blocked tasks be retried?

A) Temporarily defer blocked tasks and retry them after other work in the same location or batch changes the world.
B) Immediately rescan the current location after any task completes, so newly reachable tasks can be reconsidered before the worker leaves.
C) Retry blocked tasks only once at the end of the current batch, then skip them for the day if still blocked.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4
For feeding animals when eggs or other products block the hopper path, what should happen if "Collect Animal Products" is not enabled for the contract?

A) Do not collect products unless the player enabled the task; feeding may stay blocked.
B) Allow the worker to collect blocking animal products only when needed to reach feed work, and route them using the normal animal-product destination rules.
C) Allow the worker to move or clear blockers for navigation, but do not count that as paid animal-product collection.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5
What tie-breaker should the worker use when multiple reachable tasks have the same path length?

A) Preserve the existing deterministic task priority order for ties.
B) Prefer tasks in the same building or area before crossing doors or changing locations.
C) Prefer the stable scan order so repeated runs are predictable, even if another tie-breaker might look more natural.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 6
Should security extension rules be enforced for this project?

A) Yes — enforce all SECURITY rules as blocking constraints (recommended for production-grade applications)
B) No — skip all SECURITY rules (suitable for PoCs, prototypes, and experimental projects)
X) Other (please describe after [Answer]: tag below)

[Answer]: B, this is a game mod no security is needed

## Question 7
Should property-based testing (PBT) rules be enforced for this project?

A) Yes — enforce all PBT rules as blocking constraints (recommended for projects with business logic, data transformations, serialization, or stateful components)
B) Partial — enforce PBT rules only for pure functions and serialization round-trips (suitable for projects with limited algorithmic complexity)
C) No — skip PBT rules (suitable for simple CRUD applications, UI-only projects, or thin integration layers with no significant business logic)
X) Other (please describe after [Answer]: tag below)

[Answer]: A
