# U-04 — Business Logic Model

## Overview

U-04 establishes the foundational geometry layer and value-type vocabulary. All types are pure C# — zero SMAPI or StardewValley references in `Dayswork.Core`.

---

## Domain vocabulary lifecycle

```
TileCoord  ──── used by ────► Zone ──── used by ────► IZoneGeometry
    │                           │                           │
    │                           └── persisted in ──────► Contract (U-06)
    │
    └── used by ────► ChestRef ──── wrapped by ──────► ChestDestination
                                                             │
                                                   DestinationKey hierarchy
                                                             │
                                                    used by ─── ItemBuffer (U-10)
```

---

## Tile enumeration pipeline

At shift start, `ShiftOrchestrator` (M-12) builds the task-queue tile set using this pipeline:

```
Contract.Zones (IReadOnlyList<Zone>)
    │
    ▼
ZoneGeometry.EnumerateUniqueTiles(zones, isPassable)
    │  ├─ For each zone: iterate tiles in row-major order
    │  ├─ Apply isPassable oracle (wraps Game1 location API)
    │  └─ Deduplicate via HashSet — each tile appears at most once
    ▼
IReadOnlyList<TileCoord>  ──► TaskPriorityOrderer (C-07, U-07)
    │                           sorts (TaskKind, TileCoord) pairs into priority order
    ▼
Ordered work-item queue  ──► ShiftStateMachine intents (U-10)
```

**Why deduplication matters**: A player can draw overlapping zone rectangles (e.g., one zone covering the whole farm, one covering just the animal barn). `EnumerateUniqueTiles` ensures each tile is only worked once, preventing duplicate harvesting or redundant pathfinding hops.

---

## Hours estimation pipeline

`HoursEstimator` (C-04, introduced in U-05) uses `ZoneGeometry` for the tile-count input to the estimate:

```
ZoneGeometry.CountReachableTiles(zone, isPassable)
    → int reachableTileCount
    → passed to HoursEstimator.Estimate(zones, tasks, config)
    → decimal estimatedHours
    → passed to DepositCalculator.Compute(hours, rate)
    → int deposit (gold)
```

`CountReachableTiles` is a separate method (rather than `EnumerateReachableTiles(...).Count`) to avoid allocating the full tile list when only the count is needed.

---

## Passability oracle model

`ZoneGeometry` does NOT know about game locations. The passability oracle decouples pure tile math from SMAPI:

| Caller | Oracle implementation |
|---|---|
| `ShiftOrchestrator` (shift start) | Wraps `Farm.isTileLocationTotallyClearAndPlaceable(tile)` (or equivalent) |
| `ShiftOrchestrator` (during shift) | Same oracle, re-evaluated per scan if needed |
| `HoursEstimator` (estimate at hire) | Same oracle, captured at UI time (may differ from actual at shift time) |
| Tests | Simple `Func<TileCoord, bool>` lambda (e.g., "all tiles passable", "checkerboard pattern") |

**Stale oracle note**: The oracle captured at hire time (for the deposit estimate) may differ from the oracle at shift time (6am next morning, after overnight game-state changes). This is acceptable per the "estimate" framing — the deposit is an estimate, refunded based on actual hours worked.

---

## DestinationKey assignment flow

At contract creation (U-09, hiring flow):

```
TaskSelectionMenu: player enables tasks
    │
    ▼
ZoneAndChestMenu (U-11): player assigns chests per output task
    │  ├─ Open-farm chest → ChestDestination(ChestRef("Farm", tile))
    │  ├─ Building chest → ChestDestination(ChestRef("Greenhouse", tile))
    │  ├─ Shipping bin → ShippingBinDestination.Instance
    │  └─ No assignment → MailDestination.Instance
    ▼
ContractDraft.TaskDestinations: Dictionary<TaskKind, DestinationKey>
    │
    ▼
Contract record (persisted in ContractStore, U-06)
```

At shift execution (U-10, ShiftOrchestrator):

```
worker picks up item of TaskKind T from tile
    │
    ▼
look up contract.TaskDestinations[T] → DestinationKey
    │
    ▼
ItemBuffer.Add(item, destinationKey)  ← each unique DestinationKey has own bucket
    │
    ▼
DepositPlanner (U-14) calls ItemBuffer.TakeAllFor(key) for each unique ChestDestination
```

---

## Zone JSON serialization (PBT-02 target)

Zone serializes to Newtonsoft.Json as a flat JSON object. Nested `TileCoord` structs serialize as objects with `X` and `Y` integer fields:

```json
{
  "LocationName": "Farm",
  "TopLeft": { "X": 5, "Y": 8 },
  "BottomRight": { "X": 12, "Y": 15 }
}
```

`ChestRef` follows the same pattern:

```json
{
  "LocationName": "Greenhouse",
  "Tile": { "X": 3, "Y": 4 }
}
```

The PBT-02 test in `Dayswork.Tests/Geometry/` generates random `Zone` values (via `ZoneGen`), serializes them to JSON string, deserializes back, and asserts equality. The test must pass for ≥ 1000 generated inputs.
