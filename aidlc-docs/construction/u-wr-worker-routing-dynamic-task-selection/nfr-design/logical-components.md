# Logical Components - U-WR Worker Routing and Dynamic Task Selection

## Purpose

This document defines the logical component shape for implementing the approved NFR design. It is intentionally small: no new infrastructure, persistence, UI, background worker, or broad service layer is introduced.

## Component Overview

| Logical component | Implementation home | Responsibility |
|---|---|---|
| Active batch candidate adapters | `WorkAreaScanner`, `AnimalTaskHandler`, `ShiftOrchestrator` | Convert currently actionable Stardew world state into candidates and interaction tiles. |
| Route-cost oracle | `WorkerMovementDriver` or adjacent movement helper | Return reachable path length using the same passability assumptions as worker movement. |
| Route evaluator | `ShiftOrchestrator` or small internal helper | Evaluate candidate interaction tiles through the route-cost oracle and produce best reachable route results. |
| Pure route selector | New small helper in existing runtime/Core testable area | Choose the minimum-cost reachable candidate with deterministic tie-breaks. |
| Active-batch retry coordinator | `ShiftOrchestrator` with tiny pass-accounting helper or bounded method | Track deferred work, progress, retry passes, no-progress termination, and defensive guard. |
| Dispatch and revalidation adapter | Existing task execution paths | Revalidate target state and execute selected work through current behavior. |
| Routing tests | `Dayswork.Tests` | Add xUnit regression tests and FsCheck selector properties. |

## Component: Active Batch Candidate Adapters

### Responsibilities

- Build candidates only for currently enabled and authorized work.
- Enumerate all valid interaction tiles for tile, animal, hopper, and trough work.
- Preserve stable scan/discovery order for final tie-breaks.
- Keep `CollectAnimalProducts` as the boundary for product candidates.
- Exclude trough placement until hay is in hand.

### Non-Responsibilities

- Do not choose the final candidate by fixed side order.
- Do not use Manhattan distance as the final selection rule.
- Do not collect products as a side effect of feed routing when product collection is disabled.

### Existing Component Fit

- `WorkAreaScanner` should own tile candidate discovery and valid interaction tiles for crops, products, weeds, rocks, trees, and similar tile work.
- `AnimalTaskHandler` should own animal, hopper, and trough candidate discovery.
- `ShiftOrchestrator` should combine the current active batch's candidates into the selection flow.

## Component: Route-Cost Oracle

### Responsibilities

- Answer reachability from the worker's current tile to an interaction tile.
- Return actual path length for reachable interaction tiles.
- Match the same passability assumptions used when the worker moves.

### Non-Responsibilities

- Do not rank candidates by task priority or stable order.
- Do not perform task revalidation or execution.
- Do not maintain long-lived route caches.

### Existing Component Fit

`WorkerMovementDriver` is the preferred home because it already owns worker navigation behavior. If implementation uses an adjacent helper, it must remain aligned with movement passability and be called through a narrow route-cost contract.

## Component: Route Evaluator

### Responsibilities

- For each candidate, evaluate each valid interaction tile through the route-cost oracle.
- Drop candidates with no reachable interaction tile from current selection.
- Keep the lowest-cost reachable interaction tile for each candidate.
- Use only local memoization inside one selection call when duplicate route queries exist.
- Discard all route-cost data after the selection boundary.

### Non-Responsibilities

- Do not cache route costs across task boundaries.
- Do not skip a candidate because route evaluation is expensive.
- Do not choose among equal-cost candidates without the pure selector.

## Component: Pure Route Selector

### Responsibilities

- Select the candidate with the minimum reachable route cost.
- Resolve equal route costs by task-priority rank.
- Resolve remaining ties by stable order.
- Return no selection when no reachable candidate exists.

### Non-Responsibilities

- Do not read Stardew world state.
- Do not compute path lengths.
- Do not mutate batch state.
- Do not perform movement or work execution.

### Testability

This is the primary FsCheck seam for:

