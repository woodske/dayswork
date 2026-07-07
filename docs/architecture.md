# Dayswork — Architecture Reference

## Overview

Dayswork is a single-player SMAPI mod that lets the player construct a farm building
(`Bindicle.Dayswork_Office`) and hire an NPC farmhand from it. The farmhand spawns at the farm
entrance each morning, **physically walks** the farm performing the contract's configured tasks
(water/harvest crops, collect fruit, animal care, clear rocks/weeds/grass/trees, plus full
"managed crop" lifecycle), deposits output into the player's designated chests, and exits. The
player pays **upfront** for a block of worker energy (labor capacity). The mod is
**progression-aware** (the worker inherits the player's tool upgrade levels), **safe** (items are
never lost — anything undelivered is mailed back via the building's output chest / shipping bin),
and uses **zero Harmony patches** — everything is driven by SMAPI events.

## Project structure & the Core-purity rule

- **`Dayswork/`** — the SMAPI mod assembly. Touches Stardew/SMAPI APIs. Subdivided into
  `UI/`, `Orchestration/`, `Integration/`, `Worker/`, `Compat/`, `Guards/`, `Diagnostics/`.
- **`Dayswork.Core/`** — pure C# domain. **References zero SMAPI/Stardew types** (enforced by
  having no `ModBuildConfig` package reference). Holds capabilities, pricing, energy, the shift
  state machine, the work-batch planner, inventory/deposit planning, and persistence DTOs.
  Anything testable without the game lives here.
- **`Dayswork.Tests/`** — xUnit + FsCheck tests covering Core *and* the game-free parts of
  `Dayswork` (UI layout toolkit, view-model builders, player-state snapshot guards). Building it
  requires a local Stardew install, same as the mod. Testing policy lives in `AGENTS.md` ("Code
  conventions"): persistence formats, pricing/money, and item-routing invariants must be tested;
  per-feature ritual coverage is not required.
- **`docs/`** — reference docs (this file; `farm-warps/` documents per-farm entrance/warp tiles
  for vanilla + SVE).

`Dayswork/ModEntry.cs` is the composition root: a hand-wired dependency graph (no DI container).
Read it top-to-bottom to see every service and which SMAPI events drive it.

## Subsystem map

### Integration (`Dayswork/Integration/`) — the game-facing seam
- **`HiringBuilding`** — defines the `Bindicle.Dayswork_Office` building by *editing*
  `Data/Buildings` and loading the texture via `AssetRequested`. Two built-in chests: **Input**
  (crop-management supplies) and **Output** (overflow drop). Interaction is C#-driven, not
  Data/Buildings sub-schemas.
- **`HiringBuildingInteraction`** — `Input.ButtonPressed` handler: action-clicking the drawn-in
  bulletin-board tiles opens the hire/manage flow; the porch chest tiles open Input/Output chests.
- **`HiringBuildingOverlayRenderer`** — draws evening lit-windows + chimney smoke once the worker
  is done (`Display.RenderedWorld`), because `BuildingDrawLayer` can't be conditioned in this game
  version.
- **`ContractPersistenceAdapter`** — `Helper.Data.Read/WriteSaveData` under key `Dayswork.Contracts`;
  versioned via `SaveDataSerializer` (v1→v2).
- **`ToolLevelReader`** — snapshots the player's axe/pickaxe levels into a `ToolSnapshot` at shift
  start (the progression-inheritance source).
- **`ModConfigManager` / `GMCMRegistrar`** — config + optional GMCM page (changes apply next shift,
  read live). `ChestResolver`, `CabinChestService`, `ShopStockReader`, `ShopPurchaseService`,
  `CropCatalogProvider`, `CropHudNotifier` support deposits and managed-crop shopping.

### UI (`Dayswork/UI/`) — the hiring flow
- **`HiringFlowCoordinator`** — hub-and-spoke menu controller. The `HubMenu` is home; spokes are
  Task selection, Work scope (zone draw + chests), Manage Crops, Output destinations, Task
  priority, Energy tier, Schedule, and Summary. Builds a `ContractDraft`, re-prices it live, and on
  confirm persists a `Contract`.
- Custom layout toolkit in `UI/Layout/` (VStack/HStack/cards/scroll panels) backs the menus.

### Worker (`Dayswork/Worker/`) — the NPC
- **`FarmhandNpc`** — a custom `NPC` (placeholder Marnie sprite/portrait) added to the farm's
  characters; draws its own stamina bar. The art contract for replacing the placeholder lives in
  `docs/farmhand-art.md`. **Must be removed before save** — its parameterless ctor exists only for
  the XML serializer and is never expected to run.
- **`WorkerMovementDriver`** — pathfinds with `StardewValley.Pathfinding.PathFindController`; on no
  path / impassable path, falls back to an internal BFS; else reports `NavigationFailed`. Worker
  walks pixel-by-pixel along waypoints. Also exposes static BFS route-cost maps used by routing.
- **`ToolSwapAnimator`** — plays the per-beat tool swing; its `IsSwinging` gate paces the whole
  shift loop. Farmhand body animation and tool/effect sprites stay separate, matching Stardew's
  player-character rendering model. `ObjectTargetClassifier` classifies tiles into axe/pick targets
  + resource clumps.

### Orchestration (`Dayswork/Orchestration/`) — the shift loop
- **`ShiftOrchestrator`** (partial across `.cs`, `.WorkSelection`, `.TaskActions`, `.Movement`,
  `.Routing`, `.Travel`, `.Deposit`, `.Debris`, `.ManagedCrops`) — the engine: per-tick intent
  dispatch, work-batch selection, energy spend, stuck recovery, and the state-machine
  transitions. The orchestrator itself holds only long-lived services plus one nullable
  `ShiftSession` reference.
- **`ShiftSession`** — all mutable per-shift state in one object (the Core `ShiftContext`, the
  worker NPC, current location, action/batch/travel/managed-crop state). Created by `StartShift`
  once spawn succeeds and discarded when the shift ends — constructing a fresh session IS the
  per-shift reset.
- **`ManagedShoppingCoordinator`** (held by the session) — the managed-crop shopping trip: builds
  the purchase plan from the input-chest shortfall, walks the worker to Pierre's/Joja (waiting
  out a closed store), buys at the counter one line item per animation beat (paced by
  `WorkerActionAnimationMs`, playing `"purchaseClick"` each beat), and settles purchases into the
  input chest. Travel legs run through the shared travel primitive with `TravelPurpose.ShoppingStep`.
