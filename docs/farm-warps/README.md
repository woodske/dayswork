# Farm Warp Reference

A one-time deep-dive mapping every **exit** (farm → neighbour) and **entrance**
(neighbour → farm) warp for the vanilla farms and the three SVE farm maps.

The goal: stop re-deriving farm exit/entrance tiles every time worker pathing
breaks. Everything here was extracted directly from the actual map files (not
memory, not the wiki), so the coordinates are authoritative for the installed
content versions.

## Files

| File | Covers |
|------|--------|
| [`vanilla-farms.md`](vanilla-farms.md) | Standard, Riverland, Forest, Hill-top, Wilderness, Four Corners, Beach, Meadowlands |
| [`sve-grandpas-farm.md`](sve-grandpas-farm.md) | SVE Grandpa's Farm (`flashshifter.GrandpasFarm`) |
| [`sve-frontier-farm.md`](sve-frontier-farm.md) | SVE Frontier Farm (`flashshifter.FrontierFarm`) |
| [`sve-immersive-farm-2.md`](sve-immersive-farm-2.md) | SVE Immersive Farm 2 Remastered (`flashshifter.immersivefarm2remastered`) |

## How to read the tables

There are **two distinct warp mechanisms** in Stardew maps, and a farm uses both:

1. **Edge warps** — the map's `Warp` map-property string. Format is repeated
   `sourceX sourceY TargetMap targetX targetY`. These fire when the player steps
   onto the source tile (often a `-1` / past-the-edge tile). This is what
   `GameLocation.warps` exposes at runtime.
2. **`TouchAction: LoadMap` tiles** — per-tile properties on the `Back` layer
   (`LoadMap TargetMap targetX targetY [facing]`). These are *pedestrian paths*
   walked onto mid-map. **They are NOT in `GameLocation.warps`** — they're handled
   separately by the engine. SVE relies on these heavily, which is exactly why a
   `.warps`-only exit search misses the real route.
3. **`Action: Warp` tiles** — interaction warps (doors / building entrances),
   listed where relevant.

> ⚠️ For SVE farms the *main* bus-stop/town route is almost always a
> `TouchAction: LoadMap` tile, **not** an edge warp. Searching only `.warps`
> will find a secondary/awkward exit. See each SVE file's "Primary routes".

## Shared vanilla neighbour → Farm arrival tiles

The vanilla neighbour maps (BusStop, Backwoods, Forest, FarmCave) are shared
across all vanilla farm types. Their return warps point at fixed `Farm` tiles:

| From | Neighbour source tiles | Arrives on Farm |
|------|------------------------|-----------------|
| Bus Stop | BusStop `(9,22..25)`, `(-1,22..26)` | **`Farm (79,17)`** |
| Backwoods | Backwoods `(13..15,40)` | **`Farm (40,0)`** |
| Cindersap Forest | Forest `(67..72,-1)` | **`Farm (41,64)`** |
| Farm Cave | FarmCave `(8,12)` | **`Farm (34,6)`** |

> Note: these raw values are authored for the standard-position farms. For the
> larger Beach (110×110) and Meadowlands (100×75) maps the game relocates the
> farmhand to the nearest passable tile, so the *effective* arrival can differ.
> Treat the farm's own exit tiles (below) as the reliable anchor and verify
> arrival in-game when it matters.

## Regenerating this data

Tooling lives in [`.tools/MapWarpDump/`](../../.tools/MapWarpDump). It loads maps
with the game's own `xTile.dll` (for `.tbin`) / MonoGame `ContentManager` (for
compiled `.xnb`) and a small Python parser for Tiled `.tmx`, then dumps warp
JSON. See that folder for the exact commands. Requires the game installed and
`dotnet`/`python` on PATH; the game path is hard-coded in the tool's csproj.
