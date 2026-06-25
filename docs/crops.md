# Verified crop / seed content

Source: `Stardew Valley.dll` v1.6 decompiled via ilspycmd. All IDs are unqualified object IDs
(prefix `(O)` for the qualified form). Keys in `Data/Crops` are the **seed item ID**.

## Seasonal wild seed packets (items 495–498)

These are the "Spring/Summer/Fall/Winter Seeds" items. They are **wild seed crops** — identified
in game code by `Crop.isWildSeedCrop()` checking `rowInSpriteSheet.Value == 23` (i.e., their
`Data/Crops` entry has `SpriteIndex = 23`).

When planted they grow normally, but at maturity the game **removes the crop from HoeDirt** and
places a forage object on the tile via `replaceWithObjectOnFullGrown` (set by
`Crop.getRandomWildCropForSeason`). `HarvestItemId` in their `Data/Crops` entry is populated with
a **forage placeholder ID** (e.g., `"16"` Wild Horseradish for Spring Seeds), not null — but this
placeholder is not the actual harvest item (which is random at runtime).

| Seed item | ID  | Season | HarvestItemId (placeholder) | Runtime forage produce (random one of) |
|-----------|-----|--------|-----------------------------|----------------------------------------|
| Spring Seeds | 495 | Spring | `"16"` (Wild Horseradish) | `(O)16`, `(O)18`, `(O)20`, `(O)22` |
| Summer Seeds | 496 | Summer | (not confirmed) | `(O)396`, `(O)398`, `(O)402` |
| Fall Seeds   | 497 | Fall   | (not confirmed) | `(O)404`, `(O)406`, `(O)408`, `(O)410` |
| Winter Seeds | 498 | Winter | (not confirmed) | `(O)412`, `(O)414`, `(O)416`, `(O)418` |

Source: `Crop.getRandomWildCropForSeason(Season season)` switch in decompiled DLL. Spring Seeds
placeholder confirmed by observing "Wild Horseradish" appear in the crop picker before fix.

**Mod handling**: `CropCatalogProvider` detects wild seed crops by `data.SpriteIndex == 23` (same
check as `Crop.isWildSeedCrop()`) and uses `seedId` as both `CropItemId` and the display name
source, so they appear in the picker as "Spring Seeds" / "Summer Seeds" etc.

## Sprinkler detection on a crop tile

`StardewValley.Object.IsSprinkler()` (public virtual bool, `Object.cs` line ~6071 in the v1.6
decompile) returns `GetBaseRadiusForSprinkler() >= 0`. Sprinklers are **regular objects**
(not big-craftables) stored in `GameLocation.objects`, keyed by the tile `Vector2`.

**Mod handling**: `ManagedCropFieldReader.HasSprinkler` / `IsPlantableGroundTile` use this to skip
sprinkler tiles entirely — a sprinkler tile is the player's own fixture, so the farmhand must never
till, plant on, or clear it. (Before this, a sprinkler sat in `location.objects` and was read as
`HasDebris`, which queued a `ClearDebris` action that would destroy the sprinkler.) The same helper
backs the Draw-Zones valid-tile count **and per-tile highlight** for managed-crop groups: the crop
draw layer fills only individual plantable tiles, not the whole drawn rectangle.

`GameLocation.doesTileHaveProperty(x, y, "Diggable", "Back")` is **bounds-safe for off-map tiles** —
it resolves `map.GetLayer(...)?.Tiles[x, y]`, and xTile's `TileArray` indexer returns `null` for
out-of-range coordinates (no throw). So `IsPlantableGroundTile` can be called on any tile in a drawn
rectangle (including the black off-map area) and simply returns `false` there.

## Regular spring crop seed IDs (partial — confirmed from game code)

From `Crop.getRandomLowGradeCropForThisSeason(Season.Spring)` → `random.Next(472, 476)`:

| ID  | Item |
|-----|------|
| 472 | Parsnip Seeds |
| 473 | Bean Starter |
| 474 | Cauliflower Seeds |
| 475 | Potato Seeds |

These are the "low-grade" spring crops used internally by Mixed Seeds (770) resolution. The full
set of spring plantable seeds (kale, garlic, rhubarb, strawberry, tulip, coffee, etc.) comes from
`DataLoader.Crops()` at runtime and is not enumerated here — they are loaded dynamically.

Spring flower seed IDs (from `Crop.getRandomFlowerSeedForThisSeason(Season.Spring)`):

| ID  | Item |
|-----|------|
| 427 | (spring flower seed — confirm name in-game) |
| 429 | (spring flower seed — confirm name in-game) |

## Mixed Seeds (770)

Not a plantable crop entry itself — `Crop.ResolveSeedId` transforms item 770 into a random
specific crop seed before planting (uses `getRandomLowGradeCropForThisSeason`). Does **not**
appear in `Data/Crops` as its own entry under key `"770"`.
