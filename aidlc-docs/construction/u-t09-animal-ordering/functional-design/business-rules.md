# Functional Design — Business Rules — u-t09-animal-ordering

## Business Rules

- **BR-T09-01 — Per-building grouping.** For each selected animal building, all of that building's animal work (interior housed-animal work, then that building's grazing-animal work) is emitted as a contiguous block before any other building's animal work. *(FR-T09-01)*
- **BR-T09-02 — Indoor before that building's outdoor.** Within a building's block, the `AnimalBuilding` (interior) batch precedes the building's `OutdoorAnimals` (grazing) batch. *(FR-T09-02, Q2=A)*
- **BR-T09-03 — Per-building grazing scope.** An `OutdoorAnimals` batch services grazing animals whose home key equals that batch's `LocationName`, using the existing `AnimalTaskHandler` home keys (`homeInterior.NameOrUniqueName` + plain name + building type). *(FR-T09-03)*
- **BR-T09-04 — Grazing pass tasks.** An `OutdoorAnimals` (grazing) batch carries the non-feed animal tasks (`PetAnimals` and/or `CollectAnimalProducts`); it never feeds (feeding is interior-only). It is emitted only when at least one non-feed animal task is enabled.
- **BR-T09-05 — No dropped or duplicated work.** Every animal serviced under the baseline is serviced after the change, exactly once. Grazing animals are serviced wherever they roam (never skipped for being far from home). Re-matching under legacy shared type-name keys is idempotent (pet checks `wasPet`; collect clears `currentProduce`). *(FR-T09-04)*
- **BR-T09-06 — Farm-wide forage is a single final pass.** Ground forage (truffles) is collected by exactly one `FarmForage` batch positioned after all building blocks (and before greenhouse/crop/clearing batches). It is emitted only when `CollectAnimalProducts` is enabled. *(FR-T09-05, Q1=A)*
- **BR-T09-07 — Forage scope.** The `FarmForage` batch performs only the whole-farm ground-forage tile scan (`CollectAnimalProducts`); it carries no animal pet/collect work. Forage provenance remains `AnimalBuilding(empty)` (unchanged overflow-mail behavior).
- **BR-T09-08 — Retain late-truffle rescan.** The `FarmForage` batch retains the pre-completion rescan that picks up truffles spawned later in the day (the rescan previously attached to the combined `OutdoorAnimals` batch moves here). *(FR-T09-06, Q5=A)*
- **BR-T09-09 — Building order preserved.** The order buildings are visited is unchanged: `LocationName` ordinal, then `Tier`. No proximity routing. *(FR-T09-07, Q4=A)*
- **BR-T09-10 — Crop/greenhouse ordering preserved.** Greenhouse, OutdoorCrops, and OutdoorClearing batches keep their existing relative order and placement after all animal work.
- **BR-T09-11 — No save/config/UI change.** No new persisted contract data, config keys, or menu changes. Existing (incl. legacy) contracts produce the new ordering with no migration. *(NFR-T09-04)*

## Property-Based Test Properties (FsCheck — full mode, PBT enabled)
- **P-T09-1 — Per-building pairing & contiguity.** For any scope/task shape, every `AnimalBuilding` batch is, when non-feed animal tasks are enabled, immediately followed by an `OutdoorAnimals` batch with the **same** `LocationName`; no other building's animal batch interleaves a building's block.
- **P-T09-2 — Building order preserved.** The sequence of `AnimalBuilding` `LocationName`s equals the buildings sorted by (`LocationName` ordinal, `Tier`).
- **P-T09-3 — Single farm-forage, positioned last among animal work.** At most one `FarmForage` batch; it appears only when `CollectAnimalProducts` is enabled; it appears after all `AnimalBuilding`/`OutdoorAnimals` batches and before any Greenhouse/OutdoorCrops/OutdoorClearing batch.
- **P-T09-4 — Grazing batch count.** The number of `OutdoorAnimals` batches equals the number of selected buildings when non-feed animal tasks are enabled, else zero.
- **P-T09-5 — Bounded non-animal families.** At most one each of Greenhouse, OutdoorCrops, OutdoorClearing (unchanged invariant).
- **P-T09-6 — Skeletons empty.** `ShiftPlanBuilder` output carries empty `TileWork`/`AnimalWork` (filled later by the runtime) — unchanged invariant.

## Example-Based Test Cases (xUnit)
- **EX-T09-1** Two buildings + Feed/Pet enabled, no Collect → order `[AnimalBuilding(Barn), OutdoorAnimals(Barn), AnimalBuilding(Coop), OutdoorAnimals(Coop)]`; no `FarmForage`.
- **EX-T09-2** Two buildings + Feed/Pet/Collect → adds one `FarmForage(Farm)` after the building pairs.
- **EX-T09-3** One building + Collect only → `[AnimalBuilding, OutdoorAnimals, FarmForage]`.
- **EX-T09-4** Feed only → per-building `AnimalBuilding` batches only; no `OutdoorAnimals`, no `FarmForage`.
- **EX-T09-5** Mixed (animal + greenhouse + outdoor crops/clearing) → building pairs, then `FarmForage` (if Collect), then `Greenhouse`, `OutdoorCrops`, `OutdoorClearing`.
