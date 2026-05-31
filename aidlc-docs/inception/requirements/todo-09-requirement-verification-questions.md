# TODO-09 — Per-Building Animal Work Ordering — Requirements Clarification

**Change**: Make the worker perform **all of a single animal building's animal work (indoor housed animals AND that building's own grazing/outdoor animals) before moving on to the next building**, instead of today's behavior (all indoor buildings first, then one combined outdoor pass for every building's grazing animals + farm-wide forage).

**Type**: Enhancement (scheduling / routing UX). **No correctness bug** — purely reorders existing work.
**Scope**: `Dayswork.Core/Shifts/ShiftPlanBuilder.cs` (batch ordering) and `Dayswork/Orchestration/ShiftOrchestrator.cs` (`AnimalBuilding` / `OutdoorAnimals` batch handling + routing); grazing→home attribution already exists via `AnimalTaskHandler.ResolveHomeLocation` (`homeInterior.NameOrUniqueName`).

**Grounded current behavior** (verified in source):
- `ShiftPlanBuilder` emits one `AnimalBuilding` batch per selected building, then a single trailing `OutdoorAnimals` ("Farm") batch.
- The `OutdoorAnimals` batch services every selected building's grazing animals (pet/collect) **and** scans the whole farm for animal-product forage like truffles (`CollectAnimalProducts`), with a late-truffle pre-completion rescan.
- Truffles are **not** attributable to any specific building (they are farm-wide ground forage from foraging pigs).

Please answer each question by filling in the letter after the `[Answer]:` tag. If none fit, pick **X) Other** and describe.

---

## Question 1 — Farm-wide forage (truffles) placement
Truffles (and any non-animal-attributed animal-product forage) are farm-wide, not owned by a building. When we fold each building's grazing animals into its own visit, where should the farm-wide forage sweep happen?

A) Keep a single farm-wide forage sweep as a **final pass after all buildings** are serviced (recommended — truffles aren't building-owned; gather them once at the end, preserving today's behavior for that work)
B) Fold farm-wide forage into the **last** building's outdoor pass
C) Do the farm-wide forage sweep **first**, before any building
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2 — Order within a single building's visit
For each building, in what order should the worker do that building's indoor vs. outdoor animal work?

A) **Indoor first** — enter the building and do feed/pet/collect on housed animals, then exit and service that building's grazing animals, then move to the next building (recommended — service the interior while you're there, then sweep its roamers)
B) Outdoor (grazing) animals for that building first, then enter for indoor work
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3 — Grazing animals that roam far from their home building
Grazing animals wander the whole farm and may be nowhere near their home building. For a building's outdoor pass, should the worker still service **every** grazing animal that belongs to that building, wherever it currently is?

A) **Yes — always service every selected building's grazing animals wherever they roam** (recommended — preserves the current "every selected animal gets serviced" guarantee; this change is purely re-ordering, never dropping work)
B) Only service grazing animals currently **near** their home building during its visit; leave far-roaming ones to a final farm-wide sweep
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4 — Building visit ordering
The worker's complaint is cross-farm backtracking. Within this change, how should the **order of buildings** themselves be decided?

A) **Keep the existing deterministic order** (by `LocationName` ordinal, then `Tier`); this change only groups each building's indoor+outdoor work together (recommended — keeps scope tight and ordering unit-testable; nearest-building routing can be a separate future improvement)
B) Also reorder buildings by **proximity** (nearest building next from the worker's position) to further cut travel
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5 — Late-truffle rescan behavior
Today the single `OutdoorAnimals` batch re-scans the farm for newly-spawned truffles right before it completes (`TryRescanOutdoorAnimalProductsBeforeBatchComplete`), because pigs keep producing truffles through the day. Should that late rescan be retained on whichever pass owns farm-wide forage after this change?

A) **Yes — keep the late-truffle rescan** on the farm-wide forage pass (recommended — preserves current truffle coverage)
B) Drop the late rescan
X) Other (please describe after [Answer]: tag below)

[Answer]:  A

---

## Question 6 — Security Extensions
Should security extension rules be enforced for this change? (Project-wide default to date: **No** — no network/PII/auth surface.)

A) Yes — enforce all SECURITY rules as blocking constraints
B) No — skip all SECURITY rules (carry forward the existing project default; recommended — this is a local scheduling change with no new security surface)
X) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 7 — Property-Based Testing Extension
Should property-based testing (PBT, FsCheck) rules be enforced for this change? (Project-wide default to date: **Yes — full mode**. The reordering has clean invariants: every selected animal appears exactly once across batches, each building's animal work is contiguous, farm-wide forage is positioned per Q1.)

A) Yes — enforce all PBT rules as blocking constraints (carry forward existing project default; recommended)
B) Partial — PBT only for pure functions / serialization round-trips
C) No — skip all PBT rules
X) Other (please describe after [Answer]: tag below)

[Answer]: A
