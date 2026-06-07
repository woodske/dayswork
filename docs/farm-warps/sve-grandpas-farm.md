# SVE Grandpa's Farm — Warp Reference

- **Mod:** `flashshifter.GrandpasFarm` (Content Patcher pack "[CP] Grandpa's Farm")
- **Map asset:** `Maps/Farm` ← loaded from `assets/Maps/GrandpasFarm.tbin`
- **Size:** 140 × 93
- **Farm signature in code:** `FarmMapSignature(140, 93)` → entrance override `(112,51)`
  in [`SveExpansionProfile.cs`](../../Dayswork.Core/Compat/SveExpansionProfile.cs)

> This farm relies on **`TouchAction: LoadMap` pedestrian tiles** for almost every
> real exit. The `Warp` edge property only covers the cave, an unused tunnel, a
> minor bus-stop edge, and the Backwoods top edge. A `.warps`-only exit search
> will miss the bus-stop and town routes entirely.

## Primary routes (for town shopping)

| Direction | Use these tiles | Lands at |
|---|---|---|
| **Farm → Town** (direct) | `(139,19) (139,20) (139,21) (139,22)` `TouchAction` | Town `(1,54)` |
| **Farm → Bus Stop** (main) | `(110,17)…(114,17)` `TouchAction` | BusStop `(24,20)` |
| **Bus Stop → Farm** (main entrance) | — | **Farm `(112,51)`** |
| Farm → Bus Stop (south path) | `(111,49) (112,49) (113,49)` | BusStop `(23,28)` |
| Bus Stop → Farm (west edge) | — | Farm `(79,17)` |

The **`(112,51)` entrance** is the one the codebase already hard-codes as the
worker spawn/entrance override.

## Exits — edge warps (`Warp` map property)

| Exit tiles (Farm) | → Destination |
|---|---|
| `(43,22)` | FarmCave `(8,11)` |
| `(60,3) (60,4) (60,5) (60,6) (60,7)` | Tunnel `(39,9)` |
| `(97,3) (97,4) (97,5) (97,6) (97,7)` | BusStop `(10,8)` |
| `(78,-1)` | Backwoods `(40,24)` |
| `(79,-1)` | Backwoods `(41,24)` |

Extra property: `WarpTotemEntry = 79 16`.

## Exits — `TouchAction: LoadMap` tiles (Back layer)

| Exit tiles (Farm) | → Destination |
|---|---|
| `(49,0)…(53,0)` | Backwoods `(13,24)` |
| `(51,17) (52,17) (53,17)` | Backwoods `(14,38)` |
| `(110,17)…(114,17)` | BusStop `(24,20)` |
| `(91,33) (91,34)` | BusStop `(11,23)` |
| `(111,49) (112,49) (113,49)` | BusStop `(23,28)` |
| `(139,19) (139,20) (139,21) (139,22)` | Town `(1,54)` |
| `(39,92) (40,92)` | Forest `(39,6)` |
| `(67,92)…(72,92)` | Forest `(69,2)` |
| `(17,74) (17,75) (17,76) (17,77)` | Woods `(67,1)` |
| `(85,21)` | Custom_FarmCliff `(16,10)` |

## Internal sub-area transitions (Farm → Farm)

These move the player between zones of the same 140×93 map (cliff, grove, stairs):

| Tiles | → Farm tile |
|---|---|
| `(47,6)…(49,8)` | `(79,16)` |
| `(51,16) (52,16) (53,16)` | `(51,18) (52,18) (53,18)` (stairs) |
| `(91,32) (92,33) (92,34)` | `(89,33)` |
| `(111,48) (112,48) (113,48)` | `(112,51)` |

## Interaction warps (`Action`)

| Tile | Action |
|---|---|
| `(20,21)` | `Warp 15 16 Custom_GrandpasShedRuins` (shed entrance) |
| `(87,92)` | `LockedDoorWarp 6 9 MarnieShed 900 1800` |

## Entrances — neighbour → Farm (from this pack's MapPatches)

| From | Neighbour source | Arrives on Farm |
|---|---|---|
| Bus Stop (bus platform) | BusStop `(22,7) (23,7) (24,7)` | **`(112,51)`** |
| Bus Stop (west edge) | BusStop `(9,22..25)`, `(-1,22..26)` | `(79,17)` |
| Bus Stop (north strip) | BusStop `(0,0..3) (10,0..3)` | `(89,33)` |
| Woods | Woods `(3,0) (4,0)` | `(19,75)` |
| Forest (shortcut) | Forest-shortcut `(2,0) (3,0)` | `(39,84) (40,84)` |
| Expanded-Land layout | (variant) | `(112,38)` |

> The "Expanded Land" variant (after event `8033861`) shifts the bus entrance to
> `(112,38)`. If a save has seen that event, prefer `(112,38)` over `(112,51)`.
