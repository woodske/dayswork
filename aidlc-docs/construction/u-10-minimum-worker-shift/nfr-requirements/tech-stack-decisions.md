# U-10 — Tech Stack Decisions

All decisions inherit the project-wide stack (C# / .NET 6, SMAPI 4.x, xUnit, FsCheck) established in Requirements Analysis. This document records U-10-specific choices only.

---

## SMAPI Events

| Event | Used by | Purpose |
|---|---|---|
| `GameLoop.DayStarted` | `RecurringContractScheduler` | Trigger spawn at 6am; fire multiplayer guard; build work list |
| `GameLoop.UpdateTicked` | `ShiftOrchestrator` | Drive movement + action polling; throttled to every 4 ticks |
| `GameLoop.TimeChanged` | `ShiftOrchestrator` | Detect 8pm hard cap → transition to Depositing |
| `GameLoop.SaveLoaded` | `ModEntry` (existing) | Already wired from U-09 (ContractPersistenceAdapter) |
| `GameLoop.Saving` | `ModEntry` (existing) | Already wired from U-09 |

No new event subscriptions beyond `UpdateTicked` and `TimeChanged` are introduced in U-10.

---

## Pathfinding

**Decision**: Use the game's built-in `PathFindController` directly, wrapped by `PathFindControllerAdapter`.

`PathFindController` is the standard Stardew NPC pathfinding mechanism. It accepts a destination tile and a `GameLocation`, and drives the NPC's movement each game update internally. The adapter exposes:
- `StartNavigation(TileCoord, GameLocation)` — creates a new `PathFindController` and assigns it to the NPC
- `HasArrived : bool` — true when the controller's path is complete or null
- `IsNavigating : bool` — inverse of `HasArrived`

No third-party pathfinding library. No custom A* implementation.

---

## NPC Subclass

**Decision**: `FarmhandNpc` extends Stardew's `NPC` class directly.

- Sprite: recolored vanilla NPC (placeholder per FR-NPC-01 / Q9). Uses an existing NPC sprite from the game's content — no custom asset loading required in U-10.
- Constructor: calls base `NPC` constructor with the placeholder sprite, a start position (farm entrance), and a display name (i18n key `npc.farmhand.name` — new key added to `i18n/default.json`).
- `update()` override: delegates movement to `PathFindControllerAdapter` rather than vanilla NPC schedule logic.
- `takeDamage()` override: returns 0 and plays surprised emote — deferred to U-13. In U-10, default base behavior applies (NPC is not registered as a combatant, so damage calls won't naturally reach it).

---

## Task Action Invocation

**Decision (N2: B)**: Invoke the game's tool-use API once per tile, then poll every 4 ticks for object removal.

**Invocation**: call `StardewValley.Object.performToolAction(tool, tileX, tileY)` (or the equivalent for the specific object type — crops use `Crop.harvest()`, animals use `FarmAnimal.pet()` / `FarmAnimal.feed()`, etc.). The specific API method varies by task type; all are vanilla Stardew APIs.

**Completion detection**: after invoking, poll `GameLocation.getObjectAtTile(tileX, tileY)` every 4 ticks. When it returns null (or a different object), the action is considered complete and the orchestrator advances to the next WorkItem.

**Special case — buildings / animals**: animals are not tile objects in the same sense. Completion of `pet()` / `feed()` / `produceItem()` is detected by checking the animal's `wasPet` / `fullness` / `currentProduce` flags rather than object removal.

---

## No New NuGet Packages

U-10 introduces no new NuGet dependencies. All required packages are already present:
- `Pathoschild.Stardew.ModBuildConfig` (Dayswork.csproj) — provides SMAPI + Stardew refs
- `FsCheck.Xunit` (Dayswork.Tests.csproj) — established in U-02
- `xunit` + `Microsoft.NET.Test.Sdk` (Dayswork.Tests.csproj) — established in U-02
