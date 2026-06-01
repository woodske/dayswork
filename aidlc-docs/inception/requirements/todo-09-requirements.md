# TODO-09 — Per-Building Animal Work Ordering — Requirements

## Intent Analysis
- **User request**: "Add a todo task for the worker to perform all inside and outside animal building tasks before moving to the next animal building. Right now it does the indoor tasks for each building, then does the outdoor tasks for all at once, but that wouldn't make sense if the animal buildings are spread out over the farm." (now picked up as TODO-09)
- **Request type**: Enhancement (worker scheduling / routing UX). **Not a correctness bug** — reorders existing work only.
- **Scope estimate**: Single component boundary — `Dayswork.Core/Shifts/ShiftPlanBuilder.cs` (batch ordering) + `Dayswork/Orchestration/ShiftOrchestrator.cs` (`AnimalBuilding` / `OutdoorAnimals` batch handling and routing). Supporting attribution already exists in `Dayswork/Orchestration/AnimalTaskHandler.cs`.
- **Complexity estimate**: Moderate — batch-plan restructuring with an explicit invariant that no animal work is dropped or duplicated, plus a retained farm-wide forage pass.
- **Requirements depth**: Standard.

## Grounded Current Behavior (verified in source)
- `ShiftPlanBuilder.BuildBatchPlan` produces one `AnimalBuilding` batch per selected building (ordered by `LocationName` ordinal, then `Tier`), followed by **a single trailing `OutdoorAnimals` ("Farm") batch** when any non-feed animal task is enabled.
- `ShiftOrchestrator` enters each building interior to perform feed/pet/collect on housed animals (`CompleteBuildingEntry`), and runs the **one** `OutdoorAnimals` pass on the Farm: pet/collect every selected building's grazing animals (`RefreshOutdoorAnimalWork` → `BuildAnimalWork`) **plus** a whole-farm scan for animal-product forage (truffles) via `ScanWholeLocation`, with a late-truffle pre-completion rescan (`TryRescanOutdoorAnimalProductsBeforeBatchComplete`).
- Grazing animals are already attributable to their home building via `AnimalTaskHandler.ResolveHomeLocation` → `homeInterior.NameOrUniqueName` (the same key used for selection matching, TODO-08). `HomeLocationKeys` provides the legacy/type-name fallbacks.
- Truffles / floor-forage animal products are **farm-wide** ground forage (foraging pigs) and are **not** attributable to a specific building.

## Functional Requirements
- **FR-T09-01 — Per-building grouping.** For each selected animal building, the worker performs **all** of that building's animal work before moving to the next building: its indoor housed-animal work (feed/pet/collect) **and** its own grazing/outdoor animals (pet/collect). *(Q-core; replaces the indoor-all-then-outdoor-all ordering.)*
- **FR-T09-02 — Indoor-first within a building visit.** Within a single building's grouped work, indoor housed-animal work runs first (enter interior, feed/pet/collect), then the worker exits and services that building's grazing animals, then advances to the next building. *(Q2=A)*
- **FR-T09-03 — Grazing→building attribution.** A grazing animal is attributed to its home building using the existing home key (`homeInterior.NameOrUniqueName`, with the `HomeLocationKeys` legacy/type-name fallbacks) so each building's outdoor pass services exactly the grazing animals that belong to it. *(Q3=A; reuses TODO-08 attribution.)*
- **FR-T09-04 — No dropped or duplicated animal work.** Every selected animal that is serviced today must still be serviced after the change, exactly once. A building's grazing animals are serviced wherever they currently roam on the farm (never skipped for being far from their home building). This change is purely a re-ordering. *(Q3=A)*
- **FR-T09-05 — Farm-wide forage as a final pass.** Farm-wide, non-building-attributed animal-product forage (truffles) is collected in a single farm-wide sweep that runs **after** all building visits are complete. *(Q1=A)*
- **FR-T09-06 — Retain late-truffle rescan.** The final farm-wide forage pass retains the existing pre-completion rescan that picks up truffles spawned later in the day. *(Q5=A)*
- **FR-T09-07 — Building visit ordering unchanged.** The order in which buildings are visited stays the existing deterministic order (`LocationName` ordinal, then `Tier`). Proximity/nearest-building routing is explicitly out of scope (possible future follow-up). *(Q4=A)*
- **FR-T09-08 — Vanilla & SVE parity for shape.** The new ordering applies identically whether one or many animal buildings are selected, and under both vanilla and SVE (the change is structural, not content-specific). A single selected building yields one grouped visit; the farm-wide forage pass still runs when `CollectAnimalProducts` is enabled.

## Non-Functional Requirements
- **NFR-T09-01 — Determinism.** Batch ordering and per-building grouping are deterministic for a given selection + enabled-task set (required for unit + property tests).
- **NFR-T09-02 — Performance.** No additional whole-farm scans beyond what runs today; the per-building grazing pass partitions the same animal set rather than re-scanning per building. No per-shift regression.
- **NFR-T09-03 — Testability (PBT, full mode).** Pure batch-plan logic must be unit- and property-testable. Properties: (a) every selected animal's work appears exactly once across the plan; (b) each building's animal work (indoor + its grazing) is contiguous / not interleaved with other buildings; (c) the farm-wide forage pass is positioned last; (d) building-visit order is preserved.
- **NFR-T09-04 — Backward compatibility.** No save-schema change; existing contracts (legacy type-name homes) continue to resolve via the existing fallbacks. No config/UI change.

## Extension Configuration (this change)
| Extension | Enabled | Decided At |
|---|---|---|
| Security Baseline | No (Q6=B — local scheduling change, no security surface) | Requirements Analysis |
| Property-Based Testing | Yes — full mode (Q7=A) | Requirements Analysis |

## Out of Scope
- Proximity / nearest-building visit ordering (Q4=A defers this).
- Any change to which animals/products are detected or how they are collected (TODO-07 already shipped category-based detection).
- Multi-hop navigation or Grandpa's Shed greenhouse (tracked separately as TODO-10).

## Key Requirements Summary
Re-order animal work so the worker fully services one building (indoor housed animals **then** that building's grazing animals) before moving to the next, using the existing home-key attribution, while keeping a single farm-wide truffle sweep (with its late rescan) as a final pass — all deterministic, no dropped/duplicated work, no save/config/UI change, covered by example + FsCheck tests.
