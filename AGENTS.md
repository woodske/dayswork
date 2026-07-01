# Dayswork — AI context

Single-player Stardew Valley SMAPI mod. The player builds a farm office
(`Bindicle.Dayswork_Office`) and hires an NPC farmhand from it. The worker spawns at the farmhand office
entrance each morning, **walks** the farm doing the contract's configured work (water/harvest
crops, collect fruit, animal care, clear rocks/weeds/grass/trees, plus a full managed-crop
lifecycle), deposits output into the player's chests, and return to the office. Payment is **upfront** for a block
of worker energy. Constraints baked into the design: progression-aware (worker inherits the
player's tool levels), safe (items are never lost — undelivered output goes to the office output
chest / shipping bin), single-player only, and the worker must physically walk (no warping except
stuck-recovery and building doors).

See **`docs/architecture.md`** for the full subsystem map and the shift loop. Start any code task
from **`Dayswork/ModEntry.cs`** — it's the hand-wired composition root (no DI container); every
service and the SMAPI event it hangs off is visible there.

## Local game/source paths

- Stardew Valley install: `X:\Steam\steamapps\common\Stardew Valley`
- Stardew Valley Expanded source: `C:\Users\kwood\Repos\StardewValleyExpanded`

Use `docs/game-content-search.md` for the fastest way to search/parse the base game and SVE
trees. Per hard rule 7, verify ids, tiles, qualified ids, event keys, data fields, and runtime API
behavior against these local files, runtime data, or a decompile; then record any newly confirmed
facts under `docs/`. Update `docs/game-content-search.md` if any new search techniques are used.

## Build / deploy

- `dotnet build Dayswork/Dayswork.csproj` (or build the solution). Target is **net6.0**.
- `Pathoschild.Stardew.ModBuildConfig` auto-resolves the Stardew/SMAPI references and, with
  `<EnableModDeploy>true</EnableModDeploy>`, **auto-copies the built mod into the Stardew `Mods/`
  folder** on every build — no manual deploy step. `manifest.json`, `i18n/`, and `assets/` are
  copied to output.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — warnings fail the build. Nullable is on,
  LangVersion 10, ImplicitUsings on.
- Release: set `<EnableModZip>true</EnableModZip>` (off by default); Release also strips PDBs.
- Running it: launch the game through SMAPI; the mod loads on a single-player save.

## Hard architectural rules (enforced in code)

1. **Core purity.** `Dayswork.Core/` must reference **zero** SMAPI/Stardew types — it has no
   `ModBuildConfig` reference, only `Dayswork/` does. Put game-touching code in `Dayswork/`;
   put pure logic (pricing, energy, state machine, planning, DTOs) in Core where it's unit-tested.
2. **No Harmony.** The mod uses SMAPI events only — there are no Harmony patches and no
   `<EnableHarmony>`. Don't add one without a hard reason; prefer an event hook.
3. **Single active contract.** At most one Active/Paused contract at a time (enforced in the hiring
   flow and assumed by the scheduler).
4. **Items are never lost — and never degraded.** Two invariants on every collected item/material:
   - *Never lost.* Every deposit/overflow path falls back so output ends up somewhere safe
     (chest → office output chest → shipping bin). Preserve this when touching deposit or shift-stop code.
   - *Full fidelity preserved through every hop.* An item the worker collects must reach its
     destination **identical** to what the player would have picked up — same **quantity**, **quality**
     (base/silver/gold/iridium), and **distinct identity/traits**: flavored-preserve id + type (Sturgeon
     Roe, Blueberry Wine, flavored honey…), color, and any other `Object` traits. The collect → buffer →
     planner → deposit/overflow chain must carry these end-to-end; when a trait can't be reconstructed
     from the qualified id alone, capture the real item and clone it back (see the per-shift
     `FlavorItemRegistry` + `BufferedItem.FlavorId`). Quality/flavor/etc. are also part of the
     consolidation key so distinct variants never merge. When you add a new collect path or touch the
     deposit pipeline, verify nothing silently genericizes an item.
5. **The worker is removed before save.** `CalendarHandlers.OnSavingHook` runs before persistence
   and despawns the `FarmhandNpc`; never let it serialize into the save.
6. **Single-player only.** Guard new entry points with `MultiplayerGuard.IsMultiplayer()`.
7. **Verify game content — never guess.** Warp/entrance tiles, item ids, qualified ids, building
   ids, category numbers, event/data keys, animal data, etc. must be confirmed against the actual
   game data or a decompile before use — not recalled from memory. When you investigate and confirm
   a piece of content, **record it in `docs/` so it never has to be looked up again** (see
   "Verified game-content references"). Treat anything not yet confirmed as unknown.

## Code conventions

- **No ceremony.** Don't add an interface, wrapper class, or single-method "policy"/"evaluator"
  class unless at least two implementations or call sites need it. Pure stateless logic is a
  static method. Prefer extending an existing class over creating a new file.
