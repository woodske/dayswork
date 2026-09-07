# Verified game flags and unlock checks

Confirmed via ilspycmd decompile of `Stardew Valley.dll`.

## Greenhouse unlock

`Game1.getFarm().greenhouseUnlocked` is a `NetBool` set to `true` when the Community Center
pantry bundle is complete (`ccPantry` mail). The greenhouse building always exists on the farm
(added via `Farm.AddDefaultBuilding("Greenhouse", ...)`) so `Game1.getLocationFromName("Greenhouse")`
returns non-null even when locked — **do not use it as an unlock check**.

Use instead:
```csharp
Game1.getFarm().greenhouseUnlocked.Value
```

The game itself checks:
```csharp
if (!greenhouseUnlocked.Value && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccPantry"))
    greenhouseUnlocked.Value = true;
```

## SVE Grandpa's Shed Greenhouse unlock

`Custom_GrandpasShedGreenhouse` has `CreateOnLoad.AlwaysActive: false` in SVE's `Data/Locations`,
so `Game1.getLocationFromName("Custom_GrandpasShedGreenhouse")` returns null until the player has
**visited** the location — not when they've merely unlocked it. Do not use it as an unlock check.

SVE defines `GrandpaShedComplete` as `HasSeenEvent: currentPlayer, hostPlayer |contains=2554906`
(confirmed in `[CP] Stardew Valley Expanded/content.json`). Event 2554906 fires when the shed
refurbishment cutscene plays (Robin completing the renovation).

Use instead (single-player; host == currentPlayer):
```csharp
Game1.player.eventsSeen.Contains("2554906")
```

`Farmer.eventsSeen` is `NetStringHashSet` in SDV 1.6 — IDs are strings.
