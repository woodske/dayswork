# Multiplayer, ownership, experience, and painting — verified engine facts

Confirmed 2026-09-07 against `X:\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll`
(SDV 1.6.15.24356) with `ilspycmd`, and against the SMAPI XML docs at
`X:\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.xml`. Written while planning
Dayswork 2.0 (`docs/plans/dayswork-2.0.md`). The facts the 2.0 brief itself verified (netcode
type resolution, `Farmer.Money` throw, `Building.modData` sync, always-active Farm interiors) are
not repeated here; this file holds the **additional** facts found while checking the brief's
design against the decompile.

## `Farmer.IsLocalPlayer` is identity-based, and it gates worker-facing vanilla behaviour

`Farmer.cs:1877` — `IsLocalPlayer` returns true when `UniqueMultiplayerID ==
Game1.player.UniqueMultiplayerID` (or the farmer is the current event's farmer). It is **not** a
reference check. `Farmer.CreateFakeEventFarmer()` (`Farmer.cs:9189`) copies `UniqueMultiplayerID`
from the source farmer, so the mod's action farmer built from `Game1.player` has
`IsLocalPlayer == true` today.

That flag gates behaviour the worker depends on:

| Site | What `IsLocalPlayer` gates |
|---|---|
| `Object.cs:4626` (`CheckForActionOnMachine` collect) | `heldObject.Value = null` and the item hand-off happen **only** when `who.IsLocalPlayer`. A non-local action farmer would leave the machine full and collect nothing. |
| `Tree.cs:1498` / `1514` (`performToolAction`) | Foraging XP (`gainExperience(2, 14)` on fell, `(2, 2)` on stump), the `TreesChopped` stat, and `shakeLeft` are computed only when `getLastFarmerToUse().IsLocalPlayer`. |
| `Object.cs:6948` (planting) | Seed-planting success path. |

**Consequence for a sponsor model:** the action farmer must keep the host's
`UniqueMultiplayerID`. Building it from a remote owner's `Farmer` flips `IsLocalPlayer` to false and
breaks machine collection. Sponsor identity has to enter through money, tools, shipping, and XP
routing — not through the fake farmer's identity.

## `Farmer.gainExperience` — where XP actually goes

`Farmer.cs:3027`:

```csharp
if (which == 5 || howMuch <= 0) return;
if (!IsLocalPlayer && Game1.IsServer) { queueMessage(17, Game1.player, which, howMuch); return; }
// … Level >= 25 → Game1.stats.Increment("MasteryExp", …) (+ mastery-level global message/sound)
int num = checkForLevelGain(experiencePoints[which], experiencePoints[which] + howMuch);
experiencePoints[which] += howMuch;
// … on level gain: farmingLevel/foragingLevel/… .Value = num; newLevels.Add(new Point(which, i));
//     Game1.showGlobalMessage("…NewIdeas") when newLevels.Count == 1
```

- **Local farmer:** applied directly, with level-up bookkeeping (`newLevels`, skill level fields,
  optional `MasteryExp` stat, a corner-textbox HUD message via `Game1.showGlobalMessage` →
  `addHUDMessage`, `Game1.cs:3569`).
- **Remote *online* farmer, called on the host:** enqueued as game message type **17** on that
  farmer's `messageQueue`. `GameServer.sendMessages()` (`GameServer.cs:347`) flushes the queues of
  `Game1.otherFarmers` only. The client applies it in `Multiplayer.receiveFarmerGainExperience`
  (`Multiplayer.cs:1455`) as `Game1.player.gainExperience(which, howMuch)` — full level-up UI on
  the owner's side, no mod message needed.
- **Offline farmer (from `farmhandData`), called on the host:** same `queueMessage` branch, but the
  farmer is not in `otherFarmers`, so the queue is **never flushed** — the XP is silently dropped.
  Granting XP to an offline owner therefore requires either writing `experiencePoints` directly
  (re-implementing level-up) or **banking** the XP and granting it when the owner reconnects.
- `Game1.IsServer` is `multiplayerMode == 2` (`Game1.cs:1805`); true for the host in co-op and
  split-screen, false in pure single-player (where every farmer is local anyway).
- `Crop.harvest(int xTile, int yTile, HoeDirt soil, JunimoHarvester junimoHarvester = null, bool
  isForcedScytheHarvest = false)` (`Crop.cs:482`) has **no `Farmer` parameter**; its XP calls are
  `Game1.player.gainExperience` (`Crop.cs:533, 549, 693`). Neutralizing that leak means
  snapshotting/restoring `Game1.player.experiencePoints[..]`, the five skill-level `NetInt`s,
  `newLevels.Count`, and `Game1.stats["MasteryExp"]` around the beat, then trimming the HUD queue
  (the level-up message lands in `Game1.hudMessages`, which the existing guard already trims).

## `Building.owner`

- `Building.owner` is a `NetLong` (`Building.cs:141`), added to `NetFields` (`Building.cs:398`),
  XML-serialized as `owner`.
- Neither `Building()` nor `Building(string type, Vector2 tile)` (`Building.cs:207, 217`) sets it.
  **It is set in `GameLocation.buildStructure(Building, Vector2, Farmer who, …)`**
  (`GameLocation.cs:16885`: `building.owner.Value = who.UniqueMultiplayerID`). Every building placed
  through the carpenter flow — including the office in every 1.x save — has its builder's id; a
  building placed by any path that bypasses `buildStructure` has `owner == 0`. Treat `0` as
  "host-owned".
- The game uses it for `Building.hasCarpenterPermissions()` (`Building.cs:364`): host always,
  otherwise `owner == Game1.player.UniqueMultiplayerID`.

## Player lookup on the host

- `Game1.GetPlayer(long id, bool onlyOnline = false)` (`Game1.cs:10937`) returns the master player,
  an online farmhand from `otherFarmers`, or (unless `onlyOnline`) the offline `Farmer` from
  `netWorldState.Value.farmhandData`; `null` if the id is unknown (farmhand slot deleted).
- `Game1.getFarmer(long id)` (`Game1.cs:10922`) is **online-only and falls back to `MasterPlayer`**
  — do not use it for owner resolution; a missing owner would silently become the host.
- `getAllFarmhands()` / `getOfflineFarmhands()` (`Game1.cs:10967, 10983`) enumerate `farmhandData`.

## Shipping

- `Farm.getShippingBin(Farmer who)` (`Farm.cs:1024`): `who.personalShippingBin` under separate
  wallets, else `sharedShippingBin`.
- `Farm.shipItem(Item i, Farmer who)` (`Farm.cs:1033`) calls `who.removeItemFromInventory(i)` then
  `getShippingBin(who).Add(i)` then `showShipment` — so passing the **owner** as `who` routes to
  the owner's bin while keeping the lid animation. (It also pokes `Game1.player.showNotCarrying()`
  when the local player holds nothing — harmless.)

