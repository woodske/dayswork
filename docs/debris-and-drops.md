# Debris & drop routing (verified vanilla behavior)

How vanilla spawns the *collectible* loot from things the worker clears, and which of those
paths route the loot to the wrong location. Confirmed against a decompile of the installed
`Stardew Valley.dll` (see [[reference_sdv_decompile_access]] / `docs/game-content-search.md`),
2026-06-22.

## `Game1` debris-creation overloads and where the loot lands

`Game1.createObjectDebris` / `createMultipleObjectDebris` / `createItemDebris` /
`createMultipleItemDebris` / `createRadialDebris` come in many overloads. The rule:

- Overloads that **take a `GameLocation location` argument** add the debris to that location.
- Overloads that **omit it** (including `createObjectDebris(id, x, y, long whichPlayer)` and
  `createMultipleObjectDebris(id, x, y, number, long who)`) fall back to **`Game1.currentLocation`** —
  i.e. wherever the *player's* view currently is, NOT the location the action targets.

`Game1.currentLocation`'s setter is **not** a plain field: changing it fires `OnLocationChanged`
(music/ambient/lighting recompute, SMAPI `Warped`). Do **not** swap it per worker beat to "fix"
routing — it thrashes those systems. Sweep the loot back instead (see below).

## The leak: `ResourceClump.destroy()` ignores its `location` parameter

`StardewValley.TerrainFeatures.ResourceClump.destroy(Tool, GameLocation location, Vector2)` is
handed the clump's own `location`, but spawns nearly all of its **collectible** drops into
`Game1.currentLocation` regardless:

- Large stump (`600`) / large log (`602`): `createMultipleItemDebris((O)709 Hardwood, …, Game1.currentLocation)`.
- Boulder / mine boulders (`148`, `672`, `752`–`758`): single-player path
  `createRadialDebris(Game1.currentLocation, 390, …, item: true)` → Stone.
- Meteorite (`622`): `(O)386` iridium, `(O)390` stone (→ `Game1.currentLocation`), plus
  `createMultipleObjectDebris((O)749, …, 2)` omnigeode (location-less → `Game1.currentLocation`).

Only the **visual** chip debris (`createRadialDebris(…, resource:false)`) and the axe-shaving
`location.debris.Add(...)` use the right location; the item loot does not.

### Consequence for the worker

The worker clears autonomously while the player is free to roam. If the player is standing in
another location (e.g. Town) at the instant a stump/boulder breaks on the farm, the hardwood/stone
lands at the same tile coordinates **in the player's current location** and is stranded there
(litter in Town, on roads, etc.). The per-action debris sweep reads the *work* location's
`debris`, finds nothing, and the loot is never buffered.

### What does NOT leak (verified)

- `Tree.performTreeFall` and `Tree.performToolAction` — all drops use the tree's `Location`
  (incl. the offscreen-fall path driven by `AdvanceOffscreenTreeFall`). Felled-tree wood is safe.
- `FruitTree` fruit shake and chop-down — all use `Location` (the drop routing is safe). **But**
  `FruitTree.shake(tile, doEvenIfStillShaking)` only runs the fruit-drop block when
  `maxShake == 0f || doEvenIfStillShaking`. `maxShake` is a plain transient field (not a
  `NetField`, not serialized) that decays back to `0f` **only** in `FruitTree.tickUpdate`, which is
  called solely from `GameLocation.UpdateWhenCurrentLocation` — i.e. only for the location the
  player is standing in. `updateEvenIfFarmerIsntHere` does NOT tick `terrainFeatures`, and
  `dayUpdate` never resets `maxShake`. So when the worker shakes a fruit tree in a location the
  player isn't in (e.g. the greenhouse), the first shake sets `maxShake > 0` and it never decays;
  it persists on the live tree object across in-game days (sleep/save doesn't reconstruct it), and
  every subsequent `shake(tile, false)` is a no-op that replays the leaf animation but never clears
  the fruit. `InvokeCollectFruit` (`ShiftOrchestrator.TaskActions.cs`) therefore passes
  `doEvenIfStillShaking: true` to force the drop regardless of the frozen `maxShake`. Confirmed
  against a decompile 2026-07-09.
- `Object.performToolAction` (normal stones/forage/ore objects) — uses `Location`. (Plain stones
  are also covered by `TryGetRemovedStandardStoneDrop`'s 1-stone fallback in `InvokeClearRock`.)

## The fix

`ShiftOrchestrator.InvokeTaskActionGuarded` is the canonical worker-action sandbox: it snapshots
player state, redirects inventory gains, trims HUD messages, **and** sweeps mis-routed loot. For the
leak it snapshots `Game1.currentLocation.debris` before the beat when the player is away from the
work location, and `CollectLeakedWorkerDebris` (`ShiftOrchestrator.Debris.cs`) sweeps any debris
that wasn't there before into the worker buffer afterward. The beat is synchronous, so every new
debris in that location is worker-created — no origin filter is needed. Visual-only chips carry no
item id and are skipped (and auto-despawn).

## Dev-mode leak tripwire (gated by `DevLog.Enabled`)

`InvokeTaskActionGuarded` also runs an assertion after recovery: `AuditForeignLeak`
(`ShiftOrchestrator.Debris.cs`) counts how many item-debris vanilla mis-routed into the player's
location this beat, then re-counts what survived recovery. INVARIANT: nothing worker-created should
remain in a non-work location — `stranded` should stay **0**. A nonzero `stranded` (e.g. a drop whose
id our resolver can't normalize, or a future leak path the recovery sweep doesn't cover) is logged
at `Warn` and tallied on `ShiftSession` (`LeakBeatsObserved`/`LeakItemsRecovered`/`LeakItemsStranded`).
`DispatchShiftOverflow` logs a shift-end summary, and the `dayswork_debug_leaks` console command
prints the live tally on demand. The tripwire is scoped to `Game1.currentLocation` (the only sink
vanilla routes loot to) so the player's own roaming activity never produces false positives.
