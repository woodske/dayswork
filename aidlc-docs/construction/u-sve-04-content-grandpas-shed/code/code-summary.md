# Code Summary — U-SVE-04 New Content + Grandpa's Shed

**Stage**: CONSTRUCTION → U-SVE-04 → Code Generation (Part 2). Executing the approved 7-step plan; this is a **partial increment** — the SMAPI-coupled feature steps (3 & 4) are gated on one runtime data point each (consistent with how entrances and feed capacity were playtest-validated).

## Completed

### Step 1 — Category-based animal-product detection (TODO-07 / S-24) ✅
Fixes the reported bug where the worker ignored goose eggs, rabbit wool (vanilla Wool 440), and camel wool.
- `WorkAreaScanner.IsAnimalProductForageObject` now matches a ground object that is **(a)** a legacy whitelisted id (guaranteed parity — keeps Truffle 430 etc. without hardcoding its category), **or (b)** a non-big-craftable item whose `Category` ∈ { Egg `-5`, Animal Goods `-18` }, minus a (default-empty) exclude set. This covers vanilla Egg/Wool(440)/Duck Feather/Rabbit's Foot/Truffle **and** SVE Goose Egg (`-5`) / Golden Goose Egg (`-5`) / Camel Wool (`-18`) **and** any future product. Verified from SVE source: Goose/Camel are `HarvestType: DropOvernight` ground products with those categories.
- Collection (`ShiftOrchestrator.InvokeCollectAnimalProduct`) shares the predicate, so detection and collection stay consistent (no detect-but-can't-remove rescan loop). With detection fixed, these products are now collected rather than ignored, and the U-SVE-03 rescan guard still bounds any genuinely unreachable forage.
- Extracted a pure `IsAnimalProductForageObject(qualifiedId, id, category, bigCraftable)` overload; added `Dayswork.Tests/Orchestration/AnimalProductDetectionTests` (legacy parity, SVE products, non-products, big-craftable exclusion, FsCheck determinism).
- **Closes TODO-07.**

### Step 2 — Content-classification overrides (S-24) ✅ (no-gap, verified)
Source audit found **no verified custom-content gap**: SVE introduces no custom `ResourceClump` indices (it only Harmony-patches `ResourceClump.performToolAction`), and its custom wild trees (`…_Birch_Tree`, `…_Fir_Tree`) are vanilla `Tree` features already handled generically by `ObjectTargetClassifier.ClassifyTree`; generic crops flow through `HoeDirt`. Per Q3=A ("verified gaps only; empty = passthrough") the `SveExpansionProfile` content-override table stays **empty** and no classifier change is made. The override seam (U-SVE-01) remains available for a future verified gap. *(Deviation from the plan's "wire the consultation" wording: wiring lossy None/Axe/Pick plumbing into the `AxeTarget`/`PickTarget` classifier for a permanently-empty table would add cost with zero behavior change — wire it when a concrete gap exists.)*

### Step 4 — Unique-name building keying (TODO-08) ✅
Playtest evidence (`[Dayswork][building-outline]`) confirmed animal-house interiors get **distinct** GUID-suffixed unique names (`Coop1bc97e5f-…` vs `Coopd3136445-…`) while `Name` is just the type (`Coop`). So selections are now keyed by `NameOrUniqueName`, made consistent end-to-end:
- `ChestResolver.GetBuildingOutlines` → `BuildingOutline.LocationName = indoors.NameOrUniqueName`.
- `LegacyScopeBootstrapper.TryClassify` (vanilla path) — **2nd-pass fix**: it previously built the selection from `TryInferAnimalBuildingSelection(outline.DisplayName)`, which embedded the *type* name (`"Coop"`) as the LocationName, so base buildings still collapsed (premium already used `outline.LocationName`). Now it infers only the **tier** and rebuilds the selection with the unique `outline.LocationName`.
- `BuildingLocationResolver` exact-match now includes `interior.NameOrUniqueName` (`MatchesExactly`/`BuildingMatchInfo` gained an `InteriorUniqueName`); `NormalizeLocationName` returns the unique name.
- `AnimalTaskHandler` matches an animal's home by **any** of {unique name, type name, building type} (`HomeLocationKeys`), and `AnimalRef.HomeLocation`/`ResolveHomeLocation` use the precise unique name. New contracts service the specific building; **legacy contracts** (type-name keys) still match all same-type buildings via the type-name fallback — no save-schema change.
- `ZoneAndChestMenu.FormatAnimalScopeSummary` strips the trailing GUID for display (`Coop1bc97e5f-…` → `Coop`).
- Per-building runtime work (`ShiftOrchestrator` AnimalBuilding batch, `selectedHome = { batch.LocationName }`) resolves each unique key to its own interior.
- Tests: `BuildingLocationResolverTests` extended (two same-type coops resolve by unique name; legacy type-name still resolves).
- **Closes TODO-08.**

## Pending — gated on runtime evidence (not yet implemented)

### Step 3 — Grandpa's Shed work location (S-25) ⏳
`IsExpansionWorkLocation` is currently unconsumed; making the worker scope/enter/deposit at `Custom_GrandpasShedGreenhouse` (`CanPlantHere: true`) requires integrating into the hiring scope, the greenhouse/indoor crop scan, navigation (warp/arrival), and chest resolution — which needs runtime verification of how that location connects to the farm. Deferred pending that evidence; no dead data added.

## Verification
- `dotnet build Dayswork.sln /p:EnableModDeploy=false` → **0 warnings / 0 errors**.
- `dotnet test Dayswork.sln` → **378 passed / 1 expected skip / 0 failed** (was 364; +14: detection + unique-key resolver tests).
- Deploy: build when game closed.

## Next
1. Playtest: confirm goose egg/camel wool/rabbit wool pickup (Step 1) and that **all four+ buildings (incl. two base + two premium) are now serviced** (Step 4 / TODO-08).
2. Implement Step 3 (Grandpa's Shed consumption) once its location graph is confirmed — the only remaining U-SVE-04 item.
3. Finalize U-SVE-04, then global Build & Test.
