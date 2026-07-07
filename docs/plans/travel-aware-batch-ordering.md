# Plan — Travel-aware batch ordering

**Status:** proposed 2026-07-07, not started. Item #3 in
[architecture-review-index.md](architecture-review-index.md). Independent of the passability-cache
plan; likely the single largest visible reduction in in-game walking time.

## Problem

`ShiftPlanBuilder` orders batches **within a category alphabetically by location name**
(`OrderBy(name, StringComparer.Ordinal)`) — animal buildings, machine locations, fish-pond
locations, managed non-farm locations, greenhouses all do this. Category priority itself is
player-authoritative and correct; but within a category the worker can visit
Barn → Coop → Slime Hutch in *name* order even when that walks the farm's diagonal twice. For a
multi-building animal or machine contract, batch order dominates total walking distance far more
than any per-tile routing improvement.

## Design sketch

### Keep the planner pure (hard rule 1)

`ShiftPlanBuilder.BuildBatchPlan` gains an anchor input the orchestrator resolves at `StartShift`:

```
IReadOnlyDictionary<string /*locationName*/, TileCoord /*anchor*/> batchAnchors,
TileCoord startAnchor   // worker spawn: office door / farm entrance tile
```

- Anchor for a farm building = its **outdoor door tile** (already resolvable via
  `BuildingWorkNavigator.TryResolveDoorTile` / `BuildingLocationResolver`).
- Anchor for `"Farm"`-wide batches (outdoor crops, clearing, forage) = the zone centroid clamped
  to the scope, else the farm entrance.
- Anchor for expansion locations = the farm-side approach tile of the first hop in the validated
  expansion route (fall back to name ordering if the route doesn't validate at plan time).
- Missing anchor ⇒ that batch sorts after anchored ones, in name order (deterministic degradation
  to today's behavior).

### Ordering rule

Within each category, order batches **nearest-neighbor chained**: first batch = nearest anchor to
`startAnchor`; each next batch = nearest anchor to the previous batch's anchor. Manhattan distance
on anchors is sufficient for v1 (all anchors are farm-coordinate tiles); route cost via the cached
passability grid is a later refinement, not a prerequisite. Ties break by location name ordinal —
plan output stays deterministic.

### Structural orderings that must survive

Nearest-neighbor applies only where today's order is alphabetical-arbitrary. These stay fixed:

- **AnimalBuilding + its OutdoorAnimals batch stay adjacent as a pair** (the "fully service one
  building before the next" grouping) — the pair moves as one unit under NN.
- FarmForage (truffles) stays last within AnimalCare.
- Crops-category internal structure stays: managed non-farm → greenhouses → managed farm →
  outdoor crops → FarmCave.
- Machines/FishPonds: NN over their per-location batches (currently pure name order — the easiest
  win).

## What must NOT change

- Category priority order (player-authored) remains authoritative over everything.
- Batch *contents* and scanning are untouched — this reorders skeletons only.
- Determinism: same inputs ⇒ same plan (tests depend on it; so does debuggability).

## Testing

Planner is Core and already the right shape for unit tests: feed synthetic anchor maps and assert
chained order, pair-adjacency preservation, fixed structural orderings, missing-anchor fallback,
and tie determinism. In-game: a 3+ animal-building save arranged in an inverse-alphabetical layout
(so NN visibly diverges from today's order), watch one shift.

## Open questions

- Should the *last* batch's proximity to the farm exit get a small weight (end the day near home),
  or is that over-fitting? Suggest: no for v1; measure first.
- Cross-category chaining (start category N+1 from where category N ended) — deferred; category
  boundaries usually imply a deposit or location change anyway.
