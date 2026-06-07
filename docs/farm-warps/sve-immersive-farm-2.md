# SVE Immersive Farm 2 Remastered (IF2R) — Warp Reference

- **Mod:** `flashshifter.immersivefarm2remastered` (CP pack "[CP] Immersive Farm 2 Remastered")
- **Map asset:** `Maps/Farm` ← loaded from `assets/Maps/IF2R.tmx`
- **Size:** 163 × 156 (largest farm map)
- **Farm signature in code:** `FarmMapSignature(163, 156)` —
  **no entrance override currently set** in
  [`SveExpansionProfile.cs`](../../Dayswork.Core/Compat/SveExpansionProfile.cs).
  If one is ever needed, the main bus entrance is **`(128,30)`** (see below).

> IF2R is a big multi-zone map with many *internal* `Farm → Farm` warps that move
> between sub-areas (cliff, lower fields, minecart cave network). Don't confuse
> those with real map exits — they're listed separately below.

## Primary routes (for town shopping)

| Direction | Use these tiles | Lands at |
|---|---|---|
| **Farm → Town** (direct) | `(162,43) (162,44) (162,45) (162,46)` `TouchAction` | Town `(0,54)` |
| **Farm → Bus Stop** (north platform) | `(126,15)…(130,15)` `TouchAction` | BusStop `(23,20)`–`(25,20)` |
| Farm → Bus Stop (edge, west) | `(89,17) (89,18) (89,19) (89,20)` | BusStop `(11,23)` |
| Farm → Bus Stop (edge, south) | `(127,28) (128,28) (129,28)` | BusStop `(23,28)` |
| **Bus Stop → Farm** (main entrance) | — | **Farm `(128,30)`** |
| Bus Stop → Farm (west edge) | — | Farm `(79,17)` |

## Exits — edge warps (`Warp` map property)

| Exit tiles (Farm) | → Destination |
|---|---|
| `(89,17) (89,18) (89,19) (89,20)` | BusStop `(11,23)` |
| `(127,28) (128,28) (129,28)` | BusStop `(23,28)` |
| `(63,78) (64,78) (65,78)` | Forest `(69,1)` |
| `(39,-1) (40,-1) (41,-1)` | Backwoods `(14,39)` |
| `(136,93)` | FarmCave `(8,11)` |

## Exits — `TouchAction: LoadMap` tiles (Back layer)

| Exit tiles (Farm) | → Destination |
|---|---|
| `(126,15)…(130,15)` | BusStop `(23,20)` / `(24,20)` / `(25,20)` |
| `(162,43)…(162,46)` | Town `(0,54)` |
| `(29,113) (30,113) (31,113)` | Woods `(67,1)` |
| `(76,7)` | Custom_MinecartCave `(6,9)` |

## Interaction warps (`Action`)

| Tile | Action |
|---|---|
| `(144,33)` | `Warp 15 16 Custom_GrandpasShedRuins` (shed entrance) |
| `(4,26)` | `Warp 5 4 Custom_MinecartCave` |
| `(126,93)` | `Warp 8 4 Custom_MinecartCave` |

## Internal sub-area transitions (Farm → Farm)

| Tiles | → Farm tile |
|---|---|
| `(91,17)…(91,20)` | `(88,18)` |
| `(127,27) (128,27) (129,27)` | `(128,30)` |
| `(152,147) (152,148) (152,149)` | `(142,104)` |
| `(142,106) (143,105) (143,106)` | `(149,148)` |
| `(41,64)` | `(64,77)` |
| `(15,51) (15,52)` (Action) | `(9,148)` |
| `(8,148) (8,149)` (Action) | `(14,51)` |

## Entrances — neighbour → Farm (from this pack's MapPatches)

| From | Neighbour source | Arrives on Farm |
|---|---|---|
| Bus Stop (path) | `busstoppath_IF2R (2,5) (3,5) (4,5)` | **`(128,30)`** |
| Bus Stop (west edge) | BusStop `(9,22..25)`, `(-1,22..26)` | `(79,17)` |
| Woods | Woods-warps `(3,0) (4,0)` | `(30,111)` |
| Greenhouse (cleared) | `(14,32)` | `(44,51)` |
