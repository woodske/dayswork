# Vanilla Farms — Warp Reference

All vanilla farm maps use **edge warps only** (`Warp` map property). None of them
use `TouchAction: LoadMap` pedestrian tiles, and the farmhouse / greenhouse /
cave entrances are building warps injected at runtime (not in the map file).

Each farm exits to the same four neighbours — Bus Stop (east), Backwoods (north),
Cindersap Forest (south), and the Farm Cave (interior). Return/arrival tiles are
the shared values in the [README](README.md#shared-vanilla-neighbour--farm-arrival-tiles).

## Farm type → map file

| `whichFarm` | Farm type | Map asset | Size |
|---|---|---|---|
| 0 | Standard | `Maps/Farm` | 80×65 |
| 1 | Riverland | `Maps/Farm_Fishing` | 80×65 |
| 2 | Forest | `Maps/Farm_Foraging` | 80×65 |
| 3 | Hill-top | `Maps/Farm_Mining` | 80×65 |
| 4 | Wilderness | `Maps/Farm_Combat` | 80×65 |
| 5 | Four Corners | `Maps/Farm_FourCorners` | 80×80 |
| 6 | Beach | `Maps/Farm_Island` | 110×110 |
| 7 | Meadowlands | `Maps/Farm_Ranching` | 100×75 |

## Standard / Riverland / Forest / Hill-top / Wilderness (80×65)

These five share an **identical** warp table.

| Exit tiles (Farm) | → Destination | Arrival back on Farm |
|---|---|---|
| `(80,15) (80,16) (80,17)` | BusStop `(11,23)` | `(79,17)` |
| `(80,18)` | BusStop `(11,24)` | `(79,17)` |
| `(40,65) (41,65) (42,65)` | Forest `(68,0)` | `(41,64)` |
| `(40,-1) (41,-1)` | Backwoods `(14,39)` | `(40,0)` |
| `(34,5)` | FarmCave `(8,11)` | `(34,6)` |

## Four Corners (80×80)

Identical to the 80×65 farms **except** the Farm Cave entrance.

| Exit tiles (Farm) | → Destination | Arrival back on Farm |
|---|---|---|
| `(80,15) (80,16) (80,17)` | BusStop `(11,23)` | `(79,17)` |
| `(80,18)` | BusStop `(11,24)` | `(79,17)` |
| `(40,65) (41,65) (42,65)` | Forest `(68,0)` | `(41,64)` |
| `(40,-1) (41,-1)` | Backwoods `(14,39)` | `(40,0)` |
| `(30,35)` | FarmCave `(8,11)` | `(34,6)` |

## Beach (110×110) — `Farm_Island`

| Exit tiles (Farm) | → Destination |
|---|---|
| `(81,16) (81,17) (81,18)` | BusStop `(11,23)` |
| `(81,105) (82,105) (83,105)` | Forest `(68,0)` |
| `(40,-1) (41,-1)` | Backwoods `(14,39)` |
| `(34,15)` | FarmCave `(8,11)` |

> The Beach farm has no quarry/cave on the standard side beyond the listed Farm
> Cave warp; the shipping/greenhouse spots are runtime-placed.

## Meadowlands (100×75) — `Farm_Ranching`

| Exit tiles (Farm) | → Destination |
|---|---|
| `(100,21) (100,22) (100,23)` | BusStop `(11,23)` |
| `(52,75) (53,75)` | Forest `(68,0)` |
| `(63,-1) (64,-1)` | Backwoods `(14,39)` |
| `(88,54)` | FarmCave `(8,11)` |

Extra map property: `WarpTotemEntry = 71 6` (Farm Totem return point).

## Town-shopping note

No vanilla farm warps directly to `Town`. The route to Pierre's / JojaMart is
always **Farm → Bus Stop → Town** (or via Backwoods). The east edge BusStop warp
is the canonical "leave for town" exit on every vanilla farm.