- **`DepositTripRunner`** (held by the session) — executes the planned deposit trips
  chest-by-chest with per-stack beat pacing, re-checking the chest mutex per stack, and routes
  every failure (chest missing/busy/full, unreachable destination) to the shift overflow.
- **`Travel.cs` (`TravelPlan`/`TravelLeg`/`TravelRunner`)** — the single cross-location mover.
  A plan is an ordered list of legs (walk to a tile, then optionally warp through to another
  location); the runner drives it tick-by-tick. Building doors, SVE expansion hops, store entries,
  and deposit-building trips are all just plans; `ShiftOrchestrator.Travel.cs` holds the
  `TravelPurpose` dispatch (what to do when a travel arrives or fails) and the plan builders.
  Each plan carries a failure policy: `ReportFailure` (caller decides: skip batch, mark trip
  undelivered, abort shopping) or `WarpToDestination` (never strand the worker — warp straight to
  the destination and continue).
- **`RecurringContractScheduler`** — `DayStarted` hook: for each contract due today, handles
  festival skips, recurring-terms refresh + affordability, charges the player, and calls
  `StartShift`.
- **`CalendarHandlers`** — reads festival/weather state (fail-safe); its `Saving` hook
  (`StopForSleepAndSettle`) settles the worker **before** contracts persist.
- **`SessionResetHandler`** — clears in-memory worker runtime on `SaveLoaded` / `ReturnedToTitle`.
- **`WorkAreaScanner`** — scans zones/whole locations into `WorkItem`s, applying tool-capability
  skip rules and computing navigation stand-tiles. **`AnimalTaskHandler`** enumerates animals and
  performs feed/pet/collect. **`BuildingWorkNavigator`** resolves building door / interior entry /
  exit-approach tiles (a pure resolver — it never moves the worker).

