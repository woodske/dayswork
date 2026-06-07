# SVE Frontier Farm — Warp Reference

- **Mod:** `flashshifter.FrontierFarm` (Content Patcher pack "[CP] Frontier Farm")
- **Map asset:** `Maps/Farm` ← loaded from `Assets/Maps/FrontierFarm.tmx`
- **Size:** 156 × 65
- **Farm signature in code:** `FarmMapSignature(156, 65)` → entrance override `(142,16)`
  in [`SveExpansionProfile.cs`](../../Dayswork.Core/Compat/SveExpansionProfile.cs)

> Unlike Grandpa's Farm, Frontier's main bus-stop exit **is an edge warp** (it
> shows up in `GameLocation.warps`). The only `TouchAction`/`Action` warp is the
> shed door. Nice — `.warps`-based exit search mostly works here.

## Primary routes (for town shopping)

| Direction | Use these tiles | Lands at |
|---|---|---|
| **Farm → Bus Stop** (main) | `(144,15) (144,16) (144,17)` (also `(156,15..17)`) | BusStop `(11,23)` |
| **Bus Stop → Farm** (main entrance) | — | **Farm `(142,16)`** |
| Bus Stop → Farm (west edge) | — | Farm `(79,17)` |

No direct Farm→Town warp; route is Farm → Bus Stop → Town.

## Exits — edge warps (`Warp` map property)

| Exit tiles (Farm) | → Destination |
|---|---|
| `(144,15) (144,16) (144,17)` and `(156,15) (156,16) (156,17)` | BusStop `(11,23)` |
| `(73,4)` | FarmCave `(8,11)` |
| `(135,-1) (136,-1)` | Backwoods `(14,38)` |
| `(134,65)…(138,65)` | Forest `(69,1)` |
| `(87,65) (88,65) (89,65)` | Forest `(39,6)` |
| `(43,61)…(47,61)` | Woods `(67,1)` |
| `(-1,41)` | Custom_FerngillRepublicFrontier `(145,31)` |
| `(-1,42) (-1,43)` | Custom_FerngillRepublicFrontier `(145,32)` |
| `(131,8)` | Custom_FrontierFarm_HiddenCave `(16,10)` |
| `(103,4)` | Custom_FrontierFarm_UndergroundTunnel `(60,35)` |

Extra property: `WarpTotemEntry = 112 4`.

## Interaction warps (`Action`)

| Tile | Action |
|---|---|
| `(18,21)` | `Warp 15 16 Custom_GrandpasShedRuins` (shed entrance) |

## Entrances — neighbour → Farm (from this pack's MapPatches)

| From | Neighbour source | Arrives on Farm |
|---|---|---|
| Bus Stop (bus platform) | BusStop `(0,0..3) (9,0..3) (10,0..3)` | **`(142,16)`** |
| Bus Stop (west edge) | BusStop `(9,22..25)`, `(-1,22..26)` | `(79,17)` |
| Woods (shortcut) | Woods-shortcut `(3,0) (4,0)` | `(45,60)` |
| Forest (shortcut) | Forest-shortcut `(2,0) (3,0)` | `(89,63)` |
| Ferngill Republic / Underground Tunnel | desert shortcut | `Custom_FrontierFarm_UndergroundTunnel (59,0)` |
