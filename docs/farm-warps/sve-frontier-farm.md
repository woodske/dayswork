# SVE Frontier Farm — Warp Reference

- **Mod:** `flashshifter.FrontierFarm` (Content Patcher pack "[CP] Frontier Farm")
- **Map asset:** `Maps/Farm` ← loaded from `Assets/Maps/FrontierFarm.tmx`
- **Size:** 156 × 65
- **Farm signature in code:** `FarmMapSignature(156, 65)` → entrance override `(142,16)`
  in [`SveExpansionProfile.cs`](../../Dayswork.Core/Compat/SveExpansionProfile.cs)

> Unlike Grandpa's Farm, Frontier's main Farm -> Bus Stop exit is an edge warp
> (it shows up in `GameLocation.warps`). The Bus Stop -> Farm return has an SVE
> tile `TouchAction` overlay, so route code should prefer tile actions over the
> base Bus Stop map-property warps when they tie.

## Verified sources

- `Frontier Farm/[CP] Frontier Farm/Assets/Maps/FrontierFarm.tmx`: farm edge
  warps at `(144,15..17)` and `(156,15..17)` land at BusStop `(11,23)`.
- `Frontier Farm/[CP] Frontier Farm/content.json`: edits `Maps/BusStop` from
  `Assets/MapPatches/FrontierFarm_BusStopWarps.tmx` with `ToArea X=0 Y=22
  Width=27 Height=8` when `FarmType` is `FrontierFarm`.
- `Frontier Farm/[CP] Frontier Farm/Assets/MapPatches/FrontierFarm_BusStopWarps.tmx`:
  source tiles `(0,0..3)`, `(9,0..3)`, and `(10,0..3)` have
  `TouchAction LoadMap Farm 142 16 3`; after the `ToArea` offset, those are
  BusStop `(0,22..25)`, `(9,22..25)`, and `(10,22..25)`.
- `Stardew Valley Expanded/[CP] Stardew Valley Expanded/assets/Maps/Locations/BusStop.tmx`:
  the base SVE Bus Stop still has map-property `Warp` entries at
  `(9,22..25)` and `(-1,22..26)` to Farm `(79,17)`. On Frontier, the tile
  `TouchAction` above is the natural player-facing farm entrance and should win.

## Primary routes (for town shopping)

| Direction | Use these tiles | Lands at |
|---|---|---|
| **Farm → Bus Stop** (main) | `(144,15) (144,16) (144,17)` (also `(156,15..17)`) | BusStop `(11,23)` |
| **Bus Stop → Farm** (natural/main entrance) | `TouchAction` at BusStop `(0,22..25) (9,22..25) (10,22..25)` | **Farm `(142,16)`** |
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
| Bus Stop (Frontier overlay) | BusStop `(0,22..25) (9,22..25) (10,22..25)` `TouchAction` | **`(142,16)`** |
| Bus Stop (base map-property edge) | BusStop `(9,22..25)`, `(-1,22..26)` `Warp` | `(79,17)` |
| Woods (shortcut) | Woods-shortcut `(3,0) (4,0)` | `(45,60)` |
| Forest (shortcut) | Forest-shortcut `(2,0) (3,0)` | `(89,63)` |
| Ferngill Republic / Underground Tunnel | desert shortcut | `Custom_FrontierFarm_UndergroundTunnel (59,0)` |
