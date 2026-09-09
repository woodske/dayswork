# Split-screen lifecycle events and shared mod state

Confirmed 2026-09-07 against the installed `StardewModdingAPI.dll` (SMAPI 4.5.2,
commit `821167e5c511bf3a2d98f604e5e838561c469219`) at
`X:\Steam\steamapps\common\Stardew Valley`, using `ilspycmd`. This check was part of
the Dayswork 2.0 implementation review.

## Events run for each local screen

- `StardewModdingAPI.Framework.SCore.OnPlayerInstanceUpdating(SGame, GameTime, Action)`
  runs once per local player's game instance per update. It uses the same `SCore.EventManager`
  to raise mod events in the context of the current screen.
- `Context.IsWorldReadyForScreen` and `Context.LoadStageForScreen` are `PerScreen<T>`
  fields. Each local guest therefore has its own transition to the ready state.
- When a screen becomes ready, `OnPlayerInstanceUpdating` calls
  `OnLoadStageChanged(LoadStage.Ready)`, then raises `SaveLoaded`, then `DayStarted`.
  A local guest joining an already-running farm triggers these events for their screen;
  they are not exclusively notifications that the host loaded a save or advanced a day.
- The same method raises `Saved` and `DayStarted` after the current instance's
  `IsBetweenSaveEvents` flag was set. `SGame.AfterLoadTimer` and `IsBetweenSaveEvents`
  are instance state.
- `SCore.OnLoadStageChanged(LoadStage.None)` raises `ReturnedToTitle` for the current
  screen. The stage it compares and updates is also the per-screen `Context.LoadStage`.
- `SCore` loads mod instances and calls each mod's `Entry` in its mod-loading loop;
  it does not create a fresh mod instance when a second local screen joins.

## Authority and reset consequences

`Context.IsMainPlayer` requires both `Game1.IsMasterGame` and `Context.ScreenId == 0`
(and excludes a pending farmhand selection menu). `Context.ScreenId` is the current
`Game1.game1.instanceId`, falling back to zero before a game instance exists.

An object created once in `ModEntry.Entry` is shared by the local screens unless the
mod deliberately stores its state in `PerScreen<T>`. In particular, a guest's
`SaveLoaded` handler must not reset a shared host fleet, clear a shared owner-upgrade
store, or replace the host's day-level claims and budget ledgers. Those actions belong
to the host's lifecycle. Screen-specific UI/request resets must reset only that screen.

At `ReturnedToTitle`, a host-screen authority guard still works. `Game1.CleanupReturningToTitle`
calls `ResetGameStateOnTitleScreen` before SMAPI observes the ready → none transition; that reset
sets `multiplayerMode` to zero, for which `Game1.IsMasterGame` returns true. The host remains screen
0, so `Context.IsMainPlayer` is true when its `ReturnedToTitle` event is raised. A departing local
guest keeps its nonzero screen id and therefore remains false. This makes `Authority.IsHost` a safe
way to preserve guest state while retaining actual host-exit cleanup.

## Reproduce the source check

```powershell
ilspycmd -t StardewModdingAPI.Framework.SCore "X:\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.dll"
ilspycmd -t StardewModdingAPI.Framework.SGame "X:\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.dll"
ilspycmd -t StardewModdingAPI.Context "X:\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.dll"
```

Search the resulting types for `OnPlayerInstanceUpdating`, `SaveLoaded.RaiseEmpty`,
`DayStarted.RaiseEmpty`, `ReturnedToTitle.RaiseEmpty`, `LoadStageForScreen`, and
`IsMainPlayer`.
