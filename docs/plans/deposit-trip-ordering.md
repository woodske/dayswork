# Plan — Cross-location deposit trip ordering

**Status:** **built + unit-tested 2026-07-07.** Item #6 in
[architecture-review-index.md](architecture-review-index.md). Small, independent, pure-Core.

## What was built (2026-07-07)

- Core `DepositStop(string LocationName, TileCoord Tile)`; `DepositPlanner.Plan`'s seam changed from
  `TileCoord workerStart` + `Func<TileCoord,TileCoord,int>` to `DepositStop` + `Func<DepositStop,
  DepositStop,int>` (shipping-bin param likewise a `DepositStop`). Each trip's stop is built from its
  `ChestRef.LocationName` (`StopFor`); shipping-bin and worker-start are farm-space by construction.
  `OrderNearestNeighbor` chains over stops — same greedy algorithm, unchanged tie-break.
- Orchestrator metric `DepositStopDistance` (`ShiftOrchestrator.Deposit.cs`): same-location =
  Manhattan; cross-location = `hops * 10_000 + Manhattan(door tiles)` where the farm-side door tile
  comes from `BuildingLocationResolver.TryResolve` (farm ↔ interior = 1 hop, interior ↔ interior = 2).
  Unresolvable locations sort last via a large sentinel (the intended degradation for expansions
  without a farm warp). `K = 10_000` dwarfs any intra-location Manhattan, so same-location chests
  always group and there's no farm→interior→farm zig-zag.
- Tests: `Dayswork.Tests/Inventory/CrossLocationDepositOrderingTests.cs` (no-zig-zag, same-interior
  chests consecutive, determinism under permuted input); existing `DepositPlannerTests` retargeted to
  the new seam (Manhattan ignores location ⇒ same-location regression preserved).

The open question (force same-building chests adjacent) is answered by the `K` hop cost — verified by
the "two chests in same interior are visited consecutively" test.

## Problem

`DepositPlanner.Plan` orders deposit trips nearest-neighbor via the injected
`Func<TileCoord,TileCoord,int>` distance (the orchestrator passes Manhattan — see
`ShiftOrchestrator.Deposit.cs`). But chest destinations can live in **building interiors** and
(since the 2026-07-06 cross-location machine-chest work) potentially other locations entirely.
Manhattan between a farm tile and a shed-interior tile compares unrelated coordinate spaces, so
trip order can be arbitrary — e.g. farm-chest → shed-chest → farm-chest zig-zags — whenever
interior chests are in the plan.

## Design sketch

Keep the planner pure; make the *metric input* location-aware instead of teaching the planner
about locations.

### Core signature change

Each walkable destination already groups under a `DestinationKey`; extend the representative-
position type from bare `TileCoord` to a small value type:

```csharp
public readonly record struct DepositStop(string LocationName, TileCoord Tile);
```

and the distance seam from `Func<TileCoord,TileCoord,int>` to
`Func<DepositStop, DepositStop, int>`. `OrderNearestNeighbor` is otherwise unchanged (still
greedy-chained from the worker's start stop, deterministic tie-break by the existing stable stack
ordering).

`ChestRef` already carries the information needed to build a `DepositStop` (location + tile);
shipping-bin and worker-start stops are farm-space by construction.

### Orchestrator metric (game side)

Two-level composite, returned as one int (`hopCost * K + intraCost`, K > any plausible intra
distance):

- **Same location:** Manhattan between tiles (upgrade to cached route cost after
  [core-pathfinding-and-passability-cache.md](core-pathfinding-and-passability-cache.md) lands —
  optional, not a dependency).
- **Farm ↔ building interior:** distance measured to the building's **outdoor door tile**
  (`BuildingWorkNavigator.TryResolveDoorTile`) + one hop.
- **Farm ↔ expansion location:** hops from the validated expansion route (fall back to a large
  constant when the route doesn't validate — sorts genuinely-far stops last, which is the right
  degradation).
- **Interior ↔ interior:** door-to-door via the farm (hop count 2) — the metric composes from the
  two farm-side legs.

## What must NOT change

- Trip *execution* (`DepositTripRunner`) is untouched — it already handles building entry,
  mutexes, and failure-to-overflow. This plan changes only the visiting order.
- Item routing invariants (hard rule 4): stacks, consolidation keys, retained/eager semantics,
  overflow fallback — all unchanged. Ordering must remain a permutation of the same trips.
- Determinism of planner output.

## Testing

Item-routing-adjacent planner code, so tests are required per the testing policy:

- Same-location-only plans produce today's order (regression).
- Mixed farm + interior chests: farm stops chain before/after interior stops sensibly; no
  farm→interior→farm zig-zag when a same-location stop is available.
- Two interiors: grouped, not interleaved with farm stops.
- Deterministic under permuted input order.

## Open questions

- Should trips to the *same building* (two chests in one shed) be forced adjacent even when the
  greedy chain wouldn't pick them consecutively? (Leaning yes — the hop cost constant K
  effectively does this already; verify with a test.)
