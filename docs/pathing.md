# Pathing & passability

How the worker's route graph is built, cached, and searched. Backs the per-shift
`LocationPassabilityCache` + Core `GridPathfinder` (built 2026-07-07; item #2 of the architecture
review — see `docs/plans/core-pathfinding-and-passability-cache.md`).

## The passability probe

`WorkerMovementDriver.IsTilePassableForWorker(Point, GameLocation)` is the single source of truth
for "can the worker stand on / route through this tile". It combines:

1. `location.isTilePassable(...)` — tile-map walls, water, map bounds.
2. `location.isCollidingPosition(inset64Rect, viewport, isFarmer: false, damagesFarmer: 0,
   glider: false, character: null, pathfinding: true, ignoreCharacterRequirement: true)` — dynamic
   obstacles.
3. Closed-but-openable fence gates are treated as **passable** (`HasOpenableGate`): the worker opens
   them while pathing (`OpenGatesAlongRoute`).

The collision rect is inset `+1 / 62` (not a full 64) to match `PathFindController.findPath` corner
math; a full 64×64 rect maps the right/bottom edges onto the *adjacent* tile and produces false
positives next to buildings/objects (e.g. the farm-entrance tile falsely blocked by the shipping
bin above it).

### What `isCollidingPosition(character: null, …)` treats as blocking — verified 2026-07-07

Decompiled `StardewValley.GameLocation.isCollidingPosition` (the 10-arg overload) against
`X:\Steam\...\Stardew Valley.dll`. With our exact args (`character: null`,
`ignoreCharacterRequirement: true`, `pathfinding: true`):

| Entity | Blocks? | Notes |
|---|---|---|
| **FarmAnimals** | **No** | The animal loop is guarded by `character != null && animals.FieldDict.Count > 0 && !(character is FarmAnimal)`. `character` is null → the whole loop is **skipped**. Animals are *not* obstacles for our probe, so caching them is a non-issue — the cached grid matches live behavior exactly. |
| Buildings | Yes | `character` null falls through to the `!(character is NPC)` branch → `return true`. Static within a shift except player builds → `BuildingListChanged`. |
| Resource clumps | Yes | `resourceClumps` loop, unconditional. **No SMAPI event** for removal → invalidated at the worker's clear site. |
| Furniture (type != 12 rug) | Yes | `FurnitureListChanged`. |
| Large terrain features (bushes) | Yes | No dedicated invalidation — bushes ~never change mid-shift; accepted staleness. |
| Tile-layer (walls/water) | Yes | via `_TestCornersTiles`, plus `isTilePassable`. |

This is why the FarmAnimal question the plan flagged (hard rule 7) needs **no** special handling:
animals were never in the probe's answer.

## Core: `GridPathfinder` + `PassabilityGrid` (`Dayswork.Core/Pathing/`)

Pure, unit-tested BFS (was previously untested inside `WorkerMovementDriver`):

- `PassabilityGrid` — a row-major `bool[]` snapshot; built once from a `Func<int,int,bool>` probe,
  cells re-probed via `SetPassable`. Implements `IPassabilityView`.
- `IPassabilityView` — `Width`/`Height`/`IsPassable(x,y)`. Two implementations (ceremony earned):
  `PassabilityGrid` (cached) and game-side `LivePassabilityView` (probes every call).
- `GridPathfinder.ComputeRouteCosts(view, source)` — BFS flood fill → cost map. Source always
  present at cost 0 (its own passability never checked — the worker may stand on a blocked tile).
- `GridPathfinder.TryFindRoute(view, start, end, out route)` — BFS route (excludes start, ends at
  `end`; empty route ⇒ start == end; end must be passable; start passability never checked).

**Load-bearing invariant:** neighbour order is **N, E, S, W** and must not change — both route
selectors are first-wins-on-ties over the cost map, so reordering silently changes which work tile
the worker picks (see the worker-action-adjacency notes). Tests in
`Dayswork.Tests/Pathing/GridPathfinderTests.cs` pin this.

## Game side: the per-shift cache (`LocationPassabilityCache`)

Lives on `ShiftSession` (a fresh session is the reset). `GetGrid(location)` builds a
`PassabilityGrid` on first request per location (one full `isCollidingPosition` sweep) and reuses it
after; `RouteCostsFrom(source, location)` is the cached replacement for the old
`WorkerMovementDriver.ComputeRouteCostsFrom`. Keyed by `NameOrUniqueName` (interiors resolve only by
`NameOrUniqueName`).

**Routed through the cache** (selection / ordering only — safe to be approximate):
`TrySelectNextActiveWork` (the hot per-work-item call), `ResolveReachableShiftExitTile`,
`BuildBuildingExitPlan`, `ResolveFishPondNavTile`, `TrySelectChestDepositStandTile` (deposits +
machines), and `ManagedShoppingCoordinator`'s warp-edge ordering + shop-counter stand selection.

**Stays live (never cached):** `WorkerMovementDriver`'s navigation fallback BFS and `StartNavigation`
path validation, and all one-off `IsTilePassableForWorker` spot checks. The fallback is about to be
physically walked, so it must reflect the current world, not a possibly-stale grid.

### Staleness contract

A wrong reachability answer only degrades **routing quality**, never item safety: a nav failure
defers the work item (retried later), and the stuck detector is the terminal backstop. Actual
navigation validates against the live probe. So a stale grid degrades to pre-cache behavior — this
is what makes coarse invalidation acceptable.

### Invalidation

1. **Worker-cleared resource clumps** — `ShiftOrchestrator.TaskActions` re-probes the clump footprint
   (`InvalidateArea`) right after `resourceClumps.Remove`. This is the one obstacle with no SMAPI
   event.
2. **External / other worker changes** — SMAPI `World.ObjectListChanged` (rocks),
   `TerrainFeatureListChanged` (trees/weeds/grass), `FurnitureListChanged`, `BuildingListChanged`
   re-probe the affected tiles (`ShiftOrchestrator.Passability.cs`). All no-op when no shift is
   active or the location isn't cached. These fire for **all** locations (arg carries `Location`),
   so off-current-location worker changes are covered too.
3. **Stuck recovery** — `QueueStuckTeleport` drops the whole location grid
   (`InvalidateLocation`) before selecting a recovery tile, so recovery never teleports on stale
   reachability.

Gate toggles need no invalidation: the grid marks openable gates passable regardless of open state.
