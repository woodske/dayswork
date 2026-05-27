# NFR Design Patterns - U-WR Worker Routing and Dynamic Task Selection

## Purpose

This document translates the approved worker-routing NFR requirements into concrete design patterns for implementation. The selected shape keeps the runtime synchronous and scoped, extracts only the ranking logic needed for strong property tests, and keeps live Stardew world adaptation inside the existing `Dayswork` runtime components.

## Decision Summary

| Design area | Selected answer | Pattern |
|---|---|---|
| Selector extraction | Q1 A | Extract a small pure selector helper that ranks already-evaluated candidates by route cost, task priority, and stable order. |
| Route-cost evaluation | Q2 A | Add or expose a narrow movement-aligned route-cost oracle on `WorkerMovementDriver`. |
| Deferral and retry | Q3 A | Keep orchestration in `ShiftOrchestrator`; isolate pass accounting and defensive finite guard in a tiny helper or bounded method. |
| Route-cache lifetime | Q4 A | Use no cross-selection cache; compute fresh route costs at each task boundary with local memoization only inside one selection call. |
| Test seam priority | Q5 A | Prioritize pure selector/ranking property tests; cover live world adapters and deferral with focused examples. |

## Pattern WR-NFR-01: Candidate Adapter With Pure Selector

### Problem

The worker must choose the nearest reachable task and interaction tile without letting Stardew world object shape, fixed side order, or existing queue order decide the final route.

### Design

Use the live runtime components to build and evaluate candidates, then pass a compact already-evaluated model into a pure selector helper.

The pure selector input should contain only deterministic data:

- Candidate identifier.
- Task kind or task-priority rank.
- Stable scan/discovery order.
- Best reachable interaction tile.
- Best reachable route cost.

The pure selector output should identify the selected candidate and interaction tile. It should not read Stardew world state, inspect maps, mutate candidates, or perform pathfinding.

### Consequences

- Selector behavior is deterministic and easy to test with FsCheck.
- Stardew-specific candidate building remains inside the existing runtime.
- Code generation can use a simple minimum-cost/tie-break oracle for PBT.

## Pattern WR-NFR-02: Movement-Aligned Route-Cost Oracle

### Problem

The selected route must match the path the worker can actually walk. A separate selection-only pathfinder risks disagreement with movement.

### Design

Expose a narrow route-cost operation from `WorkerMovementDriver` or an adjacent helper that uses the same passability assumptions as actual movement. The operation should answer:

- Is the interaction tile reachable from the worker's current tile?
- If reachable, what is the actual path length?

The caller should evaluate every valid interaction tile for a candidate and keep the lowest-cost reachable route.

### Consequences

- The selector and movement driver share one pathing truth.
- Eggs, weeds, hoppers, troughs, and animals can be approached from any reachable valid interaction tile.
- Top/side enumeration order cannot override route length.

## Pattern WR-NFR-03: Per-Selection Route Evaluation

### Problem

Immediate invalidation after progress is required, and stale path costs could cause the worker to miss newly cleared routes.

### Design

Do not maintain a cross-selection or per-location route cache. At each task boundary:

1. Build currently actionable candidates.
2. Enumerate valid interaction tiles.
3. Compute route costs fresh for this selection.
4. Use only local memoization within that one call if the same route query appears more than once.
5. Discard route-cost data before the next selection boundary.

### Consequences

- Newly collected eggs, cleared weeds, moved worker position, and stale animal/tile state are reflected on the next selection.
- Cache invalidation is simple and hard to misuse.
- Performance is bounded by the finite active-batch candidate set and exact route-cost requirement.

## Pattern WR-NFR-04: Active-Batch Retry With Progress Gate

### Problem

Blocked tasks should be retried after other work may clear paths, but retry must not loop forever.

### Design

Keep retry orchestration in `ShiftOrchestrator`, where active batch context and dispatch already live. Isolate the pass accounting and finite guard in a tiny helper or clearly bounded method.

The retry pattern should track:

- Current pass number.
- Whether any progress happened during the pass.
- Count or finite measure of candidates considered.
- Deferred candidates and reasons.
- Whether the defensive max-pass guard has been reached.

The primary stop rule is no-progress pass termination. The defensive guard should be derived from finite candidate count or equivalent finite active-batch measure and should log if it fires.

### Consequences

- Hopper and trough work can retry after egg/product collection clears paths.
- Unreachable work is skipped only after the batch proves no progress can unlock it.
- Implementation remains localized to existing orchestration instead of introducing a new batch engine.

## Pattern WR-NFR-05: Revalidate Before Dispatch

### Problem

Stardew world state can change between candidate discovery, route evaluation, movement, and execution.

### Design

Before executing selected work, revalidate the target:

- Tile still contains the expected actionable object, terrain feature, crop, product, hopper, or trough state.
- Animal still exists at the expected logical target and still needs petting or product collection.
- Feed prerequisite state is still valid.
- Product collection is still authorized by `CollectAnimalProducts`.

If revalidation fails, treat the candidate as stale and continue according to active-batch progress/deferral rules without crashing or falsely completing the work.

### Consequences

- Runtime remains resilient to stale candidates.
- Product authorization cannot be bypassed by retry or feed-routing logic.
- Tests can pin stale-target behavior as a non-crashing outcome.

## Pattern WR-NFR-06: Quiet Maintainer Diagnostics

### Problem

The routing change should not create player-visible noise, but maintainers need enough signal for unexpected blocked work.

### Design

Keep normal gameplay silent. Add narrow debug or maintainer logs only for:

- Deferred candidates skipped after no-progress retry.
- Defensive max-pass guard activation.
- Unexpected route-cost evaluation failure or stale route result.

Do not add player mail, HUD messages, GMCM settings, or broad per-candidate tracing.

### Consequences

- Player experience changes through better worker behavior only.
- Maintainers still get useful evidence when routing cannot complete work.

## Pattern WR-NFR-07: Selector-First Test Strategy

### Problem

The highest-risk algorithmic behavior is the ordering of evaluated candidates by exact route cost and deterministic tie-breaks.

### Design

Use the pure selector helper as the primary property-test seam. Use generated already-evaluated candidate route results, then compare selector output to a simple oracle:

1. Filter unreachable candidates.
2. Pick minimum route cost.
3. Break ties by task-priority rank.
4. Break remaining ties by stable order.

Live Stardew adapters should be covered by focused example tests that mirror reported regressions.

### Consequences

- PBT covers the broad ordering space without requiring SMAPI world construction.
- Concrete regressions remain readable as example tests.
- Deferral PBT remains conditional: if pass accounting becomes a pure helper with observable state, code generation should add a finite-termination property.

## Security Pattern

Security implementation patterns are N/A. Security Baseline is disabled, and this unit does not introduce network behavior, authentication, authorization, PII, file parsing, or external service integration.

## PBT Compliance

| Rule | Status | NFR design compliance |
|---|---|---|
| PBT-01 | Compliant | Functional Design identified selector and deferral properties. |
| PBT-03 | Compliant for design | Selector seam preserves invariant-testability for code generation. |
| PBT-05 | Compliant for design | Selector oracle is explicitly defined as simple minimum-cost/tie-break ordering. |
| PBT-06 | Conditional | Deferral stateful PBT is required only if code generation extracts pure observable deferral state. |
| PBT-07 | Compliant for design | Selector inputs are domain-shaped candidate route results, not primitive-only parameters. |
| PBT-09 | Compliant | FsCheck.Xunit remains selected and already present. |
| PBT-10 | Compliant for design | Example regression tests remain required alongside selector properties. |

## Extension Compliance

| Extension | Status | NFR design compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - no security-sensitive behavior is introduced. |
| Property-Based Testing | Enabled, full | Compliant - selected patterns preserve required FsCheck selector properties and example regression coverage, with conditional deferral PBT if a pure helper is extracted. |
