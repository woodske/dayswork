# Logical Components — U-SVE-04 New Content + Grandpa's Shed

NFR responsibilities mapped onto **existing** components. No new components; no new infrastructure.

| Component | Layer | NFR responsibility (U-SVE-04) | Patterns |
|---|---|---|---|
| `SveExpansionProfile` (Dayswork.Core/Compat) | Pure Core | Sole home for the animal-product category set (if profile-held), content-override table (verified gaps), and expansion work-location set (Grandpa's Shed); deterministic O(1) lookups. | P-SVE4-01..03 |
| `VanillaExpansionProfile` (Dayswork.Core/Compat) | Pure Core | Null-object: empty override table + empty work-location set; preserves vanilla path. | P-SVE4-03, P-SVE4-05 |
| `ContentDescriptor` / `WorkClassification` (Dayswork.Core/Compat) | Pure Core | Content-agnostic descriptor + override result; extended only if a verified gap needs it. | P-SVE4-02 |
| `WorkAreaScanner` (Dayswork/Orchestration) | Mod adapter | Category-based product detection in the existing scan; O(1) per object, never throws. | P-SVE4-01 |
| `ShiftOrchestrator` (Dayswork/Orchestration) | Mod runtime | `InvokeCollectAnimalProduct` shares the category predicate; existing rescan guard retained. | P-SVE4-01 |
| `ObjectTargetClassifier` (Dayswork/Worker) | Mod adapter | Consult `TryClassifyContentOverride` before vanilla classify/skip. | P-SVE4-02 |
| `BuildingWorkNavigator`, `IndoorWorkScanner`, `BuildingLocationResolver`, `ChestResolver` (Dayswork) | Mod | Include `IsExpansionWorkLocation` locations (Grandpa's Shed); unique-name building keying + backward-compatible resolution. | P-SVE4-03, P-SVE4-04 |
| `Dayswork.Tests/Compat/*` (+ scanner/resolver tests) | Tests | FsCheck: category totality/parity, override determinism, work-location membership, unique keys, vanilla invariance. | P-SVE4-05 |

## Data / control flow (unchanged shape)
```
scan → WorkAreaScanner.IsAnimalProductForageObject (category test) → collectible?
            └─ ShiftOrchestrator.InvokeCollectAnimalProduct (same predicate)
classify → ObjectTargetClassifier → ExpansionCompatService.TryClassifyContentOverride
            └─ SveExpansionProfile content-override table  (else vanilla classify)
work-location → ExpansionCompatService.IsExpansionWorkLocation → navigators/scope/chest
building select → BuildingOutline(unique name) → AnimalBuildingSelection → BuildingLocationResolver (exact-first)
```

## Infrastructure
None. No deployment, storage, or cloud resource changes (Infrastructure Design skipped for the SVE change per the execution plan).
