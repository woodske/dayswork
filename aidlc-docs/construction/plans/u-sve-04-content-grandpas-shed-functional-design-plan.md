# U-SVE-04 — New Content + Grandpa's Shed — Functional Design Plan

**Unit**: U-SVE-04 (final SVE unit) · **Stories**: S-24, S-25 · **Folded-in**: TODO-07 (animal-product pickup), TODO-08 (identical-type buildings) · **Stage**: Construction → Functional Design (Part 1: planning)

## Purpose (from unit-of-work)
Handle new SVE crops/trees/animals/products through the existing data-driven paths, add classification overrides only at **verified gaps**, treat Grandpa's Shed as a work location, and preserve graceful-skip + item-safety. Requirement decisions carried in: Q5=A (data-driven, verified gaps only), Q6=A (Grandpa's Shed = full work location), Q4=A (no auto-machine special-casing). Vanilla stays byte-for-byte identical via the null-object profile.

## Grounded source findings (verified before drafting)
1. **SVE ground animal products** (`Items/Objects.json`): **Goose Egg** & **Golden Goose Egg** = `Category -5` (Egg); **Camel Wool** = `Category -18` (animal goods — same category as vanilla **Wool (440)**). `FarmAnimals.json`: Goose and Camel both `HarvestType: DropOvernight` (ground drops, not tool-harvested). ⇒ The current hardcoded `WorkAreaScanner.AnimalProductObjectIds` whitelist (missing Wool 440, Goose Egg, Camel Wool) is the TODO-07 gap; a **category-based** detector covers vanilla + SVE + future products.
2. **No new tool-harvest animals**: SVE's new animals (Goose, Camel) drop overnight; none observed using a milk/shear tool-harvest type ⇒ `IsMilkProduce`/`IsShearProduce` likely need no SVE change (re-verify at code-gen).
3. **Grandpa's Shed** (`Locations/LocationsData.json`) is a multi-location complex: `Custom_GrandpasShedGreenhouse` (**`CanPlantHere: true`** → crops), the shed interior, and `Custom_GrandpasShedOutside` (artifact spots/forage). The plantable greenhouse is the natural crop-work target.
4. **Seams already exist** (U-SVE-01): `SveExpansionProfile.TryClassifyContentOverride` / `IsExpansionWorkLocation` (both currently return false/no-override) and `ExpansionCompatService` wrappers; `ContentDescriptor` (Kind + Identifier) and `WorkClassification` (None/Axe/Pick) are minimal and extensible.

---

## Questions (answer each with a lettered option; edit the `[Answer]:` line)

### Q1 — Animal-product ground pickup detection (TODO-07 / S-24)
The hardcoded `AnimalProductObjectIds` whitelist misses Wool(440) + SVE Goose Egg/Camel Wool, so the worker ignores them (and they previously fed the rescan loop, now guarded). How should ground animal-product detection work?