## `FarmerTeam` money

- `GetMoney(Farmer who)` (`FarmerTeam.cs:625`) returns the shared `money` `NetIntDelta` when
  `useSeparateWallets` is false, else the per-player entry in `individualMoney` (created on demand
  with 500g, `Minimum = 0`). `AddIndividualMoney` / `SetIndividualMoney` are thin wrappers on it.
  One code path (`team.GetMoney(owner).Value ± n`) covers both wallet modes and survives a mid-save
  wallet switch.

## Painting a custom building

- **There is no "Paint Bucket" item in vanilla 1.6.** Painting is reached through Robin's
  carpenter menu (`CarpenterMenu.CarpentryAction.Paint`, `CarpenterMenu.cs:530`, `990-1007`), which
  opens `BuildingPaintMenu` when `building.CanBePainted()`.
- `Building.CanBePainted()` (`Building.cs:256`) returns `GetPaintDataKey() != null` (after the
  greenhouse/farmhouse special cases). `GetPaintDataKey` (`Building.cs:303`) looks up
  **`Data/PaintData`** by skin id, else by `buildingType` (with `Farmhouse`→`House`,
  `Cabin`→`Stone Cabin` remaps). **A `_PaintMask` texture alone is not enough** — the building type
  must have a `Data/PaintData` entry or the paint button is refused with
  `Strings\UI:Carpenter_CannotPaint`.
