# Delayed tree debris and concurrent workers

Confirmed 2026-09-08 against the installed `X:\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll`
(1.6.15), decompiled with `ilspycmd -t StardewValley.TerrainFeatures.Tree` and
`ilspycmd -t StardewValley.GameLocation`.

## When a felled tree emits its trunk loot

`Tree.performTreeFall` does not emit trunk loot when it starts a standing tree's fall. It sets
`stump = true`, `health = 5`, and `falling = true`; the normal fall animation continues later.
In the focused decompile these are `Tree.cs:1495–1497`.

`Tree.tickUpdate` advances the fall rotation. Once its absolute value exceeds pi/2, it clears
`falling`, plays the thud, and emits the trunk drops into the tree's `Location.debris`.
Wood, sap, hardwood, and seeds originate four tiles to the left or right of the trunk anchor,
according to `shakeLeft`. See focused `Tree.cs:500–580`, particularly wood at line 533 and sap
at line 538. The drop block reads `lastPlayerToHit` for the vanilla farmer identity, but this
identity cannot distinguish Dayswork workers: their action farmers deliberately share the host id.

`GameLocation.UpdateWhenCurrentLocation` iterates `activeTerrainFeatures` and invokes
`terrainFeature.tickUpdate(time)` (`GameLocation.cs:4155–4161`). Therefore drops from more than
one falling tree may already coexist in the location when a mod's next update callback runs.

## Consequence for independent delayed sweeps

A pre-action debris snapshot proves ownership only for debris emitted synchronously inside that
action. Keeping that same snapshot for later ticks and taking every new debris object near the
tree does not prove which tree or worker produced the object. Two nearby trees can emit within
both sweep radii, and the first sweep removes both sets before the second can see them.
Sequentially invoking the workers' update handlers does not serialize the intervening vanilla
tree-animation emissions.

The review follow-up must retain a per-worker association for delayed drops before routing them
into a worker's buffer. Neither the old snapshot plus radius nor the vanilla farmer id alone
provides that association. See also [debris-and-drops.md](debris-and-drops.md) for location routing
and the existing offscreen fall advancement.

## A single tree update is a synchronous emission boundary

Rechecked 2026-09-08 while assessing the Harmony alternative. `Tree.tickUpdate(GameTime)` reads
the specific tree instance's `Location` and emits its trunk items into that location during the
call. `GameLocation.UpdateWhenCurrentLocation` calls each active terrain feature separately and
only removes the feature after `tickUpdate` returns true. In the focused decompile, the fall can
emit items and then return true when `health == -100f` (`Tree.cs:500–591`).

Consequently, a before/after hook on that exact tree update can observe the newly emitted vanilla
debris before the caller removes the tree. The association must be to the tree instance, not just
its tile, because a removed tree can be replaced at the same tile. This boundary describes vanilla's
synchronous emissions; items created later by another mod need separate verification. Hooking the
method does not cause offscreen trees to tick — the existing offscreen advancement remains needed.

The implemented hook runs its prefix at Harmony's last priority and its postfix/finalizer at first
priority. This puts earlier-prefix output into the baseline and observes vanilla output before later
postfix additions as far as Harmony ordering can guarantee. A mod which suppresses the original or
emits loot asynchronously is outside the verified synchronous boundary and remains a required
compatibility smoke case. Stump removal and `FruitTree.shake` were rechecked in the same 1.6.15
decompile: both create their collectible debris synchronously, so the guarded action snapshot is
enough and a historical radius sweep would be actively unsafe.

The approved integration design and its cleanup/compatibility criteria are recorded in
[review fix R6](../plans/dayswork-2.0-review-fixes.md#r6--prevent-cross-worker-capture-of-delayed-tree-drops).
