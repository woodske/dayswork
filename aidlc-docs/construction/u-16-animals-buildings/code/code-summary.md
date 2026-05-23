# U-16 Animals & Buildings — Code Summary

**Stage**: CONSTRUCTION — Code Generation  
**Status**: Complete, awaiting user review/play-test approval  
**Verification**: `dotnet build Dayswork.sln /p:EnableModDeploy=false` 0 errors / 0 warnings; `dotnet test Dayswork.sln` 201 passed / 1 expected skip; `dotnet build Dayswork.sln` 0 errors / 0 warnings and auto-deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.

## Created Files

- `Dayswork.Core/Shifts/WorkBatch.cs` — `WorkBatch`, `BatchKind`, `AnimalRef`, `AnimalWorkItem`, and `AnimalProductKind`.
- `Dayswork.Core/Shifts/ShiftPlanBuilder.cs` — pure batch skeleton planner for outdoor farm, animal buildings, and other interiors.
- `Dayswork.Tests/Shifts/ShiftPlanBuilderTests.cs` — example tests plus FsCheck property coverage for batch partitioning/order.
- `Dayswork/Orchestration/WorkAreaScanner.cs` — shared location-agnostic tile scanner extracted from `ShiftOrchestrator`.
- `Dayswork/Orchestration/IndoorWorkScanner.cs` — stateless whole-interior scanner that clamps to real map bounds.
- `Dayswork/Orchestration/AnimalTaskHandler.cs` — animal enumeration, feed, pet, and product collection seam.
- `Dayswork/Orchestration/BuildingWorkNavigator.cs` — building resolution, door tile lookup, enter/exit warp handoff, and skip logging.
- `Dayswork/Integration/BuildingLocationResolver.cs` — shared resolver for building zones saved as interior names, building type names, upgraded names, or farm warp targets.

## Modified Files

- `Dayswork.Core/Shifts/WorkItem.cs` — added trailing `LocationName = "Farm"` for indoor tile work.
- `Dayswork.Core/Shifts/ShiftIntent.cs` — added building/animal intent records.
- `Dayswork.Core/Shifts/ShiftContext.cs` — added `Batches` and `CurrentBatchIndex`.
- `Dayswork/Orchestration/ShiftOrchestrator.cs` — added batch execution, location-aware tile handling, animal work queue, building entry skip handling, building-interior deposit trips, and location-aware cleanup.
- `Dayswork/Worker/WorkerMovementDriver.cs` — added `WarpWorker(...)`.
- `Dayswork/ModEntry.cs` — wired `WorkAreaScanner`, `IndoorWorkScanner`, `AnimalTaskHandler`, `BuildingWorkNavigator`, and shared movement driver.
- `Dayswork/Integration/ChestResolver.cs` — uses the shared building/interior resolver when listing building outlines.
- `Dayswork/UI/ZoneDrawMenu.cs` — normalizes legacy saved building-zone names when restoring edit-mode building selections.
- `Dayswork/i18n/default.json` — added `log.building.entering`, `log.building.skipped`, `log.animal.fed`, and `log.animal.no_silo`.

## Implementation Notes

