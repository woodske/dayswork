# NFR Design Patterns — U-SVE-04 New Content + Grandpa's Shed

Patterns realizing the approved NFR requirements (NFRU4-01..08) on existing component seams. No new infrastructure.

## P-SVE4-01 — Category-based product predicate (shared detect/collect, throw-free)
A pure, deterministic predicate classifies a ground object as a collectible animal product by item `Category` (Egg -5, Animal Goods -18, Truffle/forage), replacing the ID whitelist. Detection (`WorkAreaScanner.IsAnimalProductForageObject`) and collection (`ShiftOrchestrator.InvokeCollectAnimalProduct`) call the **same** predicate, guaranteeing every detected item is removable (kills the Bug-2 detect-but-can't-remove class). A guarded category read degrades to "not a product" rather than throwing.
- Realizes: NFRU4-01, NFRU4-03, NFRU4-04.

## P-SVE4-02 — Content-override strategy table, classify-before-vanilla
`ObjectTargetClassifier` builds a `ContentDescriptor` and consults `ExpansionCompatService.TryClassifyContentOverride` (O(1) profile lookup) **before** the vanilla classification/skip; a non-`None` result wins, `None` falls through unchanged. The SVE table is populated only for verified custom clumps/trees (else empty → passthrough).
- Realizes: NFRU4-02, NFRU4-04, NFRU4-07.

## P-SVE4-03 — Expansion work-location membership set (Null-Object vanilla)
`IsExpansionWorkLocation` is an O(1) membership test over a profile-held set (the plantable Grandpa's Shed greenhouse). The navigators / scope / chest resolution consume it to enter, scan (crop work), and deposit. The Vanilla profile returns an empty set ⇒ no extra locations, vanilla unchanged.
- Realizes: NFRU4-02, NFRU4-06.

## P-SVE4-04 — Unique-name building keying with backward-compatible fallback
Building selections key on the interior's unique name (`NameOrUniqueName`) threaded `BuildingOutline → AnimalBuildingSelection → resolver`, so identical-type buildings are distinct. The U-SVE-03 exact-then-loose `BuildingLocationResolver` resolves both new unique-name keys and legacy type-name keys, so existing saved contracts stay valid without a schema change.
- Realizes: NFRU4-03 (no migration risk), NFRU4-04.

## P-SVE4-05 — Pure-Core seams + thin adapter + FsCheck seam (vanilla invariance)
Category membership, content-override, and work-location logic live in pure `Dayswork.Core` and are FsCheck-tested without SMAPI. The Mod adapters (`WorkAreaScanner`, `ObjectTargetClassifier`, navigators, resolver, chest resolution) are the only SMAPI-touching parts, validated by playtest. Under the Vanilla profile every override/work-location lookup is false/None and detection equals legacy vanilla coverage.
- Realizes: NFRU4-05, NFRU4-06, NFRU4-08.

## Extension compliance
| Extension | Status | Compliance |
|---|---|---|
| Security Baseline | Disabled | N/A |
| Property-Based Testing | Enabled, full | Compliant — P-SVE4-01/02/04/05 carry the FsCheck obligations (category totality/parity, override determinism, unique keys, vanilla invariance) into Code Generation. |
