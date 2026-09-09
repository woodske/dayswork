# Game-data index

Every file in this folder is **verified game content** — facts confirmed against the installed
Stardew Valley / SVE files, a decompile, or observed runtime behavior, never recalled from memory
(AGENTS.md hard rule 7). Anything here can be trusted without re-deriving it.

This is *not* where mod design lives: research and tradeoff records go in
[`../analysis/`](../analysis/index.md), implementation plans in [`../plans/`](../plans/index.md),
and the subsystem map in [`../architecture.md`](../architecture.md).

## Files

| File | What it verifies |
|---|---|
| [building-chest-sprites.md](building-chest-sprites.md) | Chest sprite dimensions and draw offsets (16×32 big-craftable source rect, 1×2 tile footprint) for drawing chests on the office building. |
| [chests.md](chests.md) | `Chest` item ids (wood/stone/big), `SpecialChestTypes.BigChest` → 70 slots, and why `BuildingData.Chests` can't express capacity. |
| [crops.md](crops.md) | `Data/Crops` keying, the wild-seed packets (495–498), and `replaceWithObjectOnFullGrown` harvest behavior. |
| [debris-and-drops.md](debris-and-drops.md) | Which `Game1.create*Debris` overloads route loot to `Game1.currentLocation`, the `ResourceClump.destroy()` leak, and verified synchronous fruit/stump versus delayed standing-tree drop timing. |
| [farm-cave.md](farm-cave.md) | `FarmCave` unlock flags (`caveChoice`, `farmCaveReady`), bat/mushroom behavior, and its warp tiles. |
| [farmhand-art.md](farmhand-art.md) | Farmhand sprite/portrait dimensions, frame layout, and verified NPC/farmer animation constants. |
| [fences-and-gates.md](fences-and-gates.md) | `StardewValley.Fence` gate API — `isGate`, `gatePosition` 0/88, `toggleGate`, and the `updateWhenCurrentLocation` auto-close rule. |
| [game-content-search.md](game-content-search.md) | **Start here to verify something new** — local Stardew/SVE paths, folder maps, and how to search/parse `.xnb`, DLLs, CP packs, and `.tmx` maps. |
| [game-flags.md](game-flags.md) | Unlock checks that are easy to get wrong — greenhouse (`greenhouseUnlocked`, not location existence) and friends. |
| [grass-types.md](grass-types.md) | `Grass.grassType` constants (spring/cave/frost/lava/…). |
| [item-ids.md](item-ids.md) | Qualified item ids used by the mod, each with the DLL constant it was confirmed against; mirrored by `Dayswork/GameItemIds.cs`. |
| [machines.md](machines.md) | `Data/Machines` schema + machine runtime API (`GetMachineData`, `PlaceInMachine`, `readyForHarvest`/`heldObject`), plus the `FishPond` API in its "Fish ponds" section. |
| [multi-worker-delayed-debris.md](multi-worker-delayed-debris.md) | Tree trunk drops are emitted later by `Tree.tickUpdate`, outside the guarded action; each tree update is a synchronous emission boundary suitable for precise capture, while overlapping radius sweeps cannot establish ownership. |
| [multiplayer-handshake.md](multiplayer-handshake.md) | Installed SMAPI raises peer context before approval even for vanilla guests; Steam/LAN/Galaxy kicks require mappings created during approval; mod-message broadcasts can also reach the sender. |
| [multiplayer-and-ownership.md](multiplayer-and-ownership.md) | `Farmer.IsLocalPlayer` is identity-based and gates machine collect + tree XP (so the action farmer must keep the host's id); where `gainExperience` sends XP for local/remote/offline farmers; `Building.owner` is set by `buildStructure`; `Building.id` is a serialized, synced per-instance `NetGuid` (survives moving, dies with demolition) and `BuildCondition` defaults to always-available; `GetPlayer` vs `getFarmer`; `getShippingBin`/`shipItem(who)`; `FarmerTeam.GetMoney`; the exact `experiencePoints`/skill-level/`newLevels`/`MasteryExp` fields a worker-beat guard must restore, and that `CreateFakeEventFarmer` copies no XP; painting needs a `Data/PaintData` entry (no vanilla paint bucket exists); `Character.modData`/`AnimatedSprite.textureName` sync and the `Game1.IsMasterGame` gate in `Character.update` that makes a custom NPC inert on clients with no override; `Game1.server.kick` and `Multiplayer.sendChatMessage` (the only arbitrary text a modless guest can read, and it excludes the sender); `Context.IsOnHostComputer` as the split-screen loopback test; SMAPI peer events and `PerScreen`. |
| [npc-emotes.md](npc-emotes.md) | `Character.doEmote` icon indices (`questionMarkEmote`, `emptyCanEmote`, …). |
| [painting.md](painting.md) | `BuildingPainter.Apply` / `BuildingPaintColor` — the exact red/lime/blue mask colours, the flat-index (so same-size) mask rule, the two null-return cases, and why the hue/saturation/lightness numbers read as absolutes. Plus the full vanilla `Data/PaintData` table, the fact that every entry reuses the region names `Building`/`Roof`/`Trim` (already translated in `Strings/Buildings`, so a custom building needs no string edit) and that the two numbers are a lightness slider range; and how the office's own mask is derived from its sprite. |
| [pathing.md](pathing.md) | The verified `isCollidingPosition(character: null, …)` block table (FarmAnimals do **not** block) and how the worker's passability probe, Core `GridPathfinder`, and per-shift cache use it. |
| [scroll-bar-textures.md](scroll-bar-textures.md) | `ShopMenu.ShopCachedTheme` scroll-bar source rects, plus the Android fallback values. |
| [sound-cues.md](sound-cues.md) | How to enumerate cue names from the XACT sound bank, the cue-per-action table, and the worker-sound invariant (`IsLocalPlayer` gotcha). |
| [split-screen-lifecycle.md](split-screen-lifecycle.md) | SMAPI raises load/day/title events per local screen while mod instances are shared; guest lifecycle events must not reset host fleet, upgrades, or day state. |
| [store-interiors.md](store-interiors.md) | Counter/interaction tiles for the shops the worker visits, and the `purchaseClick` purchase sound. |
| [time-and-pacing.md](time-and-pacing.md) | Game-clock constants (700 ms per in-game minute, the 10-minute trigger, ~42 ticks/minute) and the tile→minute walk conversion. |
| [tool-sprite-sheet.md](tool-sprite-sheet.md) | `Game1.toolSpriteSheet` swing-frame Y offsets per heavy tool. |
| [zoom-and-viewport.md](zoom-and-viewport.md) | How the frozen farm view zooms/pans — `desiredBaseZoomLevel`, viewport recompute, zoom-aware mouse coords, pinch input. |

## Subfolders

| Folder | What it covers |
|---|---|
| [farm-warps/](farm-warps/README.md) | Per-farm exit/entrance warp tiles extracted from the actual map files — all vanilla farms plus SVE Grandpa's Farm, Frontier Farm, and Immersive Farm 2 Remastered. |

## Adding to this folder

1. Confirm the fact against game data, a decompile, or observed runtime behavior — see
   [game-content-search.md](game-content-search.md). Never write down something recalled.
2. Note **what** you confirmed it against and **when**, at the top of the file.
3. Add or extend a file here, then add a one-line row above.
4. If the fact is load-bearing enough that a future session must not miss it, also add a bullet to
   `AGENTS.md` → "Verified game-content references".
