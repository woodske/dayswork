# NFR Design — Patterns — u-t09-animal-ordering

Category evaluation: Resilience, Scalability, Performance, Security, and Logical-Components were reviewed. No additional question round was needed — the approved NFR requirements + the existing pure-Core/thin-adapter architecture already fix the pattern set. Security and Scalability/Availability are N/A for this in-process, single-player re-ordering.

## Patterns

- **P-T09-01 — Pure ordering authority in Core.** The batch sequence (building order, per-building `AnimalBuilding`+`OutdoorAnimals` pairing, single trailing `FarmForage`) is decided entirely in the pure `ShiftPlanBuilder`. This keeps the hardest-to-get-right logic deterministic and FsCheck-testable with no game dependency. *(NFRU-T09-01, -04, -06)*

- **P-T09-02 — Thin runtime adapter / skeleton-fill.** `ShiftOrchestrator` keeps its existing skeleton-fill role: it does not decide ordering, only fills each skeleton's tile/animal work and routes. The new `FarmForage` and the narrowed `OutdoorAnimals` cases slot into the existing `BuildInitialBatches` switch and the `BeginCurrentBatch` refresh hook. *(NFRU-T09-06)*

- **P-T09-03 — Single-home partition, no extra scans.** A per-building `OutdoorAnimals` pass calls the existing `BuildAnimalWork(farm, { LocationName }, tasks)` with a **single-element** home set — partitioning the same grazing-animal enumeration rather than adding scans. The farm-forage whole-farm scan happens exactly once in the `FarmForage` pass (same scan the combined batch did). *(NFRU-T09-02)*

- **P-T09-04 — Idempotent re-service safety.** Reliance on existing idempotent pet/collect (`ShouldPet` ⇒ `wasPet`; collect clears `currentProduce`) guarantees the no-double-work invariant even under legacy shared type-name selection keys, with no new guard state. *(NFRU-T09-03)*

- **P-T09-05 — Forage rescan relocation.** The late-truffle pre-completion rescan (`TryRescanOutdoorAnimalProductsBeforeBatchComplete`) is re-pointed from `OutdoorAnimals` to `FarmForage` (the new owner of farm-wide forage), preserving truffle coverage without new logic. *(NFRU-T09-02, BR-T09-08)*

## Risk / fault handling
- Missing/unresolvable building interior on an `AnimalBuilding` batch already advances past the batch (existing behavior). A per-building `OutdoorAnimals` pass with no matching grazing animals simply yields empty animal work and completes (same as today's empty outdoor pass) — covered by the existing "all-outdoor-empty ⇒ no worker / batch completes" handling, which is extended to include `FarmForage`.
