# U-SVE-04 — Business Rules — New Content + Grandpa's Shed

**Unit**: U-SVE-04 · **Stories**: S-24, S-25 · **Decisions**: Q1=A..Q5=A

## Business rules

### Animal-product ground pickup (TODO-07 / S-24)
- **BR-SVE4-01** — A ground `Object` is a collectible animal product iff its item `Category` is in the animal-product set { Egg `-5`, Animal Goods `-18`, Forage/Truffle category }. This replaces the hardcoded ID whitelist.
- **BR-SVE4-02** — Detection (`IsAnimalProductForageObject`) and collection (`InvokeCollectAnimalProduct`) use the **same** predicate, so every detected product is removable (no detect-but-cannot-remove rescan loop).
- **BR-SVE4-03** — Category detection must preserve the prior whitelist's vanilla coverage (Egg/Wool 440/Duck Feather/Rabbit's Foot/Truffle) — asserted by example at code-gen — and additionally cover SVE Goose Egg (`-5`), Camel Wool (`-18`), Golden Goose Egg (`-5`).
- **BR-SVE4-04** — A narrow explicit **exclude** list is permitted only for a verified false positive (a category sweeping in a non-product placed object); default is empty.

### Content classification overrides (S-24 verified gaps)
- **BR-SVE4-05** — `ObjectTargetClassifier` consults `ExpansionCompatService.TryClassifyContentOverride(descriptor)` **before** returning a vanilla classification or skipping; a non-`None` override wins, `None` falls through unchanged.
- **BR-SVE4-06** — The SVE content-override table is populated **only** for clumps/trees verified custom in SVE source; with no verified gaps it is empty (pure passthrough). Generic SVE crops/fruit-trees/trees use the unchanged data-driven paths.
- **BR-SVE4-07** — Unclassifiable content is skipped without crashing; no item loss (overflow-to-mail preserved).
- **BR-SVE4-08** — `IsMilkProduce`/`IsShearProduce` are unchanged (no SVE tool-harvest animal); Goose/Camel collect via the ground-pickup path.

### Grandpa's Shed work location (S-25)
- **BR-SVE4-09** — `IsExpansionWorkLocation` returns true for the plantable Grandpa's Shed greenhouse; it is enterable via its warp/arrival tile and is a valid chest-deposit destination, serviced as an indoor **crop-work** location (Water/Harvest).
- **BR-SVE4-10** — Exact Grandpa's Shed location id(s) and any additional serviced sub-locations are confirmed from SVE source at code-gen; default is crop work on the plantable greenhouse only.

### Identical-type buildings (TODO-08)
- **BR-SVE4-11** — Animal-building selections are keyed by the interior's unique name (`NameOrUniqueName`), so two same-type buildings are distinct selections and each is serviced. Existing saved contracts (type-name keys) still resolve via the exact-then-loose resolver fallback (backward compatible); no save-schema change.

### Vanilla invariance
- **BR-SVE4-12** — With the Vanilla null-object profile: content-override table empty, `IsExpansionWorkLocation` false, no Grandpa's Shed; category detection equals prior vanilla product coverage. Byte-for-byte vanilla behavior.

## Property-based test (PBT) table — FsCheck (full mode, blocking)

| Property | Statement |
|---|---|
| **P1 — category detection totality** | For any object, `IsAnimalProductForageObject` returns a deterministic bool and never throws; true exactly when category ∈ the animal-product set and not in the exclude list. |
| **P2 — detect/collect consistency** | Any object the scanner detects as a product is removable by collection (shared predicate) — no detect-but-not-collectible case. |
| **P3 — vanilla product parity** | Every id in the legacy whitelist (Egg/Wool/Truffle/etc.) is still detected under category rules. |
| **P4 — override passthrough/determinism** | `TryClassifyContentOverride` is deterministic; returns non-`None` only for table entries; `None` ⇒ caller behaves exactly as before. |
| **P5 — work-location membership** | `IsExpansionWorkLocation` is deterministic and true only for configured expansion work locations; false for all vanilla locations. |
| **P6 — unique building keys** | Distinct building interiors yield distinct selection keys; `SelectBuildingIndex` maps each unique key to its own building (extends U-SVE-03 resolver tests). |
| **P7 — vanilla invariance** | Under the Vanilla profile, all override/work-location lookups are false/None and detection equals legacy vanilla coverage. |

## Traceability
- S-24 → BR-SVE4-01..08, P1–P4, P7. S-25 → BR-SVE4-09/10, P5. TODO-07 → BR-SVE4-01..04. TODO-08 → BR-SVE4-11, P6. Q5=A (data-driven/verified gaps), Q6=A (Grandpa's Shed work location), Q4=A (no auto-machine special-casing) honored.
