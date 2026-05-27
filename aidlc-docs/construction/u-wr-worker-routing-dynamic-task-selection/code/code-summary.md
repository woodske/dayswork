# Code Summary - U-WR Worker Routing and Dynamic Task Selection

## Overview

U-WR changes the worker's within-batch routing from fixed queue/side preference to route-ranked active-batch selection. Broad batch order is preserved, but each task boundary now evaluates currently actionable tile, animal, hopper, and trough work by actual reachable route cost.

## Modified Application Files

| File | Change |
|---|---|
| `Dayswork.Core/Shifts/ITaskPriorityOrderer.cs` | Added `Rank(TaskKind)` so equal-route selection can use the existing task-priority authority. |
| `Dayswork.Core/Shifts/TaskPriorityOrderer.cs` | Implemented task rank lookup. |
| `Dayswork.Core/Shifts/WorkItem.cs` | Added optional `InteractionTiles` / `NavigationTiles` support while preserving existing constructor compatibility. |
| `Dayswork.Core/Inventory/CollectibleItemIdNormalizer.cs` | Adds a pure shape guard for collectible item IDs so incomplete prefixes like `(O)` can be rejected before runtime handling. |
| `Dayswork/Worker/WorkerMovementDriver.cs` | Added movement-aligned route-cost lookup and a per-selection route-cost map using the same map bounds, neighbor order, and worker passability rules as fallback movement. |
| `Dayswork/Orchestration/WorkAreaScanner.cs` | Preserves all potential interaction tiles for tile work and keeps scan order stable for dynamic selection instead of Manhattan pre-ordering. |
| `Dayswork/Orchestration/AnimalTaskHandler.cs` | Exposes multiple animal interaction tiles and gives hopper/trough feed work multiple stand candidates. |
| `Dayswork/Orchestration/DebrisItemIdResolver.cs` | Normalizes raw debris item IDs to qualified IDs and resolves them through Stardew's item registry before buffering worker drops. |
| `Dayswork/Orchestration/ShiftOrchestrator.cs` | Builds a fresh active-batch candidate pool, selects nearest reachable work with deterministic tie-breaks, defers navigation failures, retries after progress, skips blocked remainder on no reachable progress, revalidates before dispatch, updates stuck recovery to use the same active-batch selector, routes chest deposits to reachable adjacent stand tiles, and warns when worker-created debris carries an invalid item ID. |
| `Dayswork/Orchestration/BuildingWorkNavigator.cs` | Exposes interior exit approach candidates and a nearest-reachable selector so building completion walks to a reachable exit approach tile before transitioning outside. |

## Created Application/Test Files

| File | Purpose |
|---|---|
| `Dayswork.Core/Shifts/WorkerRouteSelector.cs` | Pure selector over already-evaluated route candidates, plus a shared nearest-reachable tile selector for routing-adjacent decisions. |
| `Dayswork.Tests/UWR/UWRPropertyGenerators.cs` | Domain-shaped FsCheck generators for route candidates. |
| `Dayswork.Tests/UWR/WorkerRouteSelectorPropertyTests.cs` | FsCheck invariants for minimum route cost, tie-breaks, unreachable filtering, and zero-cost current tile behavior. |
| `Dayswork.Tests/UWR/WorkerRouteSelectorTests.cs` | Deterministic selector examples. |
| `Dayswork.Tests/UWR/WorkerRoutingRegressionTests.cs` | Focused examples for wrong-side routing, blocked-side product collection, near animal selection, feed retry after product progress, disabled product boundary, and no-progress blocked termination. |
| `Dayswork.Tests/Orchestration/DebrisItemIdResolverTests.cs` | Focused examples for collectible item-ID normalization, including rejection of incomplete bare type prefixes like `(O)`. |
| `Dayswork.Tests/UWR/BuildingExitRoutingTests.cs` | Focused examples for selecting a reachable interior exit approach tile before the farm transition. |
| `Dayswork.Tests/UWR/DepositRoutingTests.cs` | Focused examples for selecting a reachable adjacent stand tile before chest deposits. |

## Behavior Implemented

- The worker evaluates actual reachable route length to candidate interaction tiles before selecting work.
- Fixed top/right/bottom/left preference no longer decides the selected stand tile.
- Animal, tile, hopper, and actionable trough work compete in one nearest-reachable pool inside the active broad batch.
- Equal route lengths use task priority, then stable scan/discovery order.
- Route costs are recomputed at each task boundary; no cross-selection cache is retained.
- Blocked or failed navigation is deferred within the active batch and retried after other work makes progress.
- If no reachable work remains, the blocked remainder is skipped for the day rather than looping.
- Feed work preserves hopper-before-trough behavior and does not collect unpaid products to clear a feed path.

## Review Feedback Fix

