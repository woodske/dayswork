# U-SVE-04 — Business Logic Model — New Content + Grandpa's Shed

**Unit**: U-SVE-04 (final SVE unit) · **Stories**: S-24, S-25 · **Decisions**: Q1=A..Q5=A · **Folded-in**: TODO-07, TODO-08

## Scope
Make SVE's new content work through Dayswork's existing data-driven paths, adding overrides only at **verified gaps**, plus Grandpa's Shed as a work location and unique keying for identical-type buildings. No new components; populate/extend existing U-SVE-01 seams. Vanilla stays byte-for-byte identical via the null-object profile; unclassifiable content is skipped without crashing and item-safety (overflow-to-mail) is preserved.

## Source-grounded premises (verified in SVE repo)
- Goose Egg / Golden Goose Egg = `Category -5` (Egg); Camel Wool = `Category -18` (animal goods, same as vanilla Wool `440`). Goose & Camel are `HarvestType: DropOvernight` (ground products).
- `Custom_GrandpasShedGreenhouse` has `CanPlantHere: true`; Grandpa's Shed also has an interior and `Custom_GrandpasShedOutside`.

---

## Flow 1 — Animal-product ground pickup (category-based; TODO-07)
Replaces the hardcoded `WorkAreaScanner.AnimalProductObjectIds` ID whitelist.

```
Detection (WorkAreaScanner.IsAnimalProductForageObject, used by ClassifyTile + the rescan):
  a ground Object is a collectible animal product when its item Category is in the
  animal-product set: { Egg(-5), AnimalGoods(-18), Forage/Truffle(<truffle category>) }.
  → naturally covers vanilla Egg/Wool(440)/Duck Feather/Rabbit's Foot/Truffle AND
    SVE Goose Egg(-5) / Camel Wool(-18) / Golden Goose Egg(-5) AND future products.

Collection (ShiftOrchestrator.InvokeCollectAnimalProduct):
  shares the SAME predicate, so a detected product is always removable → no Bug-2-style
  detect-but-cannot-remove loop. Buffer the item (Stack ≥ 1) and remove the object.
```
- Decision Q1=A. The exact category constant set (incl. Truffle's category) is confirmed at code-gen; a tiny explicit **exclude** list is added only if a category sweeps in a non-product placed object. Determinism + totality preserved.
- The Bug-2 rescan guard from U-SVE-03 remains; with detection fixed, these products are now collected rather than skipped.

## Flow 2 — Tool-harvest milk/wool (no change; verified)
`IsMilkProduce` (Cow/Goat) / `IsShearProduce` (Sheep) gate the milk-pail/shears path. SVE's new animals (Goose, Camel) are DropOvernight ground products (Flow 1), not tool-harvested, so these predicates are unchanged.
- Decision Q2=A. Re-verify at code-gen that no SVE animal uses a milk/shear `HarvestType`.

## Flow 3 — Custom clumps / special trees (content-override seam; verified gaps only)
```
ObjectTargetClassifier.ClassifyAxe / ClassifyPick:
  1. Build a ContentDescriptor for the live clump/tree/object (Kind + Identifier, e.g.
     ResourceClump sheet index or tree type).
  2. Consult ExpansionCompatService.TryClassifyContentOverride(descriptor):
       override Axe/Pick → use it; None → fall through to the existing vanilla classification.
  3. Unknown/unclassifiable → existing skip (no crash).
```
- Decision Q3=A. `SveExpansionProfile`'s content-override table is populated **only** for clumps/trees confirmed custom in SVE source at code-gen. If none exist, the table stays empty → pure passthrough (still correct). Generic SVE crops/fruit-trees/trees already flow through the unchanged `HoeDirt`/`FruitTree`/`Tree` data-driven paths.

## Flow 4 — Grandpa's Shed work location (S-25)
```
Work-location membership: ExpansionCompatService.IsExpansionWorkLocation(location)
  → SveExpansionProfile.IsExpansionWorkLocation(NameOrUniqueName)
       true for the plantable Grandpa's Shed greenhouse (Custom_GrandpasShedGreenhouse).
Navigation: the building navigators / scope include expansion work locations, entered via the
  location's warp / DefaultArrivalTile; treated as an indoor CROP-work location (Water/Harvest
  crops, like the vanilla greenhouse).
Deposit: it is a valid chest-deposit destination (BuildingLocationResolver / chest resolution
  include expansion work locations).
```
- Decision Q4=A: default to the plantable shed greenhouse as crop-work only; confirm exact location id(s) and whether the shed interior/outside also warrant work at code-gen.

## Flow 5 — Unique identical-type building keying (TODO-08)
```
BuildingOutline.LocationName is set from the interior's UNIQUE name (NameOrUniqueName) instead of
the type name, so two same-type buildings (two base Coops, two Premium Coops) get distinct
selections. This flows: BuildingOutline → AnimalBuildingSelection.LocationName → resolver/runtime.
The U-SVE-03 exact-first resolver matches the unique name; existing saved contracts (type-name
LocationName) still resolve via the exact-then-loose fallback (backward compatible).
```
- Decision Q5=A: reuse the `LocationName` field (no save-schema change). Friendly building name is still shown in the UI.

## Components touched (all pre-existing)
| Component | Change |
|---|---|
| `Dayswork/Orchestration/WorkAreaScanner.cs` | `IsAnimalProductForageObject` → category-based (Flow 1). |
| `Dayswork/Orchestration/ShiftOrchestrator.cs` | `InvokeCollectAnimalProduct` shares the category predicate (Flow 1). |
| `Dayswork.Core/Compat/SveExpansionProfile.cs` | Populate content-override table (verified gaps) + Grandpa's Shed work-location identity (Flows 3/4). |
| `Dayswork/Worker/ObjectTargetClassifier.cs` | Consult `TryClassifyContentOverride` before vanilla classify (Flow 3). |
| `Dayswork/Orchestration/BuildingWorkNavigator.cs`, `IndoorWorkScanner.cs`, `Integration/BuildingLocationResolver.cs`, chest resolution | Include `IsExpansionWorkLocation` locations (Flow 4). |
| `Dayswork/Integration/ChestResolver.cs` (GetBuildingOutlines) + `AnimalBuildingSelection` consumers | Unique-name keying (Flow 5). |
| `Dayswork.Tests/Compat/*` (+ scanner/resolver tests) | Category detection, override passthrough, work-location membership, unique keying, vanilla parity. |

## Vanilla invariance & safety
Vanilla profile → content-override table empty (passthrough), `IsExpansionWorkLocation` false, no Grandpa's Shed; category detection covers the same vanilla products the whitelist did (parity asserted at code-gen). Unclassifiable content is skipped; no item loss (overflow-to-mail preserved).
