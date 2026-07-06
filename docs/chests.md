# Chests — verified game-content reference

Confirmed against a decompile of `X:\Steam\...\Stardew Valley.dll`
(`StardewValley.Objects.Chest`, `StardewValley.Buildings.Building`) and
`StardewValley.GameData.dll` (`StardewValley.GameData.Buildings.BuildingChest`), 2026-06-30, while
making the office porch chests Big Chests.

## Chest item ids

| Item | Qualified id | Notes |
|---|---|---|
| Wood Chest | `(BC)130` | Default id used by `new Chest(playerChest: true)`. 36 slots. |
| Stone Chest | `(BC)232` | 36 slots — **not** the big chest (a common mix-up). |
| Big Chest | `(BC)BigChest` | String id, not numeric. 70 slots. |
| Big Stone Chest | `(BC)BigStoneChest` | String id. 70 slots. |

`Chest.SetSpecialChestType()` derives `SpecialChestType` from the qualified id: `(BC)BigChest` /
`(BC)BigStoneChest` → `BigChest`; `(BC)248` → `MiniShippingBin`; `(BC)256` → `JunimoChest`;
`(BC)275` → `AutoLoader`.

## Capacity / special type

- `Chest.SpecialChestTypes` (nested enum): `None, MiniShippingBin, JunimoChest, AutoLoader,
  Enricher, Mill (mobile-only), BigChest`.
- Property `Chest.SpecialChestType` (get/set) backs the `specialChestType` `NetEnum` field.
- `Chest.GetActualCapacity()` switches on it: `MiniShippingBin`/`JunimoChest` → 9, `Enricher` → 1,
  **`BigChest` → 70**, default → 36 (`Chest.capacity` const).
- Both the chest UI (`Chest.ShowMenu()`) and item insertion (`Chest.addItem()`) size themselves from
  `GetActualCapacity()`, so setting `SpecialChestType = BigChest` on any existing chest instance is
  sufficient to make it hold 70 — no id swap, no re-creation, no other code.
- `specialChestType` is a serialized net field (`[XmlElement("specialChestType")]`,
  `AddField(specialChestType, ...)`), so the upgrade **persists** in the save once set and saved.

## Auto-Grabber as an input source

Confirmed against a decompile of `X:\Steam\...\Stardew Valley.dll` (`StardewValley.Object`, 2026-07-05)
while making auto-grabbers valid machine **input** chests.

- The Auto-Grabber's qualified id is **`(BC)165`** (big-craftable). It is *not* a `Chest`; it's an
  `Object` whose **`heldObject.Value` is a `Chest`** holding the animal products it collects (wool,
  milk, large egg, truffle, duck feather…), with real quality/flavor intact. The inner chest is
  created on placement (`heldObject.Value = new Chest()`).
- It is **excluded from machine processing**: `Object.minutesElapsed` guards
  `heldObject.Value != null && QualifiedItemId != "(BC)165"` before touching `GetMachineData()`, and
  the grabber has no `Data/Machines` entry. So `MachineReader.EnumerateMachines`
  (`GetMachineData() is not null`) never treats it as a machine — no double-handling.
- Sprite state: the grabber sets `showNextIndex.Value = true` when it grabs (the "full/arrow" sprite);
  `Object.grabItemFromAutoGrabber` resets it to `false` when the inner chest empties. The worker
  mirrors this — after draining a grabber input chest it sets `showNextIndex = false` (and back to
  `true` if leftovers settle back), in `ShiftOrchestrator.Machines.cs`.
- The inner Chest is a *held object* with no world `Location`/`TileLocation`, so anything that needs a
  location/tile (deposit-style audio) must use the grabber object's own position, not the inner
  chest's. `ChestResolver.ResolveGrabberOwner(chestRef)` returns the grabber for exactly this.
- Wiring: `ChestResolver` treats a grabber tile as a resolvable chest (returns `heldObject.Value`) and
  surfaces grabbers in the chest picker **only when `includeAutoGrabbers` is set** — passed solely by
  the machine input-chest picker (`HiringFlowCoordinator.ShowMachineInputChestPicker`), never by
  output/deposit pickers. Subject to the Manage Machines v1 same-location rule: a grabber input chest
  only reloads machines in the *same* location (grabbers live in a coop/barn `AnimalHouse`), else the
  group degrades to collect-only.

## Building chests (`BuildingData.Chests`)

- Declared as `BuildingChest` entries (`Id`, `Type`, optional sounds/messages, `DisplayTile`,
  `DisplayHeight`). The schema has **no** field for capacity, item id, or special type.
- `BuildingChest.Type` is `BuildingChestType` (`Chest` / `Load` / `Collect`) — it controls
  *interaction behavior* (menu vs. item-conversion input vs. single-item collect), **not** size.
- The game instantiates them (in `Building`'s chest-init pass) as plain
  `new Chest(playerChest: true) { Name = <BuildingChest.Id> }` — id `130`, 36 slots,
  `SpecialChestType.None`. That init only *adds* chests whose `Id` is missing and *removes* chests
  whose name isn't in the data; it never resets an existing chest's properties.
- Consequence: a building chest can only be made "big" by setting `SpecialChestType = BigChest` on
  the runtime `Chest` instance after the game creates it — done idempotently in
  `CabinChestService.EnsureOfficeChests` (`SaveLoaded` + `DayStarted`) for the office input/output
  chests, so it also self-heals pre-upgrade saves. Building chests are drawn as part of the building
  sprite (not as world chest sprites), so this has no visual effect beyond the 70-slot grid.