- Play-test feedback: barn and coop routing looked good and performed well, but outdoor tile work dropped framerate to 1 FPS.
- Cause addressed: outdoor tile batches can contain many candidate tasks/stand tiles, and the initial implementation ran an exact route search for each candidate tile during selection.
- Fix: `ShiftOrchestrator` now asks `WorkerMovementDriver` for one exact route-cost map from the worker's current tile per active-batch selection, then scores all candidate interaction tiles by dictionary lookup.
- Behavior preserved: shortest reachable route still wins, candidate stand tiles are still evaluated from top/bottom/left/right/current options where applicable, and route costs are still recomputed after each completed task or world-state change.

## Building Exit Review Fix

- Play-test feedback: after building tasks, the worker visibly warped out instead of walking out.
- Cause addressed: building completion picked the first passable tile near the interior exit, but that tile was not guaranteed to be reachable from the worker's final task position.
- Fix: building completion now computes current reachable route costs inside the building, chooses the nearest reachable exit approach tile, walks there, and only then performs the existing location transition back to the farm.

## Chest Deposit Review Fix

- Play-test feedback: chest-destined materials were deposited automatically without the worker traveling to the chest.
- Cause addressed: deposit trips targeted the chest's occupied tile; if navigation failed, the deposit handler treated the failure as enough to execute the transfer.
- Fix: chest deposit trips now choose the nearest reachable stand tile adjacent to the chest and only transfer items after successful arrival. If no stand tile can be reached, the trip is mailed as undelivered instead of auto-depositing.

## Outdoor Error Item Review Fix

- Play-test feedback: an intermittent `Error Item` sometimes appeared on outdoor worker tiles, with the item UI showing a malformed object-like ID such as bare `(O)`.
- Likely cause addressed: Stardew's outdoor debris paths, especially tree seed or other probabilistic tree-drop creation, can create in-world debris directly from item ID strings. If a malformed ID slips through, `Debris.InitializeItem(...)` turns it into a visible Error Item. Dayswork previously trusted any non-empty debris ID when buffering nearby drops, so there was no narrow diagnostic when that happened.
- Fix: worker debris intake now normalizes raw debris IDs to a complete qualified shape, resolves them through `ItemRegistry`, buffers only valid resolved items, and logs a focused `[Dayswork][debris] worker-created debris could not be resolved...` warning with task/location context when a nearby worker-created debris item has an invalid ID. This keeps valid drops flowing normally while giving the next repro an actionable source breadcrumb instead of silently accepting malformed IDs.

## PBT Compliance

| Rule | Result |
|---|---|
| PBT-03 | Selector invariants covered by FsCheck properties. |
| PBT-05 | Selector properties compare against a simple minimum-cost/tie-break oracle. |
| PBT-06 | N/A for generated code: no standalone pure deferral state machine was extracted. Deferral remains integrated with live orchestrator state and is covered by examples. |
| PBT-07 | Route selector PBT uses domain-shaped candidate generators. |
| PBT-09 | Existing `FsCheck.Xunit` dependency reused. |
| PBT-10 | Example regression tests complement property tests. |

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` passed with `0` warnings and `0` errors.
- `dotnet test Dayswork.sln` passed with `300` passed, `1` expected skip, `0` failed.
- Review feedback compile check: `dotnet build Dayswork.sln /p:EnableModDeploy=false` passed with `0` warnings and `0` errors after the per-selection route-cost map change.
- Review feedback full test rerun: `dotnet test Dayswork.sln` passed with `300` passed, `1` expected skip, `0` failed after the per-selection route-cost map change.
- Building exit review fix compile check: `dotnet build Dayswork.sln /p:EnableModDeploy=false` passed with `0` warnings and `0` errors after adding reachable exit approach selection.
- Building exit review fix full test rerun: `dotnet test Dayswork.sln` passed with `303` passed, `1` expected skip, `0` failed after adding reachable exit approach selection.
- Chest deposit review fix compile check: `dotnet build Dayswork.sln /p:EnableModDeploy=false` passed with `0` warnings and `0` errors after adding reachable chest stand-tile selection.
- Chest deposit review fix full test rerun: `dotnet test Dayswork.sln` passed with `306` passed, `1` expected skip, `0` failed after adding reachable chest stand-tile selection.
- Outdoor Error Item review fix compile check: `dotnet build Dayswork.sln /p:EnableModDeploy=false` passed with `0` warnings and `0` errors after adding debris item-ID normalization and warning diagnostics.
- Outdoor Error Item review fix full test rerun: `dotnet test Dayswork.sln /p:EnableModDeploy=false` passed with `316` passed, `1` expected skip, `0` failed after adding debris item-ID normalization and warning diagnostics.
- Outdoor Error Item review fix deploy build: `dotnet build Dayswork.sln` passed with `0` warnings and `0` errors and refreshed `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.

## Caveats

- The automated tests cover the pure route-selection seam and focused regression scenarios. Full barn/coop pathing should still be play-tested in-game because Stardew map collision and animal/product placement are runtime-heavy.
- `dotnet test Dayswork.sln` used the project default deploy setting and copied the mod to the configured Stardew `Mods/Dayswork` folder during test build.
