# Sound cues

## Source / verification method

Cue names are stored as ASCII strings in the XACT sound bank:

```
X:\Steam\steamapps\common\Stardew Valley\Content\XACT\Sound Bank.xsb
```

Parse it as ASCII and regex-match `[a-zA-Z][a-zA-Z0-9_]{2,30}` to enumerate all cue names.
This is authoritative — never guess a cue name without checking here first.

## The worker-sound invariant

**Every action the worker takes must emit the same sound the player would hear — gated on the
player being in the action's location.** Concretely, every worker sound is wrapped:

```csharp
if (Game1.player.currentLocation == location) location.playSound(cue, tileVec);
```

So the worker is audible when the player is in the same location and **silent when the player is
elsewhere** (no phantom audio leaking across locations while the worker does off-farm work). When a
vanilla API would play the sound for you, prefer passing a `playSounds`/presence flag through it;
otherwise emit the cue explicitly. This applies to *every* new collect/work path you add.

## Tool-action sounds (worker-relevant)

These must be emitted **explicitly** by mod code. The worker calls game APIs directly rather
than going through `Tool.DoFunction()` / normal tool infrastructure, so the vanilla sound hooks
don't fire reliably (same reason the hoe/pickaxe sounds are explicit in `ToolSwapAnimator`).

| Action               | Cue name        | Where emitted in mod                                      |
|----------------------|-----------------|-----------------------------------------------------------|
| Watering can use     | `"wateringCan"` | `ToolSwapAnimator.SpawnToolSwing` — WateringCan case      |
| Crop harvest         | `"harvest"`     | `ShiftOrchestrator.TaskActions.InvokeHarvest` (after guard) |
| Hoe (tilling)        | `"hoeHit"`      | `ToolSwapAnimator.SpawnToolSwing` — Hoe case              |
| Pickaxe (rock clear) | `"hammer"`      | `ToolSwapAnimator.SpawnToolSwing` — Pickaxe case          |
| Shears               | `"scissors"`    | `AnimalCollectAudioCue.ForTool` → `HandleCollectFromAnimal` |
| Milk pail            | `"Milking"`     | `AnimalCollectAudioCue.ForTool` → `HandleCollectFromAnimal` |
| Fish pond collect    | `"coin"`        | `ShiftOrchestrator.FishPonds.CollectFishPond` (after output captured/nulled) |
| Machine collect      | `"coin"`        | `ShiftOrchestrator.Machines.CollectMachine` (after buffer credited) |
| Machine load/reload  | machine `LoadEffects` | `ShiftOrchestrator.Machines.LoadMachine` — `PlaceInMachine(..., playSounds: <player here>)` |
| Chest open (deposit) | `"openChest"`   | `DepositTripRunner.BeginTripExecution` (lid + sound on arrival)            |
| Chest deposit (per stack) | `"Ship"`   | `DepositTripRunner.DepositCurrentStack`                                    |
| Chest open (input fetch/return) | `"openChest"` | `WithdrawInputs` / `SettleCarriedInputs` (lid + sound on chest write)  |
| Item taken from chest | `"dwop"`       | `ShiftOrchestrator.Machines.WithdrawInputs` — one per item type, staggered |
| Item returned to chest | `"dwop"`      | `ShiftOrchestrator.Machines.SettleCarriedInputs` — one per item type, staggered |

**Placement rule for harvest:** the `playSound` call sits *after* the `dirt.crop is null` guard
in `InvokeHarvest` so it only fires when there is actually something to harvest. Doing it from
the animator (before the guard) would play a phantom sound on empty-tile no-ops. Fish-pond and
machine collect follow the same rule — the cue sits after the "has output" guard so empty
ponds/machines make no phantom sound.

**Machine collect gotcha (`IsLocalPlayer`):** vanilla `Object.CheckForActionOnMachine` plays its
`"coin"` *inside* `if (who.IsLocalPlayer)`. The worker acts as a fake `CreateFakeEventFarmer()`
farmer, which is never the local player, so `machine.checkForAction(who)` is **silent** — the mod
must emit the `"coin"` itself (it does, in `CollectMachine`). Machine *load*, by contrast, routes
through `MachineDataUtility.PlayEffects`, which is **not** `IsLocalPlayer`-gated, so passing
`playSounds: Game1.player.currentLocation == location` to `PlaceInMachine` is enough.

**Chest-take gotcha (no API sound):** `Chest.grabItemFromChest` plays **nothing** — the open/pickup
sounds live in the `ItemGrabMenu`/`InventoryMenu` UI layer (`InventoryMenu.moveItemSound = "dwop"`),
which the worker bypasses by mutating `chest.Items` directly. So both the deposit path and the
machine input-fetch path emit chest audio by hand: `"openChest"` on the chest write (with the vanilla
`frameCounter = 5` lid trigger), then per item — `"Ship"` for deposits, `"dwop"` for takes/returns.
The machine input-chest interactions are synchronous (all items moved in one call): `WithdrawInputs`
(take) and `SettleCarriedInputs` (return the rare leftover inputs) both stagger their `"dwop"`s via
`DelayedAction.playSoundAfterDelay` (≈90 ms apart) so each is individually audible rather than
collapsing into a single overlapping cue. The return only sounds when something actually lands in the
chest (a full/busy chest falls through to overflow silently).

## Other confirmed cues (for reference)

Found in the same sound bank scan:

| Cue name          | Context                        |
|-------------------|--------------------------------|
| `"leafrustle"`    | Scythe through grass/weeds     |
| `"waterfall"`     | Water ambient                  |
| `"waterfall_big"` | Larger water ambient           |
| `"waterSlosh"`    | Water splash                   |
| `"pickUpItem"`    | Item pickup                    |
| `"Pickup_Coin15"` | Coin pickup                    |
| `"trashcan"`      | Trash can open                 |
| `"trashcanlid"`   | Trash can lid                  |
