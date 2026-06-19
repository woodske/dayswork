# Game Content Search Guide

This file is the quick map for future game-content verification. Use it before spending tokens
rediscovering where Stardew Valley or Stardew Valley Expanded files live.

## Local paths

| Content | Path |
|---|---|
| Stardew Valley install | `X:\Steam\steamapps\common\Stardew Valley` |
| Stardew Valley Expanded source | `C:\Users\kwood\Repos\StardewValleyExpanded` |

When a task depends on ids, tiles, qualified item ids, building types, event keys, map names, or
runtime API behavior, verify against these local files, runtime game data, or a decompile. Record
newly confirmed facts in `docs/` so they are not re-derived later.

## Base Game Structure

Important roots under `X:\Steam\steamapps\common\Stardew Valley`:

| Path | Use |
|---|---|
| `Stardew Valley.dll` | Main game behavior. Decompile for runtime logic, methods, fields, and special cases. |
| `StardewValley.GameData.dll` | Strongly typed game-data models such as `MachineData`, location data, and item data DTOs. |
| `Stardew Valley.xml`, `StardewValley.GameData.xml` | XML API docs beside the DLLs. Useful for quick member summaries. |
| `Content\Data` | Base game data assets, mostly compiled `.xnb` files. |
| `Content\Maps` | Base map assets, mostly compiled `.xnb` maps. |
| `Content\Buildings` | Building art and related assets. |
| `Content\Characters`, `Content\Portraits` | NPC sprites and portraits. |
| `Content\TileSheets`, `Content\LooseSprites` | Common sprite sheets and UI/world textures. |
| `Content\XACT` | Sound banks and cue data. |

Base game `.xnb` files are compiled assets, not text. For exact values, prefer one of these:

- Runtime/SMAPI access through `helper.GameContent.Load<T>("Data/...")`, `DataLoader.*`, or a small
  in-game/debug probe.
- Decompilation for behavior/API questions with `ilspycmd`, especially when the value is produced
  by code rather than authored data.
- Game-aware asset tooling for maps and `.xnb` assets, such as xTile/MonoGame content loading. Do
  not guess map properties from memory.

Useful decompile targets:

```powershell
ilspycmd -t StardewValley.Object "X:\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll"
ilspycmd -t StardewValley.GameData.Machines.MachineData "X:\Steam\steamapps\common\Stardew Valley\StardewValley.GameData.dll"
```

## SVE Structure

Important roots under `C:\Users\kwood\Repos\StardewValleyExpanded`:

| Path | Use |
|---|---|
| `Stardew Valley Expanded\[CP] Stardew Valley Expanded` | Main Content Patcher pack. Most SVE data, maps, edits, and tokens live here. |
| `Stardew Valley Expanded\StardewValleyExpanded` | Main C# mod source (`FlashShifter.SVECode`). Use for SVE runtime behavior and Harmony/event logic. |
| `Stardew Valley Expanded\[FTM] Stardew Valley Expanded` | Farm Type Manager pack used by the main SVE content. |
| `Grandpa's Farm\[CP] Grandpa's Farm` | Grandpa's Farm Content Patcher pack (`flashshifter.GrandpasFarm`). |
| `Frontier Farm\[CP] Frontier Farm` | Frontier Farm Content Patcher pack (`flashshifter.FrontierFarm`). |
| `Immersive Farm 2 Remastered\[CP] Immersive Farm 2 Remastered` | IF2R Content Patcher pack (`flashshifter.immersivefarm2remastered`). |
| `GrampletonFields` | Grampleton Fields content pack and map assets. |

High-value SVE subfolders:

| Path | Use |
|---|---|
| `[CP] ...\content.json` | Content Patcher entrypoint: config defaults, dynamic tokens, custom locations, and change list. |
| `[CP] Stardew Valley Expanded\code\Items` | SVE item, crop, machine, weapon, recipe, and object edits. |
| `[CP] Stardew Valley Expanded\code\Locations` | Location data, map patches, world map, tracking, and location edits. |
| `[CP] Stardew Valley Expanded\code\Shops` | Vanilla and SVE shop edits. |
| `[CP] Stardew Valley Expanded\code\OtherEvents` | Event/token-driven unlocks such as Grandpa's Shed. |
| `[CP] ...\assets\Maps` | `.tmx` maps for locations, buildings, festivals, map patches, and farm maps. |

SVE `content.json` files are JSON with comments plus Content Patcher semantics. Treat them as
instructions that may be conditional, not as final resolved game state. Before trusting a value,
trace the relevant:

- `ConfigSchema` defaults and allowed values.
- `DynamicTokens` and the events/conditions behind them.
- `Changes` entries, especially `Action`, `Target`, `FromFile`, `Entries`, `When`, `Update`,
  `MoveEntries`, `ToArea`, and priority.
- Included or split files under `code/`.
- Farm-pack overrides for `Maps/Farm`, `Data/AdditionalFarms`, farm cave/greenhouse maps, and
  bus-stop/path patches.

## Search Tips

Use `rg` first for text search:

```powershell
rg -n "FlashShifter.StardewValleyExpandedCP|GrandpaShedComplete|2554906" "C:\Users\kwood\Repos\StardewValleyExpanded"
rg -n '"Target": "Data/Machines"|"Target": "Data/Buildings"' "C:\Users\kwood\Repos\StardewValleyExpanded\Stardew Valley Expanded"
rg -n "TouchAction|LoadMap|Action.*Warp|Warp" "C:\Users\kwood\Repos\StardewValleyExpanded"
```

PowerShell treats square brackets as wildcard syntax in path parameters. For SVE folders named
`[CP] ...` and `[FTM] ...`, use `-LiteralPath`:

```powershell
Get-ChildItem -LiteralPath "C:\Users\kwood\Repos\StardewValleyExpanded\Stardew Valley Expanded\[CP] Stardew Valley Expanded"
Get-Content -LiteralPath "C:\Users\kwood\Repos\StardewValleyExpanded\Grandpa's Farm\[CP] Grandpa's Farm\content.json"
```

For `.tmx` maps, inspect XML map properties and tile properties:

- Map property `Warp` uses repeated `sourceX sourceY TargetMap targetX targetY` groups.
- Tile property `TouchAction` with `LoadMap ...` is a walk-on transition and is not the same as
  `GameLocation.warps`.
- Tile property `Action` with `Warp ...` is usually an interaction/door style warp.
- `EditMap` patches with `ToArea` may only overlay part of a map; inspect both the base map and
  the patch file.

For `.tbin` or base `.xnb` maps, do not parse as text. Use xTile/MonoGame-aware tooling, SMAPI
runtime inspection, or a focused debug command.

## Source Of Truth

- Use decompile results for behavior and method contracts.
- Use runtime-loaded data or game-aware asset extraction for base game `.xnb` values.
- Use SVE source files to understand intended Content Patcher edits, then account for `When`
  conditions, config defaults, dynamic tokens, and farm-specific packs.
- When the current game state matters, prefer runtime inspection because Content Patcher, SVE config,
  farm choice, save flags, and other mods can change the final asset.
