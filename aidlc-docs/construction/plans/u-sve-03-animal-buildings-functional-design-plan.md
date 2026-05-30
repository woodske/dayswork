# U-SVE-03 — SVE Animal Buildings — Functional Design Plan

**Unit**: U-SVE-03 (SVE Animal Buildings) · **Story**: S-23 · **Stage**: Construction → Functional Design (Part 1: planning)

## Purpose (from unit-of-work)
Service SVE Premium Barn/Coop correctly: data-driven feeding capacity and premium → nearest-vanilla-tier mapping for scope/pricing, with **no** auto-petter/auto-grabber special-casing, and **no** enum/save-schema change (App Design Q4=A).

## Grounded source findings (verified before drafting — per the "never assume" constraint)
1. **Premium building types**: `FlashShifter.StardewValleyExpandedCP_PremiumCoop` and `FlashShifter.StardewValleyExpandedCP_PremiumBarn` (SVE `Buildings.json`). Both `MaxOccupants: 16`. `BuildCondition` requires the matching **Deluxe** building already constructed → premium is an upgrade tier *above* Deluxe.
2. **Premium buildings auto-feed**: both `PremiumCoop.tmx` and `PremiumBarn.tmx` set the map property **`AutoFeed = T`** (exactly like vanilla Deluxe Coop/Barn). Dayswork's existing `AnimalTaskHandler.IsAutoFeedBuilding` checks this map property, so the worker **already skips feeding** these buildings. ⇒ The pre-source "16-animal underfeed" premise does **not** apply to these specific SVE buildings (the game auto-fills their troughs).
3. **Both premium maps carry `Trough` tiles** and a `Feed`/hopper area (auto-feed still uses trough tiles internally).
4. **Hiring scope enumeration**: `ChestResolver.GetBuildingOutlines` builds `BuildingOutline(indoors.Name, bounds, building.buildingType.Value)` — the **DisplayName field carries the raw `buildingType`** string. `LegacyScopeBootstrapper.TryInferAnimalBuildingSelection` then matches substrings: `…_PremiumCoop` contains `"Coop"` (but no Deluxe/`Coop3` marker) ⇒ classifies as the **base `Coop` tier** today (premium is currently **underpriced**). Same for barn.
5. **Compat seams already exist** (U-SVE-01): `ExpansionCompatService.ResolveAnimalFeedCapacity(AnimalHouse)` (trough-count → `AnimalBuildingCapacityPolicy.DeriveCapacity`, clamped to MaxOccupants) and `ExpansionCompatService.ResolveAnimalBuildingTier(Building, vanillaTier)` (→ `IExpansionProfile.MapPremiumBuildingTier`, currently returns null for all). The override tables are empty pending this unit.

---

## Questions (answer each with one of the lettered options; edit the `[Answer]:` line)

### Q1 — Premium feeding behavior (the reframing question)
Source shows SVE Premium Coop/Barn set `AutoFeed = T`, so the game auto-fills their troughs and the worker's existing auto-feed check already skips manual feeding — matching vanilla Deluxe. How should U-SVE-03 treat feeding?

