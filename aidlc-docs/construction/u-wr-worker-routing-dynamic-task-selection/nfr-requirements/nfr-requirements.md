# NFR Requirements - U-WR Worker Routing and Dynamic Task Selection

## Purpose

This document defines the quality requirements for the worker-routing implementation. The functional behavior is route-sensitive, stateful, and user-visible, so the NFR bar focuses on exactness, determinism, bounded retry, testability, and quiet gameplay operation.

## Answer Summary

| Decision area | Selected answer | NFR requirement |
|---|---|---|
| Route-cost performance | Q1 A | Evaluate exact path length for all currently valid candidates and interaction tiles. Optimization is limited to short-lived caching and reuse. |
| Invalidation | Q2 A | Recompute route costs after every progress event so newly cleared paths are considered immediately. |
| Runtime architecture | Q3 A | Stay synchronous inside the existing SMAPI shift loop with deterministic in-memory route selection. |
| Retry reliability | Q4 A | Use the no-progress pass rule plus a defensive max-pass guard derived from finite candidate count. |
| Observability | Q5 A | Keep gameplay silent; add maintainer/debug logs only for skipped blocked sets and unexpected route failures. |
| Test-quality bar | Q6 A | Add regression examples for reported cases plus FsCheck selector properties; add deferral PBT if a pure helper is extracted. |

## Performance Requirements

### NFR-WR-PERF-01 Exact route cost remains authoritative

The implementation must compute actual reachable path length for every currently valid candidate interaction tile before selecting work. Manhattan distance, fixed side order, or task-tile distance can be used only as pre-filtering or ordering hints that do not remove valid work and do not decide the final selected candidate.

### NFR-WR-PERF-02 Short-lived route caching is allowed

Route-cost caching may be used only within a stable route-evaluation point or other short-lived scope where the worker position, candidate set, and passability-relevant world state have not changed. Cached route costs must not survive a progress event that can change passability or candidate validity.

### NFR-WR-PERF-03 Progress invalidates route costs immediately

After any task completion or world-state change, the next selection must rebuild route-cost data so newly reachable eggs, products, hoppers, troughs, animals, weeds, or other tasks can be considered immediately.

### NFR-WR-PERF-04 No approximation-based task loss

The worker must not skip or permanently defer work because an approximation says another candidate is closer, because a route budget was exceeded, or because one preferred interaction side is blocked. Only actual reachability and route-cost evaluation can exclude a candidate from the current selection.

## Reliability Requirements

### NFR-WR-REL-01 Deferred work must terminate

The deferral loop must terminate for every finite active batch. The primary termination rule is the approved no-progress pass: if a pass completes no work and changes no tile, object, or animal state, remaining blocked deferred candidates are skipped for the day.

### NFR-WR-REL-02 Defensive max-pass guard is required

In addition to the no-progress rule, implementation must include a defensive guard derived from the finite candidate count or equivalent finite batch measure. The guard exists to prevent accidental infinite loops caused by stale candidate rebuilding, repeated movement failure, or an implementation bug.

### NFR-WR-REL-03 Movement failure is recoverable

If movement to a selected interaction tile fails, the candidate must be deferred or re-evaluated according to the active-batch retry rules rather than causing a crash, abandoning the whole broad batch, or falsely completing the task.

### NFR-WR-REL-04 Stale targets are revalidated

Tile, animal, hopper, and trough candidates must be revalidated before execution. If the target is no longer valid, the selection loop must treat that as a non-crashing stale-candidate outcome and continue according to the progress and deferral rules.

## Determinism Requirements

### NFR-WR-DET-01 Selection must be deterministic

For the same worker position, candidate set, route-cost oracle, task-priority order, and stable scan order, the selector must choose the same candidate and interaction tile every time.

### NFR-WR-DET-02 Tie-breaks must be stable

Equal route costs must be resolved first by existing task priority and then by stable scan/discovery order. Candidate enumeration may be deterministic, but side-enumeration order must not override route cost.

### NFR-WR-DET-03 Runtime execution stays synchronous

Route selection and deferral decisions must remain synchronous within the existing SMAPI shift loop. The unit must not introduce background path-computation jobs, async route mutation, or cross-thread world access.

## Maintainability Requirements

### NFR-WR-MAINT-01 Pure helper seams are preferred

Selector behavior, route-result ranking, and finite deferral logic should be isolated behind small testable helper types or methods where possible. The live SMAPI shell should adapt Stardew world state into candidates and passability checks, not bury the ranking rules inside large imperative branches.

### NFR-WR-MAINT-02 Existing component boundaries should remain recognizable