### Core (`Dayswork.Core/`)
- **Capabilities** — `CapabilityMatrix`: the static tool-level skip table.
- **Pricing/Energy** — `ContractTermsBuilder` (validates scope×task pairs, prices the tier,
  builds the energy profile); `WorkerEnergyLedger` spends energy per beat.
- **Shifts** — `ShiftStateMachine`, `ShiftPlanBuilder` (orders the day's batches),
  `TaskPriorityOrderer`, `WorkerRouteSelector`, `StuckDetector`, `WorkUnitBoundaryClassifier`.
- **Inventory** — `ItemBuffer` (task-tagged collected items), `DepositPlanner`, `OverflowCategorizer`.
- **Domain / Persistence** — `Contract` + DTOs, `ContractStore`, value types (`TileCoord`, `Zone`,
  `GameDate`, `TaskKind`, `EnergyTier`, …).
- **Compat** — `IExpansionProfile` + vanilla/SVE profiles (SVE support; vanilla is a no-op
  null-object).

## Hiring / contract flow

1. Player action-clicks the office's bulletin board → `HiringFlowCoordinator.OpenFromBuilding`.
   If an Active/Paused contract already exists it opens **Manage** instead (single-active-contract
   invariant).
2. The hub-and-spoke menus mutate a `ContractDraft`; every change re-runs
   `ContractTermsBuilder.BuildPreview` → live energy + price + validation. Confirm is gated on a
   valid chargeable scope×task pairing.
3. On confirm: for a **one-time** contract the price is charged immediately and the `Contract` is
   stored Active. Recurring contracts are charged each morning instead.
4. `Contract` carries: enabled tasks, per-task output destinations, schedule, scope selection
   (outdoor zones / animal buildings / greenhouses), energy tier + `TermsSnapshot`, the player's
   category priority ordering, and the managed-crop plan.

## Shift execution loop

`DayStarted` → `RecurringContractScheduler` → `ShiftOrchestrator.StartShift`:

1. Snapshot player tool levels; normalize scope to live locations; classify work scopes.
2. `ShiftPlanBuilder` orders the day into **batches**: per animal building (interior feed/pet/
   collect, then that building's grazing animals), then a farm-wide forage sweep (truffles), then
   managed-crop batches, greenhouses, outdoor crops, outdoor clearing. `WorkAreaScanner` populates
   each batch's tile/animal work.
3. If no applicable work exists, **no worker spawns**. Otherwise spawn `FarmhandNpc` at the farm
   entrance tile (resolved from `farm.warps`, expansion-overridable).

Per `UpdateTicked` (gated on `Game1.shouldTimePass(false)`), tool animation
(`ToolSwapAnimator.Update`), the movement driver (`WorkerMovementDriver.Update` — so the worker
walks smoothly pixel-by-pixel), and the debris-sweep pump run **every tick**; the higher-level
**intent dispatch is throttled to every 4th tick** (`Session.TickCount % 4` in
`ShiftOrchestrator.OnUpdateTicked` — a raw modulo, not SMAPI's `IsMultipleOf`). On the dispatch
ticks the orchestrator advances on the state machine's current intent. Phases:
`WaitingForSpawn → Working → (Stuck → Recovering) → Depositing → Exiting → Done`. Within a batch,
`WorkerRouteSelector` picks the next work item by **category priority first, then
nearest-reachable** (route cost via BFS). Each task is one or more *beats*: a
beat plays the tool animation, invokes the task action, and spends energy; the next beat waits on
`ToolSwapAnimator.IsSwinging` (so animation speed = pacing). Collected items go into the
task-tagged `ItemBuffer`, never the player's inventory.

Stop conditions: energy exhausted, 8pm hard cap (`TimeChanged`), player cancel, sleep (`Saving`),
or all work complete. Any stop routes through **Depositing**: `DepositPlanner` builds trips from
the buffer to assigned chests / the shipping bin (entering buildings by door when needed), then the
worker walks to the farm exit and despawns. Undelivered/overflow items are consolidated and dropped
into the office Output chest (falling back to the shipping bin) with a HUD notice — **nothing is
lost**.

**Eager chest deposits** (config `EagerChestDeposits`, default on): at each work-batch boundary
the worker also runs a *chest-only* deposit for buffered items bound for a player-assigned chest
(`DepositPlanner.Plan(chestDestinationsOnly: true)`), then resumes the next batch — so the player's
chests fill through the day instead of all at once at clock-out (machines reading from those chests
benefit as a side effect). Shipping-bin and office-output items are *retained* in the buffer
(`DepositPlan.Retained`) for the terminal deposit — no mid-day trip to a terminal sink. This reuses
the same deposit-then-resume machinery as the pre-idle flush via `ShiftSession.DepositResume`
(`None`/`ResumeIdle`/`ResumeBatch`); the energy/8pm wrap-up pre-empts a detour through the shared
`DepositResume != None` re-plan guard in `BeginDeposit`.

**Stuck recovery** (no tile progress for N in-game minutes, `StuckDetector`): emote, then teleport
once to the next reachable work tile; if still stuck, teleport home and end the shift via the normal
deposit path. Teleporting is the *only* time the worker skips walking.

## Tasks, priority order, and skip rules

Supported `TaskKind`s and their categories (`TaskKindSets`):

| Category (default priority) | Tasks |
|---|---|
| **AnimalCare** (1st) | FeedAnimals, PetAnimals, CollectAnimalProducts |
| **Crops** (2nd) | WaterCrops, HarvestCrops, CollectFruit |
| **Fieldwork** (3rd) | CutTrees, ClearRocks, ClearWeeds, ClearGrass |

Priority is **per-contract** (the player reorders the three categories). Within a chosen category
the worker does all reachable work nearest-first before the next category. "Managed crops" runs the
authored crop plan (harvest → clear → till → fertilize → seed → water, auto-buying seeds/fertilizer
at the preferred store) *ahead of* ordinary crop work in the same location.

Skip rules confirmed in code:
- **Tool-capability gating** (`CapabilityMatrix`, against the inherited `ToolSnapshot`):
  fruit trees are **never chopped** (unconditional); large logs need **Gold+** axe; large stumps
  need **Steel+** axe; meteorites need **Gold+** pick; large boulders need **Steel+** pick; small
  rocks/trees/stumps/twigs any level. The worker skips what the player's tools couldn't do.
- **WaterCrops** skipped if the tile is already watered, dead, or ready-to-harvest. After harvesting
  a *regrowable* crop a separate WaterCrops beat is queued so the watering animation is visible.
- **CollectFruit** only when fruit is present. **CollectAnimalProducts** collects ground forage
  (eggs, wool, truffles by id+category) but **milk is excluded** (tool-harvested, never a ground
  object); on-animal milk/shearing is the `AnimalTaskHandler` path.
- Unreachable work is deferred within the batch, then skipped (with a routing guard cap so blocked
  work can't loop forever).

## SDV-1.6 API decisions & landmines (verified in code)

- **`PathFindController` is in `StardewValley.Pathfinding`** (moved in 1.6). After constructing,
  check `pathToEndPoint` for null and validate each tile is passable; otherwise BFS-fallback.
- **`QualifiedItemId` everywhere**; instantiate with `ItemRegistry.Create(qualifiedId, count)`.
  `ItemRegistry.Create` returns a fallback **"Error Item" rather than null** for bad ids — guarded
  by `IsErrorItem`/`IsDepositErrorItem` so junk is never shipped.
- **Vanilla worker-facing APIs leak into `Game1.player`.** Crop harvest, tool actions, and grass/
  tree breaks add items to the *player's* inventory and enqueue HUD messages even though the worker
  acted. `InvokeTaskActionGuarded` snapshots/restores transient player action state, redirects
  gained items from the player into the worker buffer, and trims the HUD-message queue.
- **`crop.harvest()` doesn't clean up `dirt.crop`** — the caller must `dirt.destroyCrop(false)` for
  non-regrowable crops; regrowable crops are left in place to regrow naturally.
- **`ResourceClump.performToolAction` calls `destroy()` internally** when health reaches 0 and
  returns `true`; never call `destroy()` manually — gate `resourceClumps.Remove` on the return.
- **Tree felling is multi-phase:** don't swing while `tree.falling`; a felled tree becomes a stump
  before it's removable (`CutTrees` reports "not complete" at that point); trunk debris spawns
  *after* the fall animation, so a **delayed debris sweep** catches it. Shaken fruit settles over
  several beats and uses the same delayed-sweep mechanism.
- **Off-screen locations don't tick terrain features.** A non-current `GameLocation` is updated via
  `GameLocation.updateEvenIfFarmerIsntHere` (characters, temp sprites, buildings, animals only);
  `terrainFeatures`/`resourceClumps`/debris-chunk ticking happens **only** in
  `UpdateWhenCurrentLocation`. So a worker-felled tree's fall animation *freezes* when the player is
  elsewhere — the trunk debris (`Tree.tickUpdate` → `createRadialDebris`) never spawns until the
  player returns (then `treethud` plays + wood drops with no worker present). Any multi-tick terrain
  animation the worker triggers off-screen must be driven manually; tree falls are completed in
  `ShiftOrchestrator.Debris.AdvanceOffscreenTreeFall` (pumps `tree.tickUpdate` to completion —
  `localSound` no-ops off-screen, so it's silent). Resource clumps, stumps, and fruit drops are fine:
  their debris spawns synchronously inside `performToolAction`/`shake`.
- **Chest mutex:** check `chest.GetMutex().IsLocked()` before writing — if the player has the chest
  UI open, the whole deposit is rerouted to overflow rather than mutating items behind their back.
- **`BuildingDrawLayer` has no GameStateQuery condition** in this version → conditional building
  overlays (lit windows, smoke) are drawn in C# (`Display.RenderedWorld`).
- **`FarmerSprite.pauseForSingleAnimation` makes `StopAnimation` a no-op** — the fake action-farmer
  used for tool actions clears it before stopping (see `CreateWorkerActionFarmer`).
- **Custom NPC lifecycle:** the farmhand is added to `farm.characters` at shift start and **removed
  before `Saving`** (`StopForSleepAndSettle`), so it's never serialized into the save.
- **Single-player only:** `MultiplayerGuard.IsMultiplayer()` = `Context.IsMultiplayer` (true also in
  split-screen); guards both the building interaction and the day-start scheduler.

## Compatibility (SVE): shape & rationale

The expansion layer (`Compat/`) is a small Strategy: `IExpansionProfile` with two implementations —
`VanillaExpansionProfile` (a Null-Object: every lookup says "no override") and `SveExpansionProfile`
(the single home for all SVE ids + playtest/source-verified data: farm-map signatures, entrance
tiles, cross-location route hops, premium-building tier mapping). `ExpansionDetector` picks the
active profile once at `GameLaunched`; `ExpansionCompatService` is the runtime seam consumers call.

**Why this shape:** the win isn't polymorphism — it's that all SVE-specific content lives in one
SMAPI-free, testable file, and vanilla behavior can't regress because vanilla takes the no-override
path. For the **two** cases we support (vanilla + SVE) the interface/null-object/selector is
slightly more ceremony than strictly needed; a nullable `SveContent` data class + `if (sve …)`
guards would do the same. It earns its keep only if a **third** expansion (Ridgeside, East Scarp, …)
is ever added — none is planned, so don't grow this further without that trigger. If SVE is ever
dropped as a goal, collapse it to the nullable-field form. **SVE stays a soft/optional dependency**
(runtime-detected, never in `manifest.json`).

Speculative seams built for never-shipped expansion features (`TryClassifyContentOverride`,
`FarmMapModIds`, `IsExpansionWorkLocation`, and the `ContentDescriptor`/`WorkClassification` types)
were pruned — they had zero real consumers; live "is this an expansion location" checks go through
`TryGetExpansionLocationDescriptor` / `IsExpansionDepositLocation`.
