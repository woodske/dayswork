# U-SVE-04 — Domain Entities — New Content + Grandpa's Shed

**Unit**: U-SVE-04 · **Stories**: S-24, S-25 · **Decisions**: Q1=A..Q5=A

All entities below already exist (U-SVE-01 + base domain). U-SVE-04 **populates/extends** them; it introduces no new persisted schema (Q5=A reuses `LocationName`).

## Animal-product category set (Flow 1 / TODO-07) — data, not a type
The collectible-product test is parameterised by an item-`Category` set rather than an ID list:
- `Egg` = `-5` (vanilla Egg/Large Egg/Dino/Void/Golden; **SVE Goose Egg, Golden Goose Egg**)
- `AnimalGoods` = `-18` (vanilla Wool 440/Duck Feather/Rabbit's Foot; **SVE Camel Wool**)
- `Forage/Truffle` category (preserves vanilla Truffle 430 — exact constant confirmed at code-gen)
- Optional narrow **exclude** set (default empty) for a verified category false positive.

## ContentDescriptor (Dayswork.Core/Compat) — existing, now consumed
`(WorldContentKind Kind, string Identifier)`. Built by `ObjectTargetClassifier` for a live clump/tree/object:
- `ResourceClump` → `Identifier` = sheet index (string)
- `Tree` → `Identifier` = tree type
- `Object` → `Identifier` = item/qualified id
Passed to `TryClassifyContentOverride` (Flow 3).

## WorkClassification / WorkClassificationKind (Dayswork.Core/Compat) — existing
`Kind ∈ { None, Axe, Pick }`. `None` ⇒ caller falls through to vanilla classification. Extended only if a verified SVE gap needs a kind beyond Axe/Pick (none currently expected).

## SveExpansionProfile (Dayswork.Core/Compat) — tables populated
- **Content-override table**: `ContentDescriptor → WorkClassification`, populated only for verified custom clumps/trees (may stay empty → passthrough).
- **Expansion work-locations**: set of location unique-names for which `IsExpansionWorkLocation` returns true — includes the plantable Grandpa's Shed greenhouse (`Custom_GrandpasShedGreenhouse`; exact id confirmed at code-gen).
- `VanillaExpansionProfile` returns `NoOverride` / `false` for all (unchanged).

## Grandpa's Shed location identity (Flow 4 / S-25)
Keyed by `GameLocation.NameOrUniqueName`. Default serviced location: the plantable shed greenhouse (crop work). Interior / `Custom_GrandpasShedOutside` inclusion decided at code-gen.

## Building selection key (Flow 5 / TODO-08) — existing fields, new value
- `BuildingOutline.LocationName` and `AnimalBuildingSelection.LocationName` now carry the interior's **unique** name (`NameOrUniqueName`) instead of the type name, making same-type buildings distinct. No new field; `LocationName` semantics tightened. Backward-compatible: legacy type-name keys still resolve via the exact-then-loose `BuildingLocationResolver` fallback.

## Out of scope (no new entities)
- No auto-petter/auto-grabber entity (scan-and-skip; Q4=A).
- No new persisted save field (Q5=A reuses `LocationName`).
- No new `WorkClassificationKind` value unless a verified SVE clump/tree demands one.
