# U-16 — Animals & Buildings: Code Generation Plan

**Unit**: U-16 — Animals & Buildings
**Phase**: CONSTRUCTION — Code Generation (Part 1 — Planning)
**Builds on**: approved Functional Design (FD-Q1=A…Q9=A; DEV-U16-01..04), NFR Requirements (SAFE/PERF/REL/UX/MAINT/COMPAT/PBT-U16-01..06), and NFR Design (PAT-U16-01..07; LC-U16-01..06; NFR-DES-Q1=A constructor-injected seams, Q2=A LogLevel.Warn building-skip, Q3=A stateless scanner).

> **This plan is the single source of truth for U-16 Code Generation.** Part 2 executes these steps in order, checking each box on completion. No work happens outside these steps.

---

## Stories & Traceability

| Story / Req | Coverage in U-16 |
|---|---|
| **S-08** | Completes the FR-WORK-03 priority queue — animal tasks (Feed → Pet → Collect) and building-interior tile work become executable. |
| **S-03 / S-04 deepening** | Selected buildings stop being placeholders: their interiors produce real work and route output (incl. building-interior chests). |
| **FR-WORK-09** | Building-door warp navigation (enter/exit). |
| **FR-TASK-03 / FR-TASK-04** | Feed animals (hopper) and collect animal products (egg/milk/wool/truffle). |
| **S-20** | New `log.building.*` / `log.animal.*` strings routed through `I18nHelper`. |
| **Closes TODO-05** | Building entry, indoor scanning, three animal tasks. |

**Definition of Done** (from unit-of-work.md): a selected barn/coop contract feeds, pets, and collects animal products to the configured destination and exits without losing items; a selected greenhouse/building interior runs supported tile tasks; the worker reaches building doors, transitions inside, resumes pathing indoors, and handles missing/invalid interiors gracefully.

---

## Project context & layer mapping

