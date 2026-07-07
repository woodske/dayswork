# Plan — Core pathfinding extraction + per-shift passability cache

**Status:** phase 1 **built + unit-tested 2026-07-07** (pending in-game smoke pass on a large farm).
Item #2 in [architecture-review-index.md](architecture-review-index.md). This is the keystone plan:
it is a perf fix, a testability fix, and the enabler for [pathing-polish.md](pathing-polish.md)
items (b)/(c) and the passability snapshot required by the deferred
[stand-coverage-routing.md](stand-coverage-routing.md).

## What was built (2026-07-07)

- **Core** `Dayswork.Core/Pathing/`: `IPassabilityView`, `PassabilityGrid`, `GridPathfinder` (BFS
  cost map + route reconstruction) — lifted verbatim out of `WorkerMovementDriver`, N,E,S,W order
  preserved. Unit tests: `Dayswork.Tests/Pathing/GridPathfinderTests.cs` (12 cases: cost maps,
  unreachable/OOB/blocked-end, start==end, tie-order, determinism, single-cell re-probe).
- **Game side** `LivePassabilityView` (live probe) + `LocationPassabilityCache` on `ShiftSession`.
  `WorkerMovementDriver.ComputeRouteCostsFrom`/`TryFindRoute` now delegate to `GridPathfinder` via
  the live view; the driver's private BFS/`Neighbours`/`IsWithinMap`/`ReconstructPath` were deleted.
