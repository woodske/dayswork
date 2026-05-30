# U-SVE-03 — SVE Animal Buildings — Code Generation Plan

**Unit**: U-SVE-03 · **Story**: S-23 · **Decisions**: Q1=A..Q4=A · **Patterns**: P-SVE3-01..05

Two data-driven changes wired onto existing U-SVE-01 seams: data-driven feed capacity, and Premium→Deluxe tier mapping consulted ahead of vanilla inference. No new components, no enum/save-schema change, no auto-machine special-casing. Vanilla path stays identical via the null-object profile.

## Steps

- [x] **Step 1 — Premium ids + tier table (`SveExpansionProfile`).**
  Add `const string PremiumCoopBuildingType = "FlashShifter.StardewValleyExpandedCP_PremiumCoop"` and `PremiumBarnBuildingType = "...CP_PremiumBarn"`. Populate `MapPremiumBuildingTier` to return `DeluxeCoop`/`DeluxeBarn` for these (else `null`). Keeps every SVE id inside the profile (NFR-SVE-07 / BR-SVE3-08).

- [x] **Step 2 — Capacity upper bound (`ExpansionCompatService.ResolveAnimalFeedCapacity`).**
  Read the building's real max occupants from `house.ParentBuilding?.maxOccupants.Value` (verify exact member at code-gen; = 16 for SVE premium) and pass it as the `MaxOccupants` input instead of the U-SVE-01 placeholder (troughs used as both args). Capacity = `DeriveCapacity(troughTiles, maxOccupants)`. Guarded so missing data degrades safely (NFRU3-03).

- [x] **Step 3 — String-keyed premium tier accessor (`ExpansionCompatService`).**
  Add `bool TryResolvePremiumBuildingTier(string buildingType, out AnimalBuildingTier tier)` delegating to `_activeProfile.MapPremiumBuildingTier`. Lets the hiring enumeration (which only carries the `buildingType` string on `BuildingOutline`) consult the profile without re-resolving a live `Building`, keeping SVE ids in the profile (Q3=A / P-SVE3-03).

- [x] **Step 4 — Feed capacity consumer (`AnimalTaskHandler.CreateFeedWork`).**
  Replace `var capacity = FeedCapacity(parent?.buildingType.Value);` with `ModEntry.ExpansionCompat?.ResolveAnimalFeedCapacity(animalHouse) ?? FeedCapacity(parent?.buildingType.Value)` (fallback keeps unit-test/no-compat paths working). Auto-feed gate and pet/collect remain untouched. Remove the now-dead `FeedCapacity` ladder only if no fallback references remain (otherwise keep as the safe fallback and mark it legacy).

- [x] **Step 5 — Tier mapping consumer (`LegacyScopeBootstrapper.TryClassify`).**
  Before the vanilla substring inference, consult `ModEntry.ExpansionCompat?.TryResolvePremiumBuildingTier(outline.DisplayName, out var premiumTier)` (DisplayName carries `buildingType`); on success return `new AnimalBuildingSelection(outline.LocationName, premiumTier)`. On null/absent compat, fall through to the existing inference unchanged (vanilla invariance).

- [x] **Step 6 — Tests (`Dayswork.Tests/Compat` + animal handler).**
  Added `PremiumBuildingTierTests` (premium positive cases + FsCheck totality/determinism/pass-through/vanilla-never-maps). Capacity `min(troughs, MaxOccupants)` incl. 16-occupant + clamp cases already covered by `AnimalBuildingCapacityPolicyTests`; existing `Premium_tier_mapping_is_null("Barn")` no-op test kept (still valid).
  Add: premium tier-map positive cases (PremiumCoop→DeluxeCoop, PremiumBarn→DeluxeBarn) + FsCheck totality/determinism and **pass-through** (non-premium → vanilla tier unchanged); capacity `min(troughs, maxOccupants)` example incl. a 16-occupant case and vanilla-parity examples per tier. Keep the existing `Premium_tier_mapping_is_null("Barn")` no-op test (still valid — "Barn" isn't a premium id).

- [x] **Step 7 — Verify.**
  `dotnet build Dayswork.sln /p:EnableModDeploy=false` → **0/0**; `dotnet test Dayswork.sln /p:EnableModDeploy=false` → **355 passed / 1 expected skip / 0 failed** (was 343). Deploy-enabled build deferred until the game is closed.

- [x] **Step 8 — Document + state.**
  Wrote `construction/u-sve-03-animal-buildings/code/code-summary.md`; ticked these checkboxes; updated `aidlc-state.md` and `audit.md`.

## Vanilla invariance guard
With the Vanilla profile: Step 1 returns `null` (tier inference unchanged), Step 2's capacity equals the legacy ladder for vanilla buildings (parity asserted in Step 6), Steps 4/5 fall through to existing logic. No vanilla behavior change.

## Out of scope
Auto-petter/auto-grabber detection (BR-SVE3-09 scan-and-skip), new content classification + Grandpa's Shed (U-SVE-04).
