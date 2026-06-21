# FarmCave — Verified Game Content

Confirmed against `StardewValley.dll` (`FarmCave` class) using ilspycmd (2026-06-21).

## Cave unlock and type

- `Game1.MasterPlayer.caveChoice.Value`
  - `0` — not yet chosen (Demetrius event not triggered)
  - `1` — bat cave
  - `2` — mushroom cave
- `Game1.getFarm().farmCaveReady.Value` — true when the cave has at least one ready item
  (set by `FarmCave.UpdateReadyFlag()` each day and on 10-minute ticks)

## Bat cave (caveChoice == 1)

Bats drop fruit items in `FarmCave.DayUpdate()`:

```csharp
Object obj = ItemRegistry.Create<Object>("(O)" + text);
obj.IsSpawnedObject = true;
if (CanItemBePlacedHere(vector))
    setObject(vector, obj);
```

Possible item IDs spawned:
- `296` Salmonberry
- `396` Spice Berry
- `406` Wild Plum
- `410` Blackberry
- `613` Apple (10% chance branch)
- `634`–`638` (seasonal fruits: Blueberry, Fiddlehead Fern, Grape, Cranberry, Holly — by index)

**Detection**: `obj.IsSpawnedObject.Value == true` within `FarmCave.objects`.

**Collection**: `loc.removeObject(tileVec, false)` then buffer the item directly.

## Mushroom cave (caveChoice == 2)

Mushroom boxes are BigCraftable `(BC)128`, placed by `setUpMushroomHouse()` at tiles:
- x ∈ {4, 6, 8}, y ∈ {5, 7}

Readiness mirrors `FarmCave.UpdateReadyFlag()`:
```csharp
obj.bigCraftable.Value
    && obj.heldObject.Value != null
    && obj.MinutesUntilReady <= 0
    && obj.QualifiedItemId == "(BC)128"
```

`(BC)128` falls through `Object.checkForAction` to `CheckForActionOnMachine`.
When collected by the worker, clear machine state:
```csharp
var mushroom = obj.heldObject.Value;
obj.heldObject.Value = null;
obj.readyForHarvest.Value = false;
obj.showNextIndex.Value = false;
obj.ResetParentSheetIndex();
```
Do **not** call `AttemptAutoLoad` — mushroom boxes are self-reloading machines with no
input item; they reload automatically via the overnight machine-tick cycle.

## Detection predicate (`IsCaveHarvestReady`)

```csharp
private static bool IsCaveHarvestReady(SObject obj) =>
    obj.IsSpawnedObject.Value                                     // bat cave fruit
    || (obj.bigCraftable.Value
        && obj.readyForHarvest.Value
        && obj.heldObject.Value is not null
        && string.Equals(obj.QualifiedItemId, "(BC)128",
                         StringComparison.Ordinal));              // mushroom box
```

Always gate the call with `loc.Name == "FarmCave"` to prevent false positives from spawned
objects elsewhere. `(BC)128` guard is belt-and-suspenders; `readyForHarvest` is the live flag.

## Warp tiles (confirmed in docs/farm-warps/vanilla-farms.md)

- Entry: farm.warps TargetName=="FarmCave" → warp.TargetX/Y == (8, 11) for all vanilla farms
- Exit: farmCave.warps TargetName=="Farm" → arrival on Farm at (34,6) for 80-wide farms
- At runtime always resolve via `location.warps` to support all farm types
