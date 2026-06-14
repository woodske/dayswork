# Sound cues

## Source / verification method

Cue names are stored as ASCII strings in the XACT sound bank:

```
X:\Steam\steamapps\common\Stardew Valley\Content\XACT\Sound Bank.xsb
```

Parse it as ASCII and regex-match `[a-zA-Z][a-zA-Z0-9_]{2,30}` to enumerate all cue names.
This is authoritative — never guess a cue name without checking here first.

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

**Placement rule for harvest:** the `playSound` call sits *after* the `dirt.crop is null` guard
in `InvokeHarvest` so it only fires when there is actually something to harvest. Doing it from
the animator (before the guard) would play a phantom sound on empty-tile no-ops.

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