- U-16 keeps the existing `ShiftStateMachine` phases unchanged. Building entry, animal work, and interior tile work execute inside `Working`; building chest trips execute inside `Depositing`.
- The outdoor farm path now uses `WorkAreaScanner` but preserves the existing scanner behavior and greedy nearest-neighbor routing.
- Building interiors are scanned lazily after entry through `IndoorWorkScanner`.
- Animal pet/collect work fixes the animal set by stable `AnimalRef.Id`, then resolves and re-validates the live animal at execution time.
- Milk/wool collection is tool-independent per DEV-U16-01 and buffers the produced item as `CollectAnimalProducts`.
- Floor/ground animal products (eggs, duck egg/feather, dinosaur egg, rabbit's foot, truffles) are detected by `WorkAreaScanner` as `CollectAnimalProducts` tile work and buffered through the normal item path.
- Playtest fix Step 23 centralizes building resolution so saved zone names such as `Greenhouse`, `Big Barn`, and `Coop` resolve at runtime for entry, animal-house classification, and building-interior chest deposit trips.
- Playtest fix Step 24 canonicalizes persisted non-farm zone names to interior `GameLocation.Name` values at shift start, so older contracts stop relying on display labels during batch planning and animal-home matching.
- Diagnostic Step 25 adds a startup build marker, raw-to-normalized zone planning log, resolver candidate dump on failed building resolution, and `dayswork_debug_buildings <name>` console command so the next playtest exposes the exact runtime farm/building graph.
- Playtest fix Step 26 changes outdoor building navigation to target a reachable approach tile next to the human door instead of the occupied door tile within the building footprint. This addresses the observed skips where diagnostics showed `matches=True` for `Greenhouse`, `Big Barn`, and `Coop` but farm pathing failed.
- Playtest refinement Step 27 makes animal-building work visible: feeding now creates hopper/feeder `FeedAnimals` work items instead of calling `feedAllAnimals()` on entry, milk/shear collection plays a short action beat plus `Milking`/`Shears` sound before buffering produce, and building batches walk back toward the interior exit before warping out.
- Playtest fix Step 28 replaces Step 27's guessed animal-house feed coordinates with runtime discovery: the hopper is resolved from live interior objects (`(BC)99`) or feed-hopper tile actions, feeder slots start from the map `Feed` property, and `[Dayswork][feed-plan]` logs the resolved hopper/feed coordinates for playtest verification. It also targets a reachable interior exit approach tile instead of the warp tile itself.
- Playtest fix Step 29 keeps feeder navigation on passable aisle tiles. When the map `Feed` property is absent, the fallback uses the hopper row as the visual feed row, excludes hopper/object tiles as navigation targets, and routes each visible feeder placement to a passable tile below/near the slot. The current deployed build marker is `build=U16-Step29`.
- Playtest fix Step 30 replaces the feeder-row fallback with the vanilla trough model: actual feed slots are discovered from `Back:Trough` tile properties, existing feed is counted as placed Hay objects, and placement drops vanilla Hay `"(O)178"` through `AnimalHouse.dropObject(...)` so the trough visibly fills. The current deployed build marker is `build=U16-Step30`.
- Playtest fix Step 31 refreshes outdoor farm animal work when the outdoor batch begins. Outdoor tile work is still scanned at shift start for the existing refund/no-show behavior, but pet/milk/shear work for animals living in selected barns/coops is rebuilt from live farm animals after building batches complete. The current deployed build marker is `build=U16-Step31`.
- Sleep-stop cleanup removes the worker from its actual current location, not only from the farm.

## Confirmed Stardew APIs Used

- `StardewValley.Buildings.Building.GetIndoors()`, `GetIndoorsName()`, `getPointForHumanDoor()`, `indoors`, `buildingType`, `tileX`, and `tileY`.
- `StardewValley.AnimalHouse.Animals`, `ParentBuilding`, `dropObject(...)`, `numberOfObjectsWithName(...)`, and `feedAllAnimals()`.
- `StardewValley.Farm.Animals`, `piecesOfHay`, and `getShippingBin(...)`.
- `StardewValley.FarmAnimal.myID`, `homeInterior`, `home`, `displayName`, `wasPet`, `pet(...)`, `currentProduce`, `produceQuality`, `daysSinceLastLay`, and `HandleStatsOnProduceCollected(...)`.
- `StardewValley.GameLocation.warps` and `characters`.
- `GameLocation.Map.Properties["AutoFeed"]`, `GameLocation.doesTileHaveProperty(..., "Trough", "Back", false)`, interior `Objects.Pairs`, vanilla feed hopper big craftable `(BC)99`, and vanilla hay object `"(O)178"`.

## Play-Test Checklist

- [ ] Coop/barn contract feeds, pets, and collects animal products to a configured chest, then exits with no items lost.
- [ ] Feeding visibly visits the hopper first, then individual feeder slots only when the trough is not already full.
- [ ] Milk/shear product collection plays an appropriate action beat and sound before the item is buffered.
- [ ] Worker walks back to the interior exit tile before leaving a building.
- [ ] Greenhouse or shed interior runs supported tile work using the same scanner as outdoor work.
- [ ] Worker reaches building door, transitions inside, resumes pathing, and transitions back out.
- [ ] Demolished/unresolvable/blocked building logs `log.building.skipped` at Warn and the shift continues.
- [ ] Building-interior chest deposit warps in, deposits, and warps back to the farm.
- [ ] 8pm cap inside a building returns worker to farm before deposit/exit.
- [ ] Sleep inside a building hard-stops the worker, mails collected items/refund, and does not serialize the worker into the building.
- [ ] Outdoor regression check: U-13 greedy nearest-neighbor, stuck handling, and debris timing still behave as before.
- [ ] Milk/shear works when the player owns no milk pail/shears (DEV-U16-01).
- [ ] Grazing animal pet/collect runs in the outdoor batch for animals whose selected home building is part of the contract.

## Extension Compliance

- **Property-Based Testing**: Compliant for Partial mode. PBT-03/PBT-U16-01 covered by `ShiftPlanBuilderTests.AnyZoneSet_MapsEachLocationToOneOrderedBatch`; PBT-07 uses structured `Zone` generator data; PBT-08 relies on FsCheck.Xunit seed/shrunk-input output; PBT-02/PBT-09 unchanged from existing project coverage; non-pure Stardew interactions are play-test scoped as documented.
- **Security Baseline**: N/A / disabled in `aidlc-state.md` (no network, PII, auth, or external input surface added).