- `Data/PaintData` value format (parsed in `BuildingPaintMenu.LoadRegionData`,
  `BuildingPaintMenu.cs:862`): newlines/tabs stripped, split on `/`, consumed in pairs
  `<RegionName>/<minBrightness> <maxBrightness>` (defaults −100..100 when the pair is absent or
  unparsable). Region display name is `Strings/Buildings:Paint_Region_<RegionName>` if that string
  exists, else the raw region name. Three regions maximum (the painter's mask has three channels).
- `BuildingPainter.Apply(Texture2D base, string maskPath, BuildingPaintColor color)`
  (`BuildingPainter.cs`) loads `maskPath` once (cached in `paintMaskLookup`, `null` cached on load
  failure) and recolors pixels whose mask colour is exactly `Color.Red` (region 1), `Color.Lime`
  (region 2), `Color.Blue` (region 3) by HSL shift. Returns `null` when there is no mask or
  `!color.RequiresRecolor()`.
- `Building.textureName()` (`Building.cs:845`) is skin texture → `BuildingData.Texture` →
  `Buildings\<type>`; `resetTexture()` (`Building.cs:851`) applies the painter with
  `textureName() + "_PaintMask"`. For the office that is
  `Mods/Bindicle.Dayswork/Building_PaintMask`.
- `CarpenterMenu.HasPermissionsToPaint(Building)` (`CarpenterMenu.cs:549`) restricts only cabins and
  the farmhouse; any player may paint any other building, offices included.
- `netBuildingPaintColor` is a synced, serialized `NetRef<BuildingPaintColor>`
  (`Building.cs:89, 400`), lazily created in the constructor (`Building.cs:434`). Nothing extra to
  persist or sync.

## Custom NPC on clients

- `Character.modData` (`Character.cs:514`) is a `ModDataDictionary` added to `NetFields`
  (`Character.cs:559`) — net-synced, so a host-written entry is readable on every client and
  survives the parameterless-constructor path.
- `Character.displayName` (`Character.cs:290`) is a **plain field**, not synced; on a client it
  falls back to `translateName()` → `NPC.GetDisplayName(name)` → effectively the raw `Name` for an
  NPC without a `Data/Characters` entry. A per-worker display name must be carried in `modData` and
  read by an override.
- `AnimatedSprite.textureName` is a synced `NetString` (`AnimatedSprite.cs:25, 188`);
  `LoadTexture(name, syncTextureName)` sets it (or the local-only `overrideTextureName` when
  `false`). `NPC.reloadSprite` → `ChooseAppearance` → `getTextureName()` (`NPC.cs:1101, 1170`) can
  re-derive the sprite from `Name`, which is why the 2026-07-08 build overrode `getTextureName()`.
- `NPC.update` gates schedule/controller movement and `animateOnce` on `Game1.IsMasterGame`
  (`NPC.cs:3187, 3317, 3369`); clients interpolate the synced position. Still verify in-game that a
  `FarmhandNpc` constructed on a client is inert (no controller, no local pathing).

## Kicking a peer

`GameServer.kick(long disconnectee)` (`GameServer.cs:401`) exists on `Game1.server` and disconnects
one peer. Also available: `Multiplayer.sendChatMessage(LanguageCode, string, long recipientID)`
(`Multiplayer.cs:1127`) and `globalChatInfoMessageEvenInSinglePlayer` (`Multiplayer.cs:1178`) for
text that reaches peers without the mod.

## SMAPI multiplayer API (from `StardewModdingAPI.xml`)

- `IMultiplayerEvents.PeerContextReceived` — "raised after the mod context for a peer is received.
  This happens before the game approves the connection (`PeerConnected`), so the player doesn't yet
  exist in the game. This is the earliest point where messages can be sent to the peer via SMAPI."
- `PeerConnected` — "after a peer connection is approved by the game." `PeerDisconnected` — "after
  the connection with a peer is severed."
- `IMultiplayerPeer`: `PlayerID`, `IsHost`, `IsSplitScreen`, `HasSmapi`, `ScreenID`, `Platform`,
  `GameVersion`, `ApiVersion`, `Mods`, `GetMod(string id)` → `IMultiplayerPeerMod { Name, ID,
  Version }`. Platform/version/mod fields are meaningful only when `HasSmapi`.
- Vanilla (no-SMAPI) peers: SMAPI's `SMultiplayer` remarks — if the host receives `PlayerIntro`
  before any `ModContext` it stores a "vanilla player" context. Whether that raises
  `PeerContextReceived` (as opposed to only `PeerConnected` with `HasSmapi == false`) is **not
  stated** in the docs; treat as unverified and check in-game.
- `Context.IsMainPlayer` — true in single-player and for the host. `Context.IsSplitScreen`,
  `Context.IsOnHostComputer`, `Context.HasRemotePlayers`, `Context.ScreenId` exist;
  `Context.IsMultiplayer` is true for split-screen too. `Utilities.PerScreen<T>` manages a value per
  split-screen player and is safe outside split-screen.
- `IMultiplayerHelper.SendMessage<T>(message, messageType, modIDs, playerIDs)` — target by mod id
  and player id; `ModMessageReceivedEventArgs` carries `FromPlayerID`, `FromModID`, `Type`,
  `ReadAs<T>()`.