- Minimum route-cost invariant.
- Deterministic tie-break invariant.
- Unreachable filtering invariant.
- Current-tile zero-cost invariant when route evaluation provides zero route cost.
- Oracle comparison against simple minimum-cost/tie-break ordering.

## Component: Active-Batch Retry Coordinator

### Responsibilities

- Keep deferral scoped to the current active batch.
- Track whether progress occurred during a pass.
- Retry deferred candidates after progress.
- End retry when a pass makes no progress.
- Apply the defensive max-pass guard derived from finite candidate count or equivalent finite measure.
- Produce narrow maintainer/debug logs when blocked work is skipped or the guard fires.

### Non-Responsibilities

- Do not reorder work across broad batches.
- Do not move product collection outside the enabled task boundary.
- Do not charge stamina for route evaluation or movement.

### Existing Component Fit

`ShiftOrchestrator` remains the owner of retry orchestration because it already coordinates active work, movement, dispatch, and progress. A tiny helper or bounded method should isolate pass accounting and the finite guard to avoid burying retry termination in large queue-draining branches.

## Component: Dispatch And Revalidation Adapter

### Responsibilities

- Revalidate selected tile, animal, hopper, or trough target immediately before execution.
- Treat stale targets as non-crashing outcomes.
- Route movement failure into deferral/retry behavior.
- Preserve existing stop conditions: sleep, energy exhaustion, hard cap, stuck abort, and no-work-day behavior.
- Preserve task-owned output routing, buffering, deposit, and overflow mail.

### Non-Responsibilities

- Do not silently complete stale work.
- Do not bypass task authorization.
- Do not create new output routing paths.

## Component: Routing Tests

### Responsibilities

- Add xUnit examples for the reported gameplay regressions.
- Add FsCheck selector properties using domain-shaped candidate route generators.
- Reuse existing FsCheck.Xunit dependency and seed/replay behavior.
- Add deferral property tests if pass accounting becomes a pure observable helper.

### Suggested Test Organization

- `Dayswork.Tests/UWR/WorkerRouteSelectorPropertyTests.cs`
- `Dayswork.Tests/UWR/WorkerRouteSelectorTests.cs`
- `Dayswork.Tests/UWR/WorkerRoutingRegressionTests.cs`
- `Dayswork.Tests/UWR/UWRPropertyGenerators.cs`

The exact filenames may vary during code generation to match local patterns.

## Data Flow

1. Active batch adapters discover currently actionable candidates.
2. Route evaluator asks the movement-aligned oracle for each interaction tile's reachability and route cost.
3. Route evaluator keeps each candidate's best reachable route.
4. Pure selector chooses the minimum-cost candidate and deterministic tie-break winner.
5. Dispatch adapter revalidates the target and starts movement/execution.
6. Progress, stale-target, or movement-failure outcomes update active-batch retry state.
7. The next task boundary rebuilds candidates and route costs from fresh world state.

## Explicitly Not Added

- No background worker, async route computation, queue service, circuit breaker, or external cache.
- No save schema changes.
- No config or GMCM controls.
- No player-facing messages or mail.
- No security-specific infrastructure.
- No separate selection-only pathfinding stack that can diverge from worker movement.

## PBT Compliance

| Rule | Status | Logical-component compliance |
|---|---|---|
| PBT-01 | Compliant | Properties from Functional Design map to the pure route selector and optional retry helper. |
| PBT-03 | Compliant for design | Selector invariants have a dedicated logical component. |
| PBT-05 | Compliant for design | Pure selector output can be checked against a simple oracle. |
| PBT-06 | Conditional | Active-batch retry PBT is required if code generation extracts pure observable retry state. |
| PBT-07 | Compliant for design | Candidate route results are domain-shaped generator targets. |
| PBT-09 | Compliant | Existing FsCheck.Xunit stack remains the selected PBT tool. |
| PBT-10 | Compliant for design | Routing tests include both example regressions and property tests. |

## Extension Compliance

| Extension | Status | Logical-component compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - no security-sensitive component is added. |
| Property-Based Testing | Enabled, full | Compliant - logical components isolate the selector seam required for FsCheck properties and keep example tests mandatory for reported regressions. |