The implementation should preserve the existing responsibilities:

- `WorkAreaScanner` detects tile candidates and interaction tiles.
- `AnimalTaskHandler` exposes animal, hopper, and trough work.
- `WorkerMovementDriver` provides route reachability and path-length behavior consistent with movement.
- `ShiftOrchestrator` coordinates active-batch selection, dispatch, deferral, retry, and progress tracking.
- `TaskPriorityOrderer` remains the tie-break authority for equal route lengths.

### NFR-WR-MAINT-03 No persistence or UI surface is introduced

The unit must not add save data, config settings, GMCM controls, player-facing text, or menus. Routing state is per-shift or per-active-batch runtime state only.

## Observability Requirements

### NFR-WR-OBS-01 Normal gameplay remains quiet

The player should not see new messages, menus, popups, or mail because route selection became smarter. Visible behavior changes through the worker's movement and task completion only.

### NFR-WR-OBS-02 Maintainer logs are narrow

Debug or maintainer logs should be limited to cases that help diagnose routing failures:

- Deferred candidates skipped after a no-progress pass.
- Defensive max-pass guard activation.
- Unexpected route-evaluation failure or stale route result.

Detailed per-candidate trace logging is not required and should not be added unless it is gated behind existing debug-level logging and needed during implementation.

## Security, Availability, And Scalability

### Security

Security Baseline is disabled for this change. The unit introduces no network calls, authentication, authorization, PII, file parsing, or external service integration.

### Availability

Traditional uptime is N/A for a local SMAPI mod. Runtime availability means the shift loop must continue safely when a candidate becomes blocked, stale, or unreachable.

### Scalability

The unit scales to the finite candidate counts of the current active Stardew location or building. Exact route evaluation is required for all currently valid candidates; scalability is managed through candidate validity filtering, short-lived route reuse, and immediate invalidation after progress.

## Testing Requirements

### NFR-WR-TEST-01 Example regression coverage

Code generation must add example-based tests for the reported regressions:

- Worker standing next to a task should use the shortest reachable side instead of walking around to a preferred side.
- Egg/product collection should succeed when any valid side is reachable, even if the top side is blocked.
- Animal work inside and outside should prefer nearer reachable animals within the active batch instead of walking past them to far targets.
- Feed work should defer blocked hopper/trough routes and retry after enabled product collection can clear paths.
- Product collection must not happen when `CollectAnimalProducts` is disabled, even if products block feed.

### NFR-WR-TEST-02 FsCheck selector properties

Code generation must add or preserve FsCheck property tests for these selector invariants:

- Selected reachable candidate has the minimum actual route cost.
- Equal route costs resolve by task priority, then stable order.
- Candidates with no reachable interaction tile are not selected while any reachable candidate exists.
- A candidate with the current worker tile as a valid interaction tile has route cost zero.

### NFR-WR-TEST-03 Deferral coverage

Deferral termination must have example-based coverage. If implementation extracts a pure deferral helper, code generation should add a FsCheck property proving finite deferral passes terminate after a no-progress pass or bounded finite progress sequence.

### NFR-WR-TEST-04 PBT generator quality

Routing property tests must use domain-shaped generators for candidates, interaction routes, route costs, task priority ranks, stable order, and reachability. Raw primitive-only properties are not sufficient for this unit.

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-01 | Compliant from Functional Design | Testable selector and deferral properties are documented in the functional design artifacts. |
| PBT-02 | N/A | No round-trip operation is introduced by this unit. |
| PBT-03 | Applicable later | Selector invariants are required during code generation. |
| PBT-04 | N/A | No idempotent operation is introduced as a unit requirement. |
| PBT-05 | Applicable later if helper exposes oracle | Selector tests may compare against a simple sort/minimum oracle during code generation. |
| PBT-06 | Applicable later if pure deferral helper is extracted | Stateful deferral PBT becomes required if the implementation creates a pure state-machine helper. |
| PBT-07 | Applicable later | Routing PBT must use domain-shaped generators. |
| PBT-08 | Applicable later | Build/Test instructions must include FsCheck seed logging and reproducibility. |
| PBT-09 | Compliant | FsCheck.Xunit is already selected and present in `Dayswork.Tests.csproj`. |
| PBT-10 | Applicable later | Example regression tests must complement selector properties. |

## Extension Compliance

| Extension | Status | NFR requirements compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - no security-sensitive behavior is introduced. |
| Property-Based Testing | Enabled, full | Compliant - FsCheck.Xunit is selected and already present; downstream selector, generator, reproducibility, and example/PBT complement obligations are documented. |
