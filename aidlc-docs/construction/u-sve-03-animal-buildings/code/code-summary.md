# Code Summary — U-SVE-03 SVE Animal Buildings

**Stage**: CONSTRUCTION → U-SVE-03 → Code Generation (Part 2). Executed the approved 8-step plan.

## What shipped
Two data-driven behaviors wired onto the existing U-SVE-01 compat seams, fixing how Dayswork services SVE Premium Coop/Barn — with no new components, no enum/save-schema change, and byte-for-byte vanilla behavior when SVE is absent.

1. **Premium → nearest-vanilla-tier mapping** so premium buildings scope/price as **Deluxe** instead of the cheapest Coop/Barn tier they previously fell into.
2. **Data-driven feed capacity** (`min(troughTiles, MaxOccupants)`) replacing the hardcoded `Deluxe=12/Big=8/else=4` ladder.

## Source-grounded findings that shaped the unit
- SVE Premium Coop/Barn building types: `FlashShifter.StardewValleyExpandedCP_PremiumCoop` / `_PremiumBarn`; both `MaxOccupants 16`; upgrades above Deluxe.
- **Both premium interior maps set `AutoFeed = T`** → they auto-feed like vanilla Deluxe, so the worker's existing auto-feed gate already (correctly) skips manual feeding. The pre-source "16-animal underfeed" premise does not apply to these buildings; the capacity change is a defensive generalization for any non-auto-feed building and removes the hardcoded assumption.

## Files modified
- `Dayswork.Core/Compat/SveExpansionProfile.cs` — added `PremiumCoopBuildingType` / `PremiumBarnBuildingType` constants; `MapPremiumBuildingTier` now returns `DeluxeCoop`/`DeluxeBarn` for them (else `null`). All SVE ids stay in the profile (NFR-SVE-07).
- `Dayswork/Compat/ExpansionCompatService.cs` — `ResolveAnimalFeedCapacity` now bounds capacity by the building's real `ParentBuilding.maxOccupants` (falls back to trough count when unavailable). Added `TryResolvePremiumBuildingTier(string buildingType, out AnimalBuildingTier)` for the hiring enumeration (which carries only the type string).
- `Dayswork/Orchestration/AnimalTaskHandler.cs` — `CreateFeedWork` derives capacity via `ModEntry.ExpansionCompat.ResolveAnimalFeedCapacity(animalHouse)`, falling back to the legacy `FeedCapacity` ladder when compat is unavailable (unit tests). Auto-feed gate and pet/collect unchanged.
- `Dayswork/UI/LegacyScopeBootstrapper.cs` — `TryClassify` consults `TryResolvePremiumBuildingTier(outline.DisplayName)` before the vanilla substring inference; vanilla buildings get no mapping and fall through unchanged.

## Files created
- `Dayswork.Tests/Compat/PremiumBuildingTierTests.cs` — xUnit (PremiumCoop→DeluxeCoop, PremiumBarn→DeluxeBarn; non-premium → null) + FsCheck (determinism; maps *only* the two premium ids; Vanilla never maps a premium tier).

## Pet / Collect (unchanged, by design)
No auto-petter/auto-grabber detection. `ShouldPet` (`!wasPet`) and `HasToolHarvestReady` (`currentProduce`) naturally skip animals a machine already serviced (BR-SVE3-09 / requirement Q4=A).

## Vanilla invariance
Vanilla (null-object) profile → `MapPremiumBuildingTier` returns `null` (tier inference unchanged; `LegacyScopeBootstrapper` falls through). `ResolveAnimalFeedCapacity` over a vanilla building equals the legacy ladder (capacity policy parity covered by `AnimalBuildingCapacityPolicyTests`, incl. the 4/12 and 16-occupant cases). No vanilla behavior change.

## Verification
- `dotnet build Dayswork.sln /p:EnableModDeploy=false` → **0 warnings / 0 errors**.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` → **355 passed / 1 expected skip / 0 failed** (was 343; +12 from the new premium-tier tests).
- Deploy-enabled build deferred — Stardew is still running (DLL lock); the change is ready for the next launch.

## Story coverage
- **S-23** (premium barn/coop service): premium buildings now price/scope as Deluxe and feed by real trough/occupant data; auto-feed honored; auto-machines naturally skipped.

## Playtest fixes (2026-05-30)
Three bugs reported during U-SVE-03 review:
- **Bug 2 — worker stuck in a continuous loop** (`[outdoor-animals] pre-completion rescan picked up 1 new tile item(s); batch continues` repeating). **FIXED.** `ShiftOrchestrator.TryRescanOutdoorAnimalProductsBeforeBatchComplete` re-detected a forage tile every completion cycle once `ClearRemainingActiveBatchWork` dropped it from the work queues (when a detected forage item is unreachable or not removable). Added a per-batch guard (`_rescanBatchIndex` + `_rescanEnqueuedTiles`) so each tile is enqueued at most once per batch; genuinely new forage at fresh tiles still flows.
- **Bug 3 — normal + premium coop/barn: worker services only one set.** **FIXED.** The building-select diagnostic confirmed the draft correctly held all four (`Coop:Coop; Barn:Barn; …PremiumCoop:DeluxeCoop; …PremiumBarn:DeluxeBarn`), but the shift-plan resolved them to **premium coop ×2 + premium barn ×2** (base Coop/Barn never serviced). Root cause: `BuildingLocationResolver.Matches` fell back to `LooseBuildingTypeMatch` (`requestedName.Contains(buildingType) || buildingType.Contains(requestedName)`), so `"…PremiumCoop".Contains("Coop")` made the base-`Coop` selection loose-match the **premium** building; `TryResolve` returned the first match (premium enumerates first), collapsing two distinct selections onto one building. (This is the latent multi-building bug the player suspected — it also affected vanilla, since `"Coop"` ⊂ `"Big Coop"`/`"Deluxe Coop"`.) Fix: `TryResolve` now does an **exact-name pass across all buildings first** (`interior.Name` / indoors name / `buildingType`), only falling back to loose matching when nothing matches exactly. Added pure `SelectBuildingIndex` + `BuildingLocationResolverTests` (SVE base+premium scenario, vanilla Coop/Big/Deluxe precedence, loose fallback, no-match). The `[Dayswork][building-select]` diagnostic logging was kept (useful during SVE bring-up).
  - **Known remaining limitation**: two buildings of the *identical* type/interior-name (e.g., two base Coops) still share a selection `LocationName` and collapse under `Distinct()`; servicing multiple identical-type buildings needs unique interior keying (separate follow-up, not hit by the base+premium case).
- **Bug 1 — worker ignores goose egg / rabbit wool / camel wool.** **DEFERRED to U-SVE-04** (TODO-07). Root cause: hardcoded `WorkAreaScanner.AnimalProductObjectIds` whitelist missing Wool(440) + SVE custom products; to be fixed data-driven (category-based + source-verified SVE IDs) in U-SVE-04's content-classification work. No loop now thanks to the Bug 2 guard.

Verification after fixes: build **0/0**; tests **364 passed / 1 expected skip / 0 failed**. Deploy still deferred (game running).

## Manual playtest (to confirm in-game)
1. On an SVE save, build/own a Premium Coop or Barn; open the hire screen → the premium building should appear and price as its **Deluxe** counterpart.
2. With animals inside and silo hay available, confirm the worker does **not** manually feed (premium auto-feeds), and that Pet/Collect find nothing already handled by the default auto-petter/auto-grabber.
3. (Non-auto-feed expansion building, if any) confirm all real troughs get filled rather than a fixed 4/8/12.
