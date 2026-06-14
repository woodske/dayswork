# Store interior tile reference

Confirmed game content for the stores the farmhand visits during managed-crop shopping trips.
Verified via `ilspycmd` decompile of `Stardew Valley.dll` (`GameLocation.checkAction`).

## Purchase sound

| Sound cue | Trigger |
|---|---|
| `"purchaseClick"` | Plays once per line item when the worker buys at the counter. Confirmed in-game. This is the same sound the player hears when buying from any vanilla shop menu. |

## JojaMart

| Tile purpose | Layer | Action property | Coordinates |
|---|---|---|---|
| Joja shop counter (product purchase) | Buildings | `JojaShop` | TBD — read DevLog after a Joja trip with `DevLog.Enabled = true` |
| Morris / membership sign-up | Buildings | `JoinJoja` | TBD |
| Entrance warp arrival | — | — | ~`(12, 23)` (verify in-game) |

**Key finding:** JojaMart uses `"JojaShop"` as the tile action for the product counter, *not* `"OpenShop Joja"`. It is handled by `GameLocation.checkAction` as a special case that calls `Utility.TryOpenShopMenu("Joja", ...)`. The fallback in `FindStoreCounterStandTile` is `(13, 23)` (update once counter tile is confirmed from DevLog).

## Pierre's (SeedShop)

| Tile purpose | Layer | Action property | Coordinates |
|---|---|---|---|
| Seed shop counter | Buildings or Back | `Buy General` or `OpenShop SeedShop` | `(4, 17)` |

Confirmed via `GameLocation.checkAction` decompile:
```csharp
Utility.TryOpenShopMenu("SeedShop", this, new Rectangle(4, 17, 1, 1), ...);
```
