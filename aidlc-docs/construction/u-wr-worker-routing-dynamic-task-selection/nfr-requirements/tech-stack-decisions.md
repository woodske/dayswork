# Tech Stack Decisions - U-WR Worker Routing and Dynamic Task Selection

## Decision Summary

This unit keeps the existing Dayswork runtime and test stack. The main technical decision is not to introduce new infrastructure or async machinery for routing. Instead, the implementation should expose small deterministic route-selection seams that can be tested with the existing xUnit and FsCheck setup.

| Area | Decision | Rationale |
|---|---|---|
| Runtime platform | Keep existing C# / .NET 6 SMAPI mod runtime. | The change is local worker behavior inside the existing Stardew Valley mod. |
| Execution model | Keep synchronous in-memory selection inside the existing shift loop. | Avoids cross-thread Stardew world access and preserves deterministic gameplay behavior. |
| Route evaluation | Reuse or expose existing worker navigation passability and path-length behavior. | The selected task must match the route the worker can actually walk. |
| Selector implementation | Prefer small internal helper types or methods over a new top-level service. | Keeps the change scoped while making route ordering and deferral testable. |
| Persistence | No new save data or migration. | Routing state is runtime-only and should not affect contract schema. |
| UI/config | No new GMCM or player-facing UI. | The requested behavior should work automatically. |
| Example tests | xUnit. | Existing project test runner and regression-test pattern. |
| Property-based tests | FsCheck.Xunit 2.16.5. | Already present in `Dayswork.Tests.csproj` and used across the project. |

## Existing Stack Confirmation

The test project already includes:

- `Microsoft.NET.Test.Sdk` 17.8.0
- `xunit` 2.6.2
- `xunit.runner.visualstudio` 2.5.4
- `FsCheck.Xunit` 2.16.5
- `coverlet.collector` 6.0.0

No dependency addition is required for PBT-09 unless code generation discovers a project-file drift before implementation.

## Routing Implementation Direction

The preferred implementation shape is:

1. Convert currently actionable work into candidates with stable order and one or more interaction tiles.
2. Ask the existing navigation/passability layer for exact reachable route length to each interaction tile.
3. Convert each reachable candidate to a selectable result using its lowest-cost interaction tile.
4. Sort or select by route cost, task-priority rank, and stable order.
5. Dispatch the selected candidate through the existing movement and task execution path.
6. Recompute candidates and route costs after every progress event.
7. Defer unreachable or failed candidates within the active batch and retry only while progress continues.

## Performance Pattern

No new pathfinding framework is selected. The implementation should use the current movement driver's navigation behavior or a narrow exposed route-cost method so route-cost decisions and actual movement stay aligned.

Short-lived caching is acceptable only when all of these remain unchanged:

- Worker current tile.
- Candidate target state.
- Interaction tile list.
- Passability-relevant world state.

Any progress event invalidates cached route data.

## Testing Pattern

### Example-Based Tests

Use xUnit tests to pin the concrete regressions from the request. These tests should be named and organized so a maintainer can connect each one to a visible routing scenario.

### Property-Based Tests

Use FsCheck.Xunit properties for selector invariants. Tests should use domain-shaped generators for candidate sets and route results, then compare the selected output to a simple minimum-cost/tie-break oracle.

Expected property targets:

- Minimum reachable route cost wins.
- Equal route costs use task-priority rank before stable order.
- Unreachable candidates are filtered while reachable candidates exist.
- Current-tile interaction gives route cost zero.

If a pure deferral helper is introduced, add a stateful or sequence-style property for finite termination. If deferral remains integrated with live Stardew world state, keep deferral property requirements documented and cover it with focused example tests.

## Observability Pattern

Use existing logging facilities only for maintainer-facing diagnostics:

- Skipping blocked deferred work after a no-progress pass.
- Defensive max-pass guard activation.
- Unexpected route-evaluation failures.

Do not add player-facing messages, mail, HUD text, or verbose normal-path logs for routing choices.

## PBT-09 Compliance

| Criterion | Status | Evidence |
|---|---|---|
| Framework selected | Compliant | FsCheck.Xunit is the selected C#/.NET PBT framework. |
| Framework dependency present | Compliant | `Dayswork.Tests/Dayswork.Tests.csproj` references `FsCheck.Xunit` version 2.16.5. |
| Supports custom generators | Compliant | Existing `Dayswork.Tests/Generators/` and unit-specific generator files use FsCheck `Arbitrary<T>`. |
| Supports shrinking | Compliant | FsCheck shrinking is enabled by default and is already used by existing property tests. |
| Supports seed replay | Compliant | Existing test docs and smoke tests document FsCheck replay syntax. |
| Integrates with test runner | Compliant | `FsCheck.Xunit` integrates with the existing xUnit test project. |

## Extension Compliance

| Extension | Status | Tech-stack compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - no security stack decision is required. |
| Property-Based Testing | Enabled, full | Compliant - PBT-09 is satisfied by the existing FsCheck.Xunit stack and documented for this unit. |
