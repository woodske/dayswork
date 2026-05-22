# U-16 — Tech Stack Decisions

**Unit**: U-16 — Animals & Buildings

NFR decisions applied: NFR-Q1=A (full vanilla animal-care gains), NFR-Q2=A (lazy interior scan at batch entry), NFR-Q3=A (reuse stuck detection for moving/unreachable animals). FD decisions FD-Q1=A…Q9=A and DEV-U16-01..04 apply.

---

## TS-U16-01 — No new frameworks or dependencies
Testing stays on **xUnit** + **FsCheck**. U-16 adds no NuGet package and no manifest dependency: MFM is already required (U-14), GMCM stays optional (U-17). All new logic is plain Mod-layer C# over existing Stardew/SMAPI APIs. *(COMPAT-U16-01)*

## TS-U16-02 — New orchestration helpers wrap Stardew behind seams (NFR-MAINT-03)
Three new Mod components in `Dayswork/Orchestration/` isolate the new game-API surface:
- **`BuildingWorkNavigator`** — door approach + cross-location warp handoff (also used to reach in-building chests at deposit time).
- **`IndoorWorkScanner`** — reuses the existing `DetectTask` over a building interior `GameLocation`.
- **`AnimalTaskHandler`** — feed/pet/collect against live `FarmAnimal` state.

Core gains only pure types (`WorkBatch`, `BatchKind`, `AnimalWorkItem`, `AnimalRef`, `AnimalProductKind`) and a `LocationName` on `WorkItem`. *(MAINT-U16-01)*

## TS-U16-03 — Cross-location movement: PathFindController within a location + manual warp (FR-WORK-09)
In-location pathing reuses the existing `WorkerMovementDriver` / `PathFindController`. A **warp** is a manual handoff: remove the worker NPC from the old location's `characters`, add it to the target location's, set its entry `Position` and `currentLocation`. The outdoor **door tile** comes from the building footprint. Exact members (`Building.humanDoor` / door warp tile, `AnimalHouse`, `Building.indoors`, Greenhouse by-type lookup) are confirmed against the installed game at Code Generation, as was done for MFM in U-14 (DEV-U14-03). `IsTilePassableForWorker` gains an interior branch alongside the existing `Farm` building-occupancy special-case. *(BR-NAV-01, REL-U16-04)*

## TS-U16-04 — Animal care via vanilla interactions for full gains (NFR-Q1=A)
Feed/Pet/Collect call the **vanilla** animal interactions so friendship/mood/quality apply identically to the player doing it:
- **Pet** → `FarmAnimal.pet(...)` (sets `wasPet`, applies friendship/mood).
- **Collect** → the vanilla produce path for floor items, and the milk-pail/shears harvest path for `ToolHarvest`; truffles via the forage pickup path.
- **Feed** → place hay on feed-bench tiles drawing from the building hopper (silo-supplied).

Exact members (`FarmAnimal.pet`, `currentProduce`/`GetHarvestType`, `MilkPail`/`Shears` use, `Farm.piecesOfHay` / hopper, deluxe auto-feed flag) confirmed at Code Generation. *(UX-U16-01, BR-FEED-01, BR-PROD-01/02)*

## TS-U16-05 — Milk/shear are tool-independent (DEV-U16-01)
The worker performs milk/shear regardless of whether the player owns a milk pail / shears (those tools are un-tiered; the worker is tool-independent per DEV-U15-03). The capability snapshot may record their presence for completeness but does not gate the action. *(DEV-U16-01, BR-PROD-02)*

## TS-U16-06 — Lazy interior scan reuses DetectTask (NFR-Q2=A)
`IndoorWorkScanner` runs the same `DetectTask` scan over the interior `GameLocation` at the moment the worker enters that batch, clamped to the interior's real map bounds (the `(0,0)..(999,999)` zone is a "whole interior" placeholder). One scan per location per shift. *(PERF-U16-01, BR-IND-01/02)*

## TS-U16-07 — Moving-target give-up reuses StuckDetector (NFR-Q3=A)
The worker targets an animal's live position and re-targets while approaching; the existing U-13 `StuckDetector` provides the bounded give-up (skip the animal if unreachable within the stuck window). No new give-up component. *(REL-U16-02/03)*

## TS-U16-08 — Worker cleanup is location-aware (SAFE-U16-02)
`ShiftOrchestrator.ClearWorker` (and the sleep-stop path) removes the worker from **its current location**, not a hardcoded `Game1.getFarm()`. This guarantees the worker is never serialized into a building on a save that occurs while it is indoors. *(SAFE-U16-02, REL-U16-04)*

## TS-U16-09 — No new save data; no estimator/pricing change
Building zones already persist in the contract segment; animal/feed/produce state is live game state; the settlement letter reuses U-15's next-morning mechanism. The deposit keeps `DepositHoursPolicy.FlatPreviewHours` (DEV-U16-04 / DEV-U15-07); no `HoursEstimator`/pricing rework in U-16. *(SAFE-U16-05, DEV-U16-04)*

## TS-U16-10 — Multi-location deposit run extends the existing loop (FD-Q8=B)
The pure `DepositPlanner` is unchanged. The orchestrator's deposit execution gains a warp step for any building-interior chest trip (enter → deposit via `ChestResolver` → exit), while shipping-bin and farm-chest trips run on the farm as today. *(BR-DEP-01/02)*
