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