- **A. (Recommended)** Keep auto-feed buildings skipped (no manual feed work for SVE premium, matching vanilla Deluxe), **and** still replace the hardcoded `FeedCapacity` ladder (Deluxe=12/Big=8/else=4) with the data-driven `ExpansionCompatService.ResolveAnimalFeedCapacity` trough-count policy. This removes the hardcoded assumption and makes any *non*-auto-feed expansion animal building (now or future) feed all of its real troughs, while leaving premium behavior correct and vanilla-identical.
- **B.** Force the worker to manually feed premium troughs anyway (ignore the `AutoFeed` property for SVE buildings). Risks double-feeding / wasted hay and diverges from vanilla Deluxe semantics.
- **C.** Leave feeding entirely as-is (don't replace the hardcoded ladder); only do the tier-mapping work in this unit.

[Answer]: A

### Q2 — Premium → vanilla tier mapping target (scope & pricing)
Premium buildings are an upgrade above Deluxe (16 occupants). The unit must price/scope them as their **nearest vanilla tier** with no new enum value (Q4=A). Which mapping?

- **A. (Recommended)** `…_PremiumCoop → AnimalBuildingTier.DeluxeCoop`, `…_PremiumBarn → AnimalBuildingTier.DeluxeBarn` (nearest existing vanilla tier; premium sits just above Deluxe). Encoded in `SveExpansionProfile.MapPremiumBuildingTier`, keyed on the exact `buildingType` strings.
- **B.** Map premium to the base `Coop`/`Barn` tier (current accidental behavior). Underprices the largest buildings — not recommended.
- **C.** Add new `PremiumCoop`/`PremiumBarn` enum values. **Rejected** by App Design Q4=A (changes the save schema and pricing table shape).

[Answer]: A

### Q3 — Where to apply the tier override (integration seam)
`LegacyScopeBootstrapper.TryInferAnimalBuildingSelection` infers tier from the outline's name/`buildingType` substrings. How should the premium override be injected so it wins over the substring heuristic?

- **A. (Recommended)** In the enumeration/classification path, consult `SveExpansionProfile.MapPremiumBuildingTier(buildingType)` **first** (via the existing `ExpansionCompatService.ResolveAnimalBuildingTier` seam, keyed on the raw `buildingType` already carried by `BuildingOutline`); if it returns a tier, use it; otherwise fall through to the current vanilla substring inference unchanged. Vanilla buildings get `null` → byte-for-byte identical behavior.
- **B.** Add `"Premium"` substring branches directly inside `LegacyScopeBootstrapper` (no profile involvement). Spreads SVE identifiers outside the profile, violating the "all SVE ids live in `SveExpansionProfile`" invariant (NFR-SVE-07).
- **C.** Resolve the live `Building` again at classification time and pass it to `ResolveAnimalBuildingTier(Building, …)`. Heavier; only needed if the buildingType weren't already on the outline (it is, per finding #4).

[Answer]: A

### Q4 — Feed-capacity upper bound (data-driven policy inputs)
`AnimalBuildingCapacityPolicy.DeriveCapacity` = `min(troughTileCount, maxOccupants)`. When wiring `ResolveAnimalFeedCapacity` to real building data, what feeds the `MaxOccupants` bound?

- **A. (Recommended)** Use the live building's real max occupants (`AnimalHouse.animalLimit` / building data `MaxOccupants`, = 16 for premium), and count `Trough`-property tiles from the map. Capacity = `min(troughTiles, maxOccupants)`. Fully data-driven; matches vanilla for vanilla buildings (verify parity at code-gen).
- **B.** Use trough count alone (ignore MaxOccupants). Simpler but could over-fill if a map has more trough tiles than the building allows occupants.
- **C.** Keep MaxOccupants as a fixed 16 for premium only. Re-introduces a hardcoded SVE assumption — not recommended.

[Answer]: A

---

## Answers (recorded)
Q1=A · Q2=A · Q3=A · Q4=A (user: "continue", 2026-05-30). No vague/ambiguous responses → no clarification round needed.

## Planned functional-design artifacts (Part 2) — generated
- [x] `construction/u-sve-03-animal-buildings/functional-design/business-logic-model.md` — feed-capacity derivation flow (auto-feed gate → trough-count capacity), premium tier-resolution flow, pet/collect natural-skip flow.
- [x] `construction/u-sve-03-animal-buildings/functional-design/business-rules.md` — BR-SVE3-01..09 + PBT property table (capacity determinism/clamping; tier-map totality; vanilla pass-through; auto-feed skip).
- [x] `construction/u-sve-03-animal-buildings/functional-design/domain-entities.md` — capacity inputs, premium building-type → tier entries, auto-feed predicate inputs.

## Plan checkboxes
- [x] Step 1 — Analyze unit context (unit-of-work U-SVE-03, story S-23, App Design seams)
- [x] Step 2 — Create functional design plan
- [x] Step 3 — Generate source-grounded questions (Q1–Q4)
- [x] Step 4 — Store plan
- [x] Step 5 — Collect & analyze answers (Q1–Q4 = A; no ambiguity)
- [x] Step 6 — Generate functional-design artifacts
- [x] Step 7 — Present completion message
- [ ] Step 8 — Await approval
- [ ] Step 9 — Record approval & update state