- **Testing policy.** Tests are *required* for persistence formats/migrations, pricing and money
  math, and item-routing invariants (items are never lost) — plus a regression test when fixing a
  bug. Per-feature ritual coverage is not required; the shift engine itself is verified by
  in-game play-testing (see `DevLog.Enabled` + the `dayswork_*` console commands).
- **Core placement.** Before adding a game-state snapshot type to Core, check the decision logic
  is complex enough to justify the reader → planner → executor round trip. A single `if` belongs
  in `Dayswork/` next to the game call.
- **Logging.** Two mechanisms — pick the right one:
  - **Operational logs** (errors, warnings, skip/skip-reason events the player or a bug report
    would care about): use `ModEntry.ModMonitor.Log(message, DevLog.WarnLevel)`. Always-on;
    `DevLog.WarnLevel` is `Debug` during dev and `Warn` in release so the same line doesn't spam
    in dev mode but surfaces clearly in release SMAPI logs.
  - **Dev-only diagnostic logs** (verbose field state, action counts, internal trace info): use
    `DevLog.Log(message, level)`. Gated by `DevLog.Enabled` — silent in release builds. Never
    use `DevLog.Log` for something the player or a support log would need to see.

## Bug Fixing

- When fixing bugs that relate to game behavior, always verify the root cause against the decompiled Stardew Valley game code before proposing a fix; 
  do not rely on assumptions or the explore agent's mental model.

## Where things live

- `Dayswork/Orchestration/` — the shift engine. `ShiftOrchestrator.*` partials drive the tick
  loop and the state-machine transitions; **all mutable per-shift state lives on `ShiftSession`**
  (created at shift start, discarded at shift end — a fresh session is the reset), which also
  holds the per-shift `ManagedShoppingCoordinator` (store trips) and `DepositTripRunner`
  (chest/bin deposit trips). Day-start scheduler, work scanning, and animal handling live
  alongside. All cross-location movement (building doors, expansion hops, store trips) runs
  through one primitive: `Travel.cs` (`TravelPlan` + `TravelRunner`), with the completion
  dispatch in `ShiftOrchestrator.Travel.cs`.
- `Dayswork/Integration/` — building definition + interaction, persistence, config/GMCM, chest and
  shop resolution.
- `Dayswork/UI/` — the hub-and-spoke hiring menus + a small layout toolkit (`UI/Layout/`).
- `Dayswork/Worker/` — the NPC, movement driver, tool animation.
- `Dayswork/Compat/` — SVE / farm-expansion support (vanilla path is a no-op).
- `Dayswork.Core/` — domain, capabilities, pricing, energy, shift state machine + planner,
  inventory/deposit, persistence DTOs.
- `docs/farm-warps/` — per-farm entrance/warp tile reference (vanilla + SVE).

## Verified game-content references

Confirmed game content lives under `docs/` so it's looked up once, not re-derived each session
(per hard rule 7). Add to these (or start a new file) whenever you confirm content against game
data or a decompile, and note where you confirmed it.

- `docs/farm-warps/` — farm entrance/warp + interior-door tiles, vanilla and SVE.
- `docs/game-content-search.md` — local Stardew/SVE paths, folder maps, and search/parsing tips for
  base `.xnb`/DLL content, SVE Content Patcher packs, C# source, and `.tmx` maps.
- `docs/zoom-and-viewport.md` — how the frozen farm view zooms/pans (`Game1.options.desiredBaseZoomLevel`, viewport recompute, `getMouseX(false)` zoom-awareness, MonoGame `TouchPanel` for pinch).
- `docs/machines.md` — `Data/Machines` schema + machine runtime API (`GetMachineData`, `PlaceInMachine`/`AttemptAutoLoad`, `MachineDataUtility`, `readyForHarvest`/`heldObject`) backing the Manage Machines feature (built 2026-06-19; reader = `Dayswork/Orchestration/MachineReader.cs`); plan + status in `docs/plans/machine-management.md`. Its "Fish ponds" section verifies the `StardewValley.Buildings.FishPond` API (`output` NetRef, `tileX/tileY` identity, direct-null collect, 5×5 footprint) backing Manage Fish Ponds (built 2026-06-23; reader = `Dayswork/Orchestration/FishPondReader.cs`); plan + status in `docs/plans/fish-ponds.md`.
- `docs/farmhand-art.md` — farmhand sprite/portrait dimensions, frame layout, verified NPC/farmer animation constants, and the decision to keep body animation separate from tool/effect sprites.
- `docs/fences-and-gates.md` — `StardewValley.Fence` gate API (`isGate`, `gatePosition` 0/88, `health > 1f`, `isPassable`, `toggleGate`, `updateWhenCurrentLocation` auto-close rule) backing the worker's open-gates-while-pathing logic in `Dayswork/Worker/WorkerMovementDriver.cs`.
- `docs/debris-and-drops.md` — which `Game1.create*Debris` overloads route loot to `Game1.currentLocation` vs the passed `location`; the `ResourceClump.destroy()` leak (hardwood/stone spawn at the *player's* location, not the clump's) and the `InvokeTaskActionGuarded` sweep that recovers it.
- `docs/sound-cues.md` — the **worker-sound invariant** (every worker action emits the player's sound, gated on `Game1.player.currentLocation == location`; silent off-location), the verified cue-per-action table, how to enumerate cue names from the XACT sound bank, and the `IsLocalPlayer` gotcha (vanilla machine-collect `"coin"` won't fire for the fake worker farmer).
- `docs/chests.md` — `Chest` capacity/special-type API (`SpecialChestTypes.BigChest` → `GetActualCapacity()` 70, serialized so it persists) and why `BuildingData.Chests` can't express capacity — backing the office porch chests being upgraded to Big Chests in `Dayswork/Integration/CabinChestService.cs`; also the wood/stone/big chest id table (`232` is the Stone Chest, not the Big Chest).