- **A. (Recommended)** Detect by **item category** — ground objects in the animal-product categories (Egg `-5`, animal goods `-18`, plus the forage/Truffle category already covered) are collectible, replacing the ID whitelist. Naturally covers vanilla Wool(440), SVE Goose Egg (`-5`) / Camel Wool (`-18`), and any future product. Both detection (`IsAnimalProductForageObject`) and collection (`InvokeCollectAnimalProduct`) share the predicate so they stay consistent. Exact category set (incl. Truffle's) verified at code-gen; keep a tiny explicit exclude-list only if a category sweeps in something that shouldn't be grabbed.
- **B.** Keep the whitelist, just add the three verified IDs (Wool 440 + the two SVE ids). Brittle; misses future/other-expansion content; against Q5=A.
- **C.** Route per-id through `SveExpansionProfile`'s content-override table. Still enumerated, SVE-only, and doesn't fix the vanilla Wool(440) gap.

[Answer]: A

### Q2 — Tool-harvest milk/wool animal types (S-24 verified gap)
`IsMilkProduce` (Cow/Goat) and `IsShearProduce` (Sheep) gate the *tool-harvest* (milk pail / shears) path. Source shows SVE's new animals (Goose, Camel) are **DropOvernight** ground products, not tool-harvested.

- **A. (Recommended)** No change to `IsMilkProduce`/`IsShearProduce` — SVE adds no new milkable/shearable (tool-harvest) animal; Goose/Camel are handled by the Q1 ground-pickup path. Re-verify at code-gen that no SVE animal uses a milk/shear harvest type.
- **B.** Make milk/shear classification data-driven now anyway (defensive), even though no SVE animal currently needs it.

[Answer]: A

### Q3 — Custom resource clumps / special trees (S-24 verified gap)
`ObjectTargetClassifier` hardcodes vanilla `ResourceClump` indices and tree types. Generic SVE crops/trees already flow through the data-driven `HoeDirt`/`FruitTree`/`Tree` paths.

- **A. (Recommended)** Consult `ExpansionCompatService.TryClassifyContentOverride(...)` in `ObjectTargetClassifier` **before** the vanilla classification/skip; populate the `SveExpansionProfile` content-override table **only for verified gaps** (custom clump sheet indices / special trees confirmed from SVE source at code-gen). If source review finds no custom clumps, the table stays empty (pure passthrough) — still correct, and unclassifiable content is skipped without crashing.
- **B.** Pre-emptively add overrides for guessed SVE clumps/trees now. Violates "never assume / verified gaps only."

[Answer]: A

### Q4 — Grandpa's Shed work-location scope & task set (S-25 / Q6=A)
Grandpa's Shed is a complex: `Custom_GrandpasShedGreenhouse` (CanPlantHere → crops), the shed interior, and `Custom_GrandpasShedOutside` (forage/artifact spots). What does the worker service?

- **A. (Recommended)** Treat the **plantable Grandpa's Shed greenhouse** as an indoor **crop-work** location (Water/Harvest crops, like the vanilla greenhouse): `IsExpansionWorkLocation` returns true for it, it's enterable via its warp/arrival tile, and is a valid chest-deposit destination. Confirm the exact location id(s) and whether the shed interior/outside also warrant work at code-gen; default to crop work only.
- **B.** Treat **all** Grandpa's Shed sub-locations (greenhouse + interior + outside) as general work locations (crops + clearing + forage). Broader; higher risk of servicing unintended areas.
- **C.** Defer Grandpa's Shed entirely (keep U-SVE-04 focused on content classification); revisit in a follow-up.

[Answer]: A

### Q5 — Multiple identical-type buildings (TODO-08)
Two buildings of the same type (two base Coops, or two Premium Coops) currently share a selection `LocationName` (= `indoors.Name` = the building type for animal houses), so `Distinct()` collapses them and only one is serviced. How to give each its own identity?

- **A. (Recommended)** Key animal-building selections by the interior's **unique name** (`NameOrUniqueName`) where it is actually unique, threaded `BuildingOutline → AnimalBuildingSelection.LocationName → resolver/runtime`. The `LocationName` field is reused (no schema change), and **existing saved contracts stay valid** via the exact-then-loose resolver fallback. Display still shows the friendly building name.
- **B.** Add a dedicated unique key (e.g., building tile coords) to `AnimalBuildingSelection` — most robust, but a **save-schema change** (new persisted field + migration).
- **C.** Defer TODO-08 to a later pass; U-SVE-04 ships content + Grandpa's Shed only.

[Answer]: A

---

## Answers (recorded)
Q1=A · Q2=A · Q3=A · Q4=A · Q5=A (user: "use recommended, continue", 2026-05-30). No ambiguity → no clarification round.

## Functional-design artifacts (Part 2) — generated
- [x] `construction/u-sve-04-content-grandpas-shed/functional-design/business-logic-model.md` — category-based product detection (Flow 1); content-override consultation in ObjectTargetClassifier (Flow 3); Grandpa's Shed work-location flow (Flow 4); unique building-keying flow (Flow 5); no-change milk/shear (Flow 2).
- [x] `construction/u-sve-04-content-grandpas-shed/functional-design/business-rules.md` — BR-SVE4-01..12 + PBT table P1–P7.
- [x] `construction/u-sve-04-content-grandpas-shed/functional-design/domain-entities.md` — animal-product category set, `ContentDescriptor`/`WorkClassification` usage, SveExpansionProfile tables, Grandpa's Shed identity, unique building-key.

## Plan checkboxes
- [x] Step 1 — Analyze unit context (unit-of-work U-SVE-04, stories S-24/S-25, TODO-07/08, App Design seams)
- [x] Step 2 — Create functional design plan
- [x] Step 3 — Generate source-grounded questions (Q1–Q5)
- [x] Step 4 — Store plan
- [x] Step 5 — Collect & analyze answers (Q1–Q5 = A; no ambiguity)
- [x] Step 6 — Generate functional-design artifacts
- [x] Step 7 — Present completion message
- [ ] Step 8 — Await approval
- [ ] Step 9 — Record approval & update state
