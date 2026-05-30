# U-SVE-03 — Business Logic Model — SVE Animal Buildings

**Unit**: U-SVE-03 · **Story**: S-23 · **Decisions**: Q1=A, Q2=A, Q3=A, Q4=A

## Scope
Make Dayswork service SVE Premium Coop/Barn correctly along two axes — **feed-capacity derivation** and **scope/pricing tier resolution** — by populating the U-SVE-01 compat seams (which currently no-op) and routing the two existing hardcoded sites through them. No new components, no enum/save-schema change, no auto-petter/auto-grabber special-casing. Vanilla farms resolve through a null/identity profile, so behavior stays byte-for-byte identical when SVE is absent.

## Source-grounded premises (verified in SVE repo)
- Building types: `FlashShifter.StardewValleyExpandedCP_PremiumCoop`, `FlashShifter.StardewValleyExpandedCP_PremiumBarn`; both `MaxOccupants = 16`; both are upgrades requiring the matching **Deluxe** building first.
- Both premium interior maps set `AutoFeed = T` and contain `Trough` tiles + a feed/hopper area.
- `BuildingOutline.DisplayName` already carries the raw `building.buildingType.Value`.

---

## Flow 1 — Feed work (capacity derivation)
Entry: `AnimalTaskHandler.CreateFeedWork(animalHouseLocation)`.

```
1. Not an AnimalHouse?                      → no feed work.
2. IsAutoFeedBuilding(buildingType, map)?   → no feed work.
   (Premium Coop/Barn hit this via the map "AutoFeed = T" property — the
    game auto-fills troughs, so the worker correctly does NOT manually feed.
    Identical to vanilla Deluxe. This gate is UNCHANGED.)
3. capacity := ExpansionCompatService.ResolveAnimalFeedCapacity(animalHouse)
   (REPLACES the hardcoded FeedCapacity ladder Deluxe=12/Big=8/else=4.)
     = AnimalBuildingCapacityPolicy.DeriveCapacity(
          troughTiles  = count of "Trough" map-property tiles,
          maxOccupants = building's real max occupants)
     = clamp(troughTiles, 0, maxOccupants)
4. filled := placed-hay count (clamped to capacity)
5. emptySlots := max(0, capacity - filled); 0 → no feed work
6. (unchanged) take hay from silo, resolve trough tiles + hopper, emit WorkItems
```

**Effect of the change**: For a *non*-auto-feed building whose real trough count exceeds the old ladder bucket, the worker now fills every real trough instead of a fixed 4/8/12. For vanilla buildings, the trough count equals the vanilla bucket (verified at code-gen), so capacity is unchanged. For SVE premium, step 2 already short-circuits, so the capacity change is inert there but removes the hardcoded assumption.

> Decision Q1=A: premium auto-feed is honored (skip), ladder is still generalized.
> Decision Q4=A: capacity = `min(troughTiles, MaxOccupants)`.

## Flow 2 — Scope/pricing tier resolution
Entry: hiring building enumeration → `LegacyScopeBootstrapper.TryInferAnimalBuildingSelection(name)` (name = `BuildingOutline.DisplayName` = raw `buildingType`).

```
1. expansionTier := ExpansionCompatService.ResolveAnimalBuildingTier(building, vanillaTierOrNull)
   → SveExpansionProfile.MapPremiumBuildingTier(buildingType):
        "...CP_PremiumCoop" → DeluxeCoop
        "...CP_PremiumBarn" → DeluxeBarn
        otherwise           → null
2. If a premium mapping applied → use that tier (DeluxeCoop / DeluxeBarn).
3. Else → existing vanilla substring inference, UNCHANGED
   (Coop3/Deluxe Coop→DeluxeCoop, Coop2/Big Coop→BigCoop, Coop→Coop, …barn…).
```

**Effect of the change**: SVE premium buildings — which today fall through to the *cheapest* `Coop`/`Barn` tier because their type string contains "Coop"/"Barn" but no Deluxe marker — now price and scope as **Deluxe**, their nearest vanilla tier. Vanilla buildings get `null` from the profile and follow the existing path exactly.

> Decision Q2=A: Premium → nearest vanilla Deluxe tier, no enum/save change.
> Decision Q3=A: profile-first via the existing seam, keyed on `buildingType`; all SVE ids stay inside `SveExpansionProfile`.

## Flow 3 — Pet / Collect on premium animals (no change)
Premium buildings ship a default auto-petter `(BC)272` and auto-grabber `(BC)165`. Dayswork does **not** detect these machines. The worker scans animals as usual:
- `ShouldPet(animal)` = `!animal.wasPet` → an already auto-petted animal is naturally skipped.
- `HasToolHarvestReady(animal)` = `currentProduce` non-empty → an already auto-grabbed animal yields nothing and is naturally skipped.

No machine-presence assumption, no special-casing. The player can relocate the machines and the worker adapts automatically.

> Requirement Q4=A refinement: scan and naturally skip; do not special-case auto-petter/auto-grabber.

---

## Components touched (all pre-existing)
| Component | Change |
|---|---|
| `Dayswork.Core/Compat/SveExpansionProfile.cs` | Populate `MapPremiumBuildingTier` for the two premium type strings (was `null`). |
| `Dayswork.Core/Compat/AnimalBuildingCapacityPolicy.cs` | No change (already `min(troughs, maxOccupants)`); now actually consumed. |
| `Dayswork/Compat/ExpansionCompatService.cs` | `ResolveAnimalFeedCapacity` wires the real `MaxOccupants` upper bound (U-SVE-01 passed troughs as both args). |
| `Dayswork/Orchestration/AnimalTaskHandler.cs` | Replace `FeedCapacity(buildingType)` ladder with `ResolveAnimalFeedCapacity`. Auto-feed gate + pet/collect unchanged. |
| hiring enumeration (`LegacyScopeBootstrapper.TryInferAnimalBuildingSelection`) | Consult `ResolveAnimalBuildingTier` first; vanilla path unchanged. |
| `Dayswork.Tests/Compat/*`, animal-handler tests | Add premium tier-map + capacity coverage incl. FsCheck properties. |

## Vanilla invariance
With the Vanilla (null-object) profile active: `MapPremiumBuildingTier` returns `null` for every building → tier inference is unchanged; `ResolveAnimalFeedCapacity` over a vanilla building yields the same capacity as the old ladder (parity asserted at code-gen). No vanilla behavior change.