Hard-coded ids that are already verified in code (keep them centralized when you touch them):
the office building/chest ids in `Dayswork/Integration/HiringBuilding.cs`, the animal-product
forage ids/categories in `Dayswork/Orchestration/WorkAreaScanner.cs`, and tool/build-material item
ids in `HiringBuilding.BuildData`.

## SDV-1.6 landmines (these cause bugs if forgotten)

- `PathFindController` lives in **`StardewValley.Pathfinding`**; check `pathToEndPoint` for null.
- Use `QualifiedItemId` + `ItemRegistry.Create`; bad ids yield a fallback **"Error Item", not
  null** — always guard before shipping/depositing.
- Vanilla harvest/tool/grass/tree APIs mutate `Game1.player` and enqueue HUD messages even when the
  **worker** acts — wrap worker actions to snapshot/restore player state, redirect gained items to
  the worker buffer, and trim HUD messages (see `InvokeTaskActionGuarded`).
- `crop.harvest()` doesn't clean up `dirt.crop`; `ResourceClump.performToolAction` calls
  `destroy()` internally and returns a bool (never destroy manually); felled trees pass through a
  stump phase and spawn debris *after* the fall animation (delayed debris sweep).
- Check `chest.GetMutex().IsLocked()` before writing to a chest.
- `BuildingDrawLayer` can't be conditioned in this version → conditional building overlays are
  C#-rendered in `Display.RenderedWorld`.
- `FarmerSprite.StopAnimation` is a no-op while `pauseForSingleAnimation` is true — clear the flag
  first.
- **Worker sounds don't auto-fire.** Every worker action must emit the player's sound, gated on
  `if (Game1.player.currentLocation == location) location.playSound(cue, tileVec)` — audible
  on-location, silent off-location. Many vanilla APIs that "play the sound for you" gate it behind
  `who.IsLocalPlayer`, which is false for the worker's `CreateFakeEventFarmer()` (e.g.
  `Object.CheckForActionOnMachine`'s collect `"coin"`), so the sound silently doesn't play — emit it
  yourself. When adding any collect/work path, wire its cue per `docs/sound-cues.md`.

## Current state

Builds clean and runs. Working today: build the office and hire from its bulletin board; the
hiring flow (tasks, zone-draw work scope, output chests, energy tier, task priority, one-time vs
recurring schedule, managed crops, **Manage Machines**, **Manage Fish Ponds**); the full shift loop
(animal care, crops, fieldwork, managed-crop planting with auto-buy, **machine collect/reload**,
**fish-pond collect**, multi-trip deposits, overflow safety, stuck recovery, 8pm cap, sleep settle);
save/load persistence; evening office lighting/smoke; optional GMCM config; and SVE expansion
compatibility. **Manage Machines** (2026-06-19) is built, unit-tested, and **passed its in-game smoke
pass (milestone 8) on 2026-06-28 — release-ready**: worker collect/reload, fish-smoker (fish+coal) and
dehydrator (×5) loads, flavored-roe round-trip, filtered loads, and the **per-group fetch-first
single-visit** workflow (worker fetches a group's inputs in one chest trip, then visits each machine
once to collect→reload) all verified in-world. See `docs/plans/machine-management.md` for status + v1
limitations (notably: a group's input chest must be in the same location as its machines, else
collect-only). **Manage Fish Ponds** (2026-06-23, collect-only) is built and unit-tested but **awaits
its in-game smoke pass** — see `docs/plans/fish-ponds.md`. Collected output keeps its **flavored/colored identity** (Sturgeon Roe,
blueberry wine, flavored honey…) end-to-end via the per-shift `FlavorItemRegistry` +
`BufferedItem.FlavorId` (capture-and-clone; benefits machine output too). Dev tooling (verbose logs
+ console commands like `dayswork_end_shift`, `dayswork_debug_machines`, `dayswork_debug_leaks`) is gated behind
`DevLog.Enabled`, off for release.
