# Fences and gates (`StardewValley.Fence`)

Verified against a decompile of the installed game DLL (`X:\Steam\steamapps\common\Stardew
Valley\Stardew Valley.dll`, `ilspycmd -t StardewValley.Fence`). Backs the worker's
"open gates while pathing" logic in `Dayswork/Worker/WorkerMovementDriver.cs`.

## What a gate is

A fence gate is a `StardewValley.Fence` (subclass of `Object`) stored in `location.objects`,
keyed by its tile `Vector2`. It is a gate when `isGate.Value == true`. Unqualified item ids:

| Item | Id |
|------|----|
| Wood fence | `322` |
| Stone fence | `323` |
| Iron fence | `324` |
| Hardwood fence | `298` |
| **Gate** | `325` |

## Open / closed state

- `gatePosition` (`NetInt`): `0` = closed, `88` = fully open (`gateClosedPosition` /
  `gateOpenedPosition` constants).
- `isPassable()` returns `isGate.Value && gatePosition.Value >= 88`. A regular (non-gate) fence is
  never passable; a closed gate is not passable. Because the worker's collision probe
  (`location.isCollidingPosition`) honors `isPassable()`, a **closed** gate blocks pathing while an
  **open** one does not.
- `health.Value > 1f` is required to operate the gate — a broken gate (`<= 1f`) ignores
  `toggleGate` and ignores interaction. Always gate on `health.Value > 1f` before opening.

## Opening a gate

```csharp
public virtual void toggleGate(bool open, bool is_toggling_counterpart = false, Farmer who = null)
public void toggleGate(Farmer who, bool open, bool is_toggling_counterpart = false) // arg-order overload
```

- Call `fence.toggleGate(open: true)` to open. `who` may be `null` — it is only used to add a
  `TemporaryPassableTiles` rect to a `Farmer`, which the worker (an `NPC`, not a `Farmer`) does not
  need; setting `gatePosition = 88` makes `isPassable()` true on its own.
- It does **not** touch `Game1.player`, so no `InvokeTaskActionGuarded` wrapping is required.
- It syncs the counterpart of a double-wide gate and plays the `doorClose` sound cue.

## Auto-close behavior (why opened gates stay open for the worker)

`updateWhenCurrentLocation` runs each tick while the location is current. When a gate reaches
`gatePosition == 88` it auto-closes **only if** its `getDrawSum()` is not one of the
gate-in-a-fence-line sums:

```
valid (stays open): 10, 100, 110, 500, 1000, 1500
```

`getDrawSum()` adds `+10` (west neighbor), `+100` (east), `+500` (south), `+1000` (north) for each
adjacent counting fence. Any sane placement — a gate within or at the end of a fence run — yields
one of the valid sums, so an opened gate **stays open**. This is why the worker can open a gate
once at the start of a travel leg and not be re-blocked mid-walk.

The proximity auto-open you see for the player is driven by the player's adjacency in vanilla
movement code; it never fires for an `NPC` worker, which is why the worker must open gates itself.

## Removing a gate

`performToolAction` removes a gate when hit with an `Axe` or `Pickaxe` (drops `(O)325`). The worker
does not do this; documented only for completeness.