- **Cached call sites** (7): `TrySelectNextActiveWork`, `ResolveReachableShiftExitTile`,
  `BuildBuildingExitPlan`, `ResolveFishPondNavTile`, `TrySelectChestDepositStandTile` (cache threaded
  through the static method's 4 callers), and `ManagedShoppingCoordinator` ×2.
- **Invalidation:** clump-clear footprint re-probe (`TaskActions`), SMAPI
  Object/TerrainFeature/Furniture/Building list-changed (`ShiftOrchestrator.Passability.cs` + wired
  in `ModEntry`), stuck-recovery `InvalidateLocation` in `QueueStuckTeleport`.
- **Hard rule 7:** the FarmAnimal collision question is resolved — with `character: null` the animal
  loop is skipped entirely, so animals were never in the probe's answer and need no special handling.
  Recorded in `docs/pathing.md`. The inset-rect `+1/62` corner math transfers unchanged (the grid is
  built from the same `IsTilePassableForWorker` probe).

Phase 2 (targeted A* for sweep categories) intentionally deferred — build only if profiling says the
remaining cost matters.

## Problem

Every work-item selection calls `WorkerMovementDriver.ComputeRouteCostsFrom` — a whole-map BFS
where **every tile probe goes through `IsTilePassableForWorker`**, which calls
`location.isCollidingPosition(...)`. That vanilla call iterates buildings, resource clumps,
furniture, and characters per probe. On an 80×65 farm that is ~5,200 collision probes on a single
tick, once per work item, hundreds of times per shift — the likeliest frame-hitch source on large
modded/SVE farms.

Call sites of `ComputeRouteCostsFrom` today (all pay full price every call):

- `ShiftOrchestrator.WorkSelection.cs` `TrySelectNextActiveWork` (per work item — the hot one)
- `ShiftOrchestrator.Routing.cs` `ResolveReachableShiftExitTile`
- `ShiftOrchestrator.Travel.cs` `BuildBuildingExitPlan`
- `ShiftOrchestrator.Deposit.cs`, `ShiftOrchestrator.FishPonds.cs`,
  `ManagedShoppingCoordinator.cs` (×2)

The same per-tile probe also backs `TryFindRoute` (the BFS fallback in `StartNavigation`) and the
vanilla-path validation loop.

Secondary problem: all of this graph logic lives in `Dayswork/` solely because it needs
`GameLocation` for the passability probe — so none of it is unit-tested (BFS correctness, route
reconstruction, cost maps, fallback behavior).

## Design sketch

### Core (`Dayswork.Core`, pure, unit-tested)

New `Dayswork.Core/Pathing/`:

- **`PassabilityGrid`** — width/height + packed passability bits; indexer by `TileCoord`.
  Construction from a `Func<int,int,bool>` probe (used by Dayswork) or a `bool[,]` (used by tests).
- **`GridPathfinder`** (static) — the algorithms moved out of `WorkerMovementDriver`, operating on
  a `PassabilityGrid`:
  - `ComputeRouteCosts(grid, source)` → cost map (prefer a pooled `int[,]` wrapped in a readonly
    view over the current `Dictionary<TileCoord,int>` + `HashSet<Point>` allocation churn);
  - `TryFindRoute(grid, start, end, out route)` (BFS route reconstruction, 4-dir, unchanged
    semantics: unweighted, cardinal `Neighbours` order preserved — **tie-break order is
    load-bearing** for both route selectors, per the worker-action-adjacency notes).

### Dayswork (game-facing seam)

- **`LocationPassabilityCache`** (per-shift, lives on `ShiftSession` so a fresh session is the
  reset): `PassabilityGrid GetGrid(GameLocation)` keyed by `NameOrUniqueName` (interiors resolve
  only by `NameOrUniqueName` — known landmine). First request per location does the one full
  `isCollidingPosition` sweep; subsequent requests are free.
- **Invalidation**, cheapest-correct first:
  1. *Worker's own changes* — the mod knows exactly what it cleared. After each guarded beat that
     removes an obstacle (rocks, weeds, trees→stumps→gone, resource clumps via the
     `performToolAction` return, gate toggles), invalidate that tile/footprint.
  2. *External changes* — subscribe SMAPI `World.ObjectListChanged`,
     `World.TerrainFeatureListChanged`, `World.BuildingListChanged`,
     `World.FurnitureListChanged` and invalidate the affected tiles for the affected location.
     Resource clumps have no SMAPI event: accept staleness (see tolerance analysis below).
  3. *Rebuild triggers* — location entry (grid built lazily anyway) and stuck-detector fire
     (rebuild before recovery-tile selection, so recovery never picks a tile based on stale data).
- **Gate semantics preserved:** the grid marks closed-but-openable gates passable (current
  `HasOpenableGate` behavior) — record gate tiles so `OpenGatesAlongRoute` still works from route
  tiles.
- `IsTilePassableForWorker` stays as the *live* probe for the two places that must not trust a
  cache: final validation of a `PathFindController` path in `StartNavigation`, and single-tile
  spot checks (`ResolvePassableNearby` etc. can migrate later or stay live — they're rare).

### Staleness tolerance

The system already tolerates a wrong reachability answer: navigation failure defers the work item
within the batch, deferred items are retried, and the stuck detector is the terminal backstop. A
stale grid therefore degrades to today's behavior, never to item loss or a stranded worker. This
is what makes coarse invalidation acceptable.

### Optional phase 2 — targeted search for sweep categories

For serpentine categories, `SelectionKey = StableOrder` means selection order is predetermined;
the flood fill is only consulted for *reachability + stand-tile choice*. A targeted A* from the
worker to the first few candidates in sweep order terminates long before exploring the map in the
common case. Keep the flood fill as fallback when the head candidates are unreachable. Build this
only if profiling after phase 1 says the remaining cost matters.

## Verification required before build (hard rule 7)

- Confirm against the decompile exactly which dynamic entities
  `isCollidingPosition(..., isFarmer: false, character: null, pathfinding: true,
  ignoreCharacterRequirement: true)` includes — specifically whether **FarmAnimals** block. If
  they do, animal positions must be *excluded from the cached grid* and probed live (animals move
  every tick; caching them would thrash), matching whatever today's behavior actually is.
- Confirm the inset-rect corner math note (+1/62) transfers unchanged into the grid-build sweep.

## What must NOT change

- BFS neighbour order (cardinal N,E,S,W) and first-wins tie-breaking — route selector parity.
- The walk-only invariant: caching never introduces a new warp path.
- `StartNavigation`'s vanilla-path validation stays live (not grid-based).

## Testing

- Core: unit tests for `GridPathfinder` (cost maps, unreachable targets, route reconstruction,
  determinism/tie-order, start==end, blocked end tile) — this code is currently untested at all.
- Dayswork: DevLog timing around `TrySelectNextActiveWork` before/after (a
  `dayswork_debug_pathing` console command or plain DevLog lines) to prove the win; in-game smoke
  pass on a large farm with buildings + clumps.
- Regression focus: gate routing, deferred-work retry after a stale-grid nav failure, stuck
  recovery rebuild.

## Open questions

- Pool the `int[,]` cost buffer per shift or per location size class?
- Should `ResolvePassableNearby` / exit-tile resolution migrate to the grid in v1 or stay live?
  (They're rare; migrating is cleanup, not perf.)
