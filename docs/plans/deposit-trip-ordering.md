# Plan — Cross-location deposit trip ordering

**Status:** proposed 2026-07-07, not started. Item #6 in
[architecture-review-index.md](architecture-review-index.md). Small, independent, pure-Core; can
slot in anywhere.

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