- **Project type**: Greenfield single-mod solution (C# / .NET 6), 3 projects: `Dayswork.Core` (pure logic), `Dayswork` (Mod/SMAPI integration), `Dayswork.Tests` (xUnit + FsCheck). Workspace root `C:\Users\kwood\Repos\dayswork`.
- **Standard layer template** (Business Logic / API / Repository / Frontend) maps as: **Business Logic** = Core pure types + `ShiftPlanBuilder`; **Integration** = the three Mod seams + orchestrator/movement-driver/ModEntry changes. **API layer / Repository layer / Frontend layer = N/A** (single-player mod; no service API, no database, no web/UI framework — UI is existing Stardew menus, untouched by U-16).
- **Brownfield-style modification**: every file below is modified in place or newly created; never duplicated (no `*_new.cs`).

### Net-new Stardew/SMAPI API surface — confirm against installed game
No existing Dayswork code references `AnimalHouse`, `FarmAnimal`, `FarmAnimal.pet`, milk-pail/shears harvest, `piecesOfHay`/hopper, `Building.humanDoor`, `building.indoors`, or Greenhouse by-type lookup. As with MFM in U-14 (DEV-U14-03), the **exact member names/signatures are confirmed at generation time** by inspecting the installed game at `X:\Steam\steamapps\common\Stardew Valley` (decompiled `Stardew Valley.dll` / reflection). If a guessed member is wrong, the seam is corrected before build. Every seam degrades gracefully (skip/no-op) if an API shape is unexpected (SAFE-U16-04).

---

## Architecture decision recorded for execution

The orchestrator is single-location today (every handler runs on `Game1.getFarm()`). U-16 inserts a **batch layer** above the proven per-item working loop **without adding a state-machine phase** (warps are intents dispatched inside `Working`/`Depositing`; the `ShiftStateMachine` successor table is untouched — MAINT-U16-02 / PBT-U16-05). Two threading decisions:

1. **`WorkItem` gains a *trailing optional* `LocationName = "Farm"`** (not a leading required field). `new WorkItem(` exists only in the orchestrator, but a default keeps the change behavior-preserving and avoids touching any caller that does not care about location. Honors domain-entities intent ("defaults to `Farm`").
2. **A `_currentLocation` field** carries the active batch's `GameLocation`. The in-batch handlers generalize from `Farm` to `GameLocation`; farm-only operations (spawn, shipping bin, farm-entrance exit, between-building staging) stay on `Game1.getFarm()`. `WorkerMovementDriver.IsTilePassableForWorker` already special-cases `Farm` and passes through other locations, so generalizing is behavior-preserving for the farm path.
3. **Scan timing** (reconciles FD Flow 1 "plan at start" with NFR-Q2=A "lazy interior scan"): batch *skeletons* (location, kind, feed flag, order) are built at `StartShift` (pure `ShiftPlanBuilder`); the **outdoor farm** is scanned eagerly at `StartShift` (preserves the existing empty-zone refund check); **building interiors** are scanned lazily at batch entry (PAT-U16-03). Empty-shift refund fires when the outdoor scan is empty **and** there are no building batches.

---

# PART 1 — PLANNING (this document)

Steps 1–22 below. Approval of this plan authorizes Part 2 (execution).

---

# PART 2 — GENERATION STEPS

## Phase A — Core pure types & planner (Dayswork.Core) — unit/PBT tested

- [x] **Step 1 — Core domain types.** Create `Dayswork.Core/Shifts/WorkBatch.cs` containing: `enum BatchKind { AnimalBuilding, Interior, OutdoorFarm }`; `enum AnimalProductKind { FloorForage, ToolHarvest, GroundForage }`; `sealed record AnimalRef(long Id, string HomeLocation, string DisplayName)`; `sealed record AnimalWorkItem(string LocationName, AnimalRef Animal, TaskKind Task)`; `sealed record WorkBatch(string LocationName, BatchKind Kind, IReadOnlyList<WorkItem> TileWork, IReadOnlyList<AnimalWorkItem> AnimalWork, bool FeedBuilding)`. Pure; no Stardew references. *(LC-U16-05, MAINT-U16-01)*

- [x] **Step 2 — Extend `WorkItem`.** Add trailing `string LocationName = "Farm"` to `Dayswork.Core/Shifts/WorkItem.cs`. Update the doc comment. *(domain-entities "WorkItem gains a location")*

- [x] **Step 3 — New shift intents.** Add to `Dayswork.Core/Shifts/ShiftIntent.cs`: `IntentWarpToLocation(string TargetLocationName, TileCoord EntryTile)`, `IntentFeedBuilding(string LocationName)`, `IntentPetAnimal(AnimalRef Animal)`, `IntentCollectFromAnimal(AnimalRef Animal)`. These dispatch inside `Working`; no state-machine change. *(domain-entities "New shift intents", MAINT-U16-02)*

- [x] **Step 4 — `ShiftPlanBuilder` (pure).** Create `Dayswork.Core/Shifts/ShiftPlanBuilder.cs`: `IReadOnlyList<WorkBatch> BuildBatchPlan(IReadOnlyList<Zone> zones, IReadOnlySet<TaskKind> enabledTasks, Func<string,bool> isAnimalHouse)`. Partitions zones (outdoor `"Farm"` zones → one `OutdoorFarm` batch skeleton; each building zone → `AnimalBuilding` if `isAnimalHouse(locationName)` else `Interior`); sets `FeedBuilding = isAnimalHouse && enabled.Contains(FeedAnimals)`; orders **AnimalBuilding → Interior → OutdoorFarm**; `TileWork`/`AnimalWork` start empty (filled by the orchestrator/scanner). Injecting `isAnimalHouse` as a delegate keeps it pure and testable. *(BR-LOC-01/02, PBT-U16-01)*

- [x] **Step 5 — Extend `ShiftContext`.** Add `IReadOnlyList<WorkBatch> Batches` (settable once at start) and `int CurrentBatchIndex`. `WorkList` continues to hold the *current batch's* remaining tile work (refilled on batch advance). Keep the existing constructor working (additive). *(domain-entities ShiftContext, LC-U16-05)*

## Phase A tests (Dayswork.Tests)

- [x] **Step 6 — `ShiftPlanBuilderTests`.** Create `Dayswork.Tests/Shifts/ShiftPlanBuilderTests.cs`. **Property (PBT-U16-01, FsCheck)**: for any random zone set, (a) every input zone maps to exactly one batch, (b) batches are emitted in non-decreasing `BatchKind` order AnimalBuilding→Interior→OutdoorFarm, (c) at most one `OutdoorFarm` batch. **Unit examples**: barn-only, greenhouse-only, mixed, outdoor-only, empty. Follow the U-02 **seed + shrunk-input logging** convention (PBT-U16-06 / PBT-08). Animal Feed→Pet→Collect ordering is already covered by `TaskPriorityOrdererTests` (PBT-U16-02) — add an assertion referencing it, no new property needed.

## Phase B — Mod orchestration seams (Dayswork/Orchestration/) — Stardew behind seams

- [x] **Step 7 — `WorkAreaScanner` (shared tile-detection engine).** Create `Dayswork/Orchestration/WorkAreaScanner.cs`. Move the **location-agnostic** tile-detection helpers out of `ShiftOrchestrator` into this internal class so there is a single source of truth (PAT-U16-05): `DetectTask`, `IsReadyToHarvest`, `IsTrellisCrop`, `CanonicalTaskTile`, `FindNavigationTile`, `FindOrthogonalNeighbour` (both overloads), `RequiresAdjacentNavigation`, `GreedyNearestNeighbour`. Generalize their `Farm` params to `GameLocation`. Constructor takes `ICapabilityEvaluator`. Expose `IReadOnlyList<WorkItem> ScanZones(GameLocation loc, IEnumerable<Zone> zones, IReadOnlySet<TaskKind> enabled, ToolSnapshot snapshot, TileCoord origin)` (sets `WorkItem.LocationName = loc.Name`). **Behavior-preserving** for the farm. *(reuse DetectTask; MAINT-U16-01)*

- [x] **Step 8 — `IndoorWorkScanner` (stateless seam, NFR-DES-Q3=A).** Create `Dayswork/Orchestration/IndoorWorkScanner.cs`. `IReadOnlyList<WorkItem> ScanInterior(GameLocation interior, IReadOnlySet<TaskKind> enabled, ToolSnapshot snapshot)`: clamp the `(0,0)..(999,999)` whole-interior placeholder to the interior map's real dimensions, then delegate to `WorkAreaScanner.ScanZones`. Returns the list; holds no state. *(PAT-U16-03, BR-IND-01/02, PERF-U16-01)*

- [x] **Step 9 — `AnimalTaskHandler` (seam).** Create `Dayswork/Orchestration/AnimalTaskHandler.cs`. Constructor takes `IMonitor`. Methods (confirm Stardew members against installed game):
  - `EnumerateAnimals(GameLocation location)` → `IReadOnlyList<(AnimalRef Ref, FarmAnimal Live)>` for animals currently in `location` whose home is a selected `AnimalHouse` (used to build per-batch `AnimalWork`, incl. grazing animals on the farm).
  - `Feed(GameLocation animalHouse)` → fills empty feed-bench slots from the building hopper/silo hay; skips deluxe auto-feed; logs `log.animal.fed` or `log.animal.no_silo` (Warn). No output. *(BR-FEED-01..04, DEV-U16-03)*
  - `Pet(FarmAnimal animal)` → re-validate "not yet petted" live, call `FarmAnimal.pet(...)` (full vanilla gains, NFR-Q1=A). *(BR-ANIM-02/03, UX-U16-01)*
  - `TryCollect(FarmAnimal animal, ItemBuffer buffer)` → re-validate "product ready" live; route by `AnimalProductKind`: `ToolHarvest` (milk/wool, tool-independent — DEV-U16-01) buffers the produced item tagged `CollectAnimalProducts`. `FloorForage`/`GroundForage` are world objects collected by the existing tile/forage path, not here. *(BR-PROD-01..06, SAFE-U16-03)*

- [x] **Step 10 — `BuildingWorkNavigator` (seam).** Create `Dayswork/Orchestration/BuildingWorkNavigator.cs`. Constructor takes `IMonitor`. (Confirm members against installed game.)
  - `bool TryResolveDoorTile(string interiorLocationName, out TileCoord outdoorDoorTile, out GameLocation interior)` — resolve the building from `Game1.getFarm().buildings` by interior name (with the Greenhouse by-type fallback used in `ChestResolver.GetBuildingOutlines`); the outdoor door tile from the footprint (`Building.humanDoor` / door offset). Returns false (→ skip) if demolished/unresolved; logs `log.building.skipped` at **Warn** (NFR-DES-Q2=A). *(PAT-U16-01, REL-U16-01)*
  - `Enter(FarmhandNpc worker, GameLocation interior, TileCoord interiorEntryTile)` — warp handoff into the interior (via `WorkerMovementDriver.WarpWorker`, Step 11); log `log.building.entering`.
  - `ExitToFarm(FarmhandNpc worker, TileCoord outdoorDoorTile)` — warp handoff back to the farm.
  These three are also used at deposit time (Step 14). All failure paths return an outcome the orchestrator treats as a building skip (REL-U16-04).

## Phase C — Worker movement (Dayswork/Worker/)

- [x] **Step 11 — `WorkerMovementDriver` warp handoff.** Add `void WarpWorker(FarmhandNpc worker, GameLocation from, GameLocation to, TileCoord entryTile)`: remove worker from `from.characters`, add to `to.characters`, set `worker.currentLocation = to` and `worker.Position` to the entry tile pixel, and `Clear()` any in-flight path. Confirm interior passability: `IsTilePassableForWorker` already passes through non-`Farm` locations (the building-occupancy check is `Farm`-only), so no change is needed for interiors beyond verifying via play-test. *(PAT-U16-06/07, REL-U16-04, BR-NAV-01)*

## Phase D — Orchestrator integration (Dayswork/Orchestration/ShiftOrchestrator.cs) — HIGHEST RISK

- [x] **Step 12 — Generalize in-batch handlers `Farm` → `GameLocation` [RISK].** Add `private GameLocation _currentLocation = null!` (set to the farm at spawn). Re-point the per-item working path to `_currentLocation`: `OnUpdateTicked` dispatch, `HandleMovement`, `HandleTaskAction`, `InvokeTaskAction` + all `Invoke*`/`IsTaskComplete`/debris helpers, `SampleProgress`, `IsTileReachable`, and the `QueueStuckTeleport` reachability scan. Delete the moved scan helpers (now in `WorkAreaScanner`, Step 7) and call the scanner instead. **Behavior-preserving for the farm-only path** — the existing 199 tests and the outdoor play-test must still pass unchanged. Spawn, shipping-bin, farm-entrance exit stay on `Game1.getFarm()`.

- [x] **Step 13 — Batch loop in `StartShift` + batch advance [RISK].**
  - `StartShift`: build `Batches` via `ShiftPlanBuilder.BuildBatchPlan(...)` (pass `isAnimalHouse` resolving against `Game1.getFarm().buildings`). Eagerly scan the **outdoor farm** (existing `BuildWorkList` logic via `WorkAreaScanner`), then append farm-batch `AnimalWork` (grazing animals via `AnimalTaskHandler.EnumerateAnimals(farm)`) and ground-truffle `CollectAnimalProducts` tile items. **Empty check**: if the outdoor batch has no tile work **and** no animal work **and** there are no building batches → existing mailed-refund / no-show path. Otherwise spawn and begin batch 0.
  - **Batch begin**: if `Kind != OutdoorFarm`, `BuildingWorkNavigator.TryResolveDoorTile` → walk to door → `Enter` (set `_currentLocation = interior`); on failure skip the batch (keep buffer). Then `IndoorWorkScanner.ScanInterior` fills `TileWork` and `AnimalTaskHandler.EnumerateAnimals(interior)` fills `AnimalWork` (PAT-U16-03/04). Enqueue animal work first (Feed→Pet→Collect), then tile work into `WorkList`.
  - **Batch advance** (replaces the "deposit when WorkList empty" branch in `AdvanceWorkList`): when the current batch's animal+tile work is exhausted, if it was a building `ExitToFarm` (set `_currentLocation = farm`); increment `CurrentBatchIndex`; begin the next batch, or `BeginDeposit()` when no batches remain.
  - **Animal dispatch**: handle `IntentFeedBuilding`/`IntentPetAnimal`/`IntentCollectFromAnimal` in the `OnUpdateTicked` switch via `AnimalTaskHandler`, using the live-targeting + `StuckDetector` skip (PAT-U16-02/04). Approach uses the existing nav to the animal's live tile.
  - **Mid-building 8pm cap**: in `OnTimeChanged`/`BeginDeposit`, if `_currentLocation` is a building, `ExitToFarm` before the deposit run so deposit/exit originate on the farm (REL-U16-05, BR-NAV-03).

- [x] **Step 14 — Multi-location deposit run [RISK].** Extend the deposit loop so a trip whose `ChestDestination.Ref.LocationName` is a building interior warps in (`BuildingWorkNavigator.Enter`), deposits via the existing `ExecuteTrip`/`ChestResolver`, then warps back; shipping-bin and farm-chest trips run on the farm unchanged. Deposit-run warps are **unbilled** (refund formula untouched — BR-DEP-04). *(FD-Q8=B, BR-DEP-01/02, PAT-U16-07, TS-U16-10)*

- [x] **Step 15 — Location-aware cleanup.** `ClearWorker` removes the worker from `_farmhand.currentLocation.characters` (fallback `Game1.getFarm()`), not a hardcoded farm. Same for the `StopForSleepAndSettle` path (worker may sleep inside a building). *(PAT-U16-06, SAFE-U16-02)*

## Phase E — Wiring & strings

- [x] **Step 16 — `ModEntry` wiring.** Construct `WorkAreaScanner`, `IndoorWorkScanner`, `AnimalTaskHandler`, `BuildingWorkNavigator` and inject them into `ShiftOrchestrator`'s constructor (NFR-DES-Q1=A). Order them with the existing seam construction block. *(LC-U16-06)*

- [x] **Step 17 — i18n keys.** Add to `Dayswork/i18n/default.json`: `log.building.entering`, `log.building.skipped`, `log.animal.fed`, `log.animal.no_silo`. Route all four through `I18nHelper.Get(...)` at their call sites (no hardcoded user-visible strings — S-20 / BR-I18N-01). No new mail strings. *(UX-U16-02)*

## Phase F — Build, test, deploy

- [x] **Step 18 — Build (no deploy).** `dotnet build Dayswork.sln /p:EnableModDeploy=false` → expect 0 errors / 0 warnings. Fix any issues before proceeding.

- [x] **Step 19 — Test.** `dotnet test Dayswork.sln` → expect all existing tests (199 + 1 expected skip) plus the new `ShiftPlanBuilderTests` to pass. Investigate/fix any regression (esp. the Step 12 generalization).

- [x] **Step 20 — Build + auto-deploy.** `dotnet build Dayswork.sln` → 0/0 and auto-deploy to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork` (retry if the DLL is locked by a running game).

## Phase G — Documentation & state

- [x] **Step 21 — Code summary + play-test checklist.** Create `aidlc-docs/construction/u-16-animals-buildings/code/code-summary.md`: created vs modified files, the batch-layer design, the net-new Stardew APIs actually used (recorded after confirmation), and a **play-test checklist** covering the DoD: (1) coop/barn feed→pet→collect to a chest, exits with no items lost; (2) greenhouse/shed interior tile work; (3) door warp in/out resumes pathing indoors; (4) demolished/blocked building skipped gracefully (Warn log, shift continues); (5) building-interior chest deposit (warp in/out at deposit); (6) 8pm cap fires inside a building → returns to farm → deposits; (7) sleep inside a building → hard-stop, items mailed, worker not serialized; (8) **outdoor regression re-check** (U-13 greedy-NN, stuck, debris timing unchanged); (9) milk/shear with player owning no pail/shears (DEV-U16-01); (10) grazing animal pet/collect in the outdoor batch.

- [x] **Step 22 — Update state & audit.** Mark all plan steps `[x]`; mark S-08 / S-03-S-04 deepening implemented in the story map note; update `aidlc-state.md` (Code Generation complete for U-16, TODO-05 closed) and append the `audit.md` entry. Present the standardized 2-option completion message.

- [x] **Step 23 — Playtest fix: building name resolution.** Fix the runtime mismatch where selected building zones saved as names like `Greenhouse`, `Big Barn`, and `Coop` cannot be resolved at shift time. Reuse/centralize the same building/interior resolution logic for `ChestResolver.GetBuildingOutlines`, `ShiftPlanBuilder` animal-house classification, `BuildingWorkNavigator.TryResolveDoorTile`, and building-interior deposit trips; rebuild, test, auto-deploy, and update U-16 docs/state/audit.

- [x] **Step 24 — Playtest fix: canonicalize saved building zone names at shift start.** Normalize any persisted non-farm zone name to the resolved interior `GameLocation.Name` before batch planning so older contracts saved with labels like `Big Barn`, `Coop`, or `Greenhouse` execute against stable runtime names. Also use the same normalization when restoring building selections in the edit UI. Rebuild, test, auto-deploy, and update U-16 docs/state/audit.

- [x] **Step 25 — Diagnostic build for repeated building-resolution failure.** After playtest still reported the same three building skips, add targeted runtime diagnostics: startup build marker, raw→normalized zone planning log, resolver candidate dump on failed building resolution, and `dayswork_debug_buildings <name>` console command. Rebuild, test, auto-deploy, and update U-16 docs/state/audit. This step intentionally gathers the missing runtime fact before the next fix.

- [x] **Step 26 — Playtest fix: approach reachable outdoor door tile.** Use the Step 25 diagnostics to identify the true failure: resolution matched the correct buildings, but navigation targeted the human-door tile inside the building footprint, which `WorkerMovementDriver.IsTilePassableForWorker` rejects on the farm. Change building resolution to return a reachable outdoor approach tile adjacent to the door (preferring the tile below the door), keep interior warp behavior unchanged, rebuild, test, auto-deploy, and update U-16 docs/state/audit.

- [x] **Step 27 — Playtest refinement: visible animal-building work.** Incorporate user feedback that animal-building work should feel less instantaneous: replace automatic feed-on-entry with visible hopper and individual feeder-slot work when troughs are not already filled; add a collect-animal-product action beat with milking/shearing sounds before buffered product collection; and walk to the interior exit tile before warping back to the farm after a building batch. Rebuild, test, auto-deploy, and update U-16 docs/state/audit.

- [x] **Step 28 — Playtest fix: discover animal-house feed/exit tiles.** Replace the Step 27 guessed hopper/feed-row coordinates with map/object discovery from the loaded animal-house interior, logging discovered hopper/feed candidates and only falling back with diagnostics if the map has no usable feed signal. Also change building exit walking to target a reachable interior approach tile near the exit warp instead of the warp tile itself. Rebuild, test, auto-deploy, and update U-16 docs/state/audit.

- [x] **Step 29 — Playtest fix: keep feeder navigation on the aisle.** Use the Step 28 feed-plan logs to fix the remaining feeding path issue: the hopper was resolved correctly, but the feed-row fallback pointed above the hopper and selected unreachable/object tiles as navigation targets. Change fallback feeder work to use the hopper row as the visual feed row, route the worker to passable aisle tiles below/near each feed slot, never use the hopper/object tile as a feeder navigation tile, rebuild, test, auto-deploy, and update U-16 docs/state/audit.

- [x] **Step 30 — Playtest fix: use actual trough tiles and vanilla hay placement.** Replace feeder-row fallback placement with real trough discovery from `Back:Trough` tile properties, count existing placed hay objects instead of `piecesOfHay`, and place hay by dropping vanilla hay `"(O)178"` through `AnimalHouse.dropObject(...)` on the discovered trough tile. Rebuild, test, auto-deploy, and update U-16 docs/state/audit.

- [x] **Step 31 — Playtest fix: rescan outdoor animals at outdoor batch start.** Fix outside pet/milk/shear misses caused by building the outdoor farm animal queue at shift start before animals move outside. Keep outdoor tile work eagerly scanned for refund behavior, but refresh outdoor `AnimalWork` from live farm animals when the outdoor batch begins after buildings. Rebuild, test, auto-deploy, and update U-16 docs/state/audit.

- [x] **Step 32 — Playtest fix: per-animal task grouping (pet then milk before moving on).** Fix `BuildAnimalWork` in `ShiftOrchestrator`: the final `.OrderBy(task kind)` sorted all work items globally, causing the worker to pet every cow first and then loop back to milk them. Replace with `.GroupBy(animal.Id).SelectMany(group => group.OrderBy(task kind))` so the worker completes all tasks for one animal (pet → milk/shear) before moving to the next. Rebuild, test, auto-deploy, and update U-16 docs/state/audit.

- [ ] **Step 34 — Playtest fix: big rocks require multiple hits and drop correct loot.** Two bugs in `InvokeClearRock` for resource clumps (boulders): (1) `damage = 0` was passed to `performToolAction` (no health reduction per hit); (2) the clump was always force-removed after a single call and `destroy()` was never called. Reflected game DLL to confirm exact API: `ResourceClump.health` (NetFloat), `performToolAction(Tool, int damage, Vector2)` returns bool (true = health ≤ 0), `destroy(Tool, GameLocation, Vector2)` spawns the actual loot drops (3680-byte method vs 1327 for PTA). Fix: pass `damage = 1` per swing; only call `destroy()` + `Remove()` when `performToolAction` returns `true`; collect debris after both the hit and the destroy separately. The existing `IsTaskComplete` check (`FindResourceClumpAt is null`) and the action-loop retry pattern (`_actionPending` reset) naturally deliver multi-hit behavior at one swing per animation cycle. Deploy pending — game DLL locked during playtest; rebuild with `dotnet build Dayswork.sln` after closing the game.

- [x] **Step 33 — Playtest fix: greenhouse crop harvest infinite loop + items going to player inventory.** Two bugs in `InvokeHarvest`: (1) `Crop.harvest()` does not clear `dirt.crop`, so `IsTaskComplete` (checking `hd.crop is null`) never returned true → infinite harvest loop on the same tile. (2) SDV 1.6 `Crop.harvest(null junimoHarvester)` adds produce directly to `Game1.player` (or creates debris the player magnet immediately collects), bypassing the worker buffer. Fix: reflected the actual game DLL (`Crop.RegrowsAfterHarvest()`, `HoeDirt.destroyCrop(bool)`, `HoeDirt.readyForHarvest()`) to use authoritative vanilla APIs; snapshot player inventory by object reference before calling `harvest()`; after the call, diff and redirect any new/increased stacks to the worker buffer then remove them from the player; call `dirt.destroyCrop(false)` when `!dirt.crop.RegrowsAfterHarvest()` (the correct caller-side cleanup). Updated `IsTaskComplete` for `HarvestCrops` to use `!hd.readyForHarvest()` (covers regrowable crops). Updated `WorkAreaScanner.DetectTask` to use `dirt.readyForHarvest()` and removed the now-dead hand-rolled `IsReadyToHarvest(Crop)` helper. Rebuild, test, auto-deploy, and update U-16 docs/state/audit.

---

## Risk notes

- **Steps 12–14 are the high-risk core**: they refactor a 1467-line, play-tested orchestrator. Mitigation: Step 12 is strictly behavior-preserving (regression-gated by the 199-test suite + outdoor play-test); Steps 13–14 add new branches that the farm-only path never enters. No state-machine phase is added (PBT-U16-05 holds).
- **Net-new Stardew APIs** (Steps 9–11): confirmed against the installed game during generation (DEV-U14-03 precedent); each seam degrades gracefully on an unexpected shape.
- **PBT scope** (Partial mode): only `ShiftPlanBuilder` adds a new property (Step 6); animal/warp/feed/collect behavior is play-tested per the DoD (PBT-U16 "Not PBT" list).

## Artifact output

- Application code: `Dayswork.Core/Shifts/` (new types, planner, intents, context), `Dayswork/Orchestration/` (3 seams + scanner + orchestrator), `Dayswork/Worker/WorkerMovementDriver.cs`, `Dayswork/ModEntry.cs`, `Dayswork/i18n/default.json`.
- Tests: `Dayswork.Tests/Shifts/ShiftPlanBuilderTests.cs`.
- Docs: `aidlc-docs/construction/u-16-animals-buildings/code/code-summary.md`.
