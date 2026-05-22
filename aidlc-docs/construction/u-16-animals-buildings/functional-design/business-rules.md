# U-16 — Animals & Buildings: Business Rules

**Unit**: U-16 — Animals & Buildings
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=B, FD-Q4=A (+ hopper refinement), FD-Q5=B, FD-Q6=A, FD-Q7=A, FD-Q8=B, FD-Q9=A

Enforceable rules for U-16. Each cites its source requirement/story/decision. See [business-logic-model.md](business-logic-model.md) for flows and [domain-entities.md](domain-entities.md) for types.

---

## Deviations & refinements introduced by U-16

| ID | Rule | Relates to | Reason |
|---|---|---|---|
| **DEV-U16-01** | `CollectAnimalProducts` includes milk & wool via tool-use on animals, and the worker performs milk/shear **regardless of whether the player owns a milk pail / shears**. | FD-Q3=B; clarifies FR-TASK-04; consistent with DEV-U15-03 | Milk pail/shears are un-tiered; the worker is tool-independent (it defaults to having basic tools), so milk/wool are produced like any other product. *(Surfaced for explicit approval — picking option B over C signalled "not ownership-gated".)* |
| **DEV-U16-02** | Pet/Collect act on a selected building's animals **wherever they currently are**, including animals grazing outdoors on the farm. | FD-Q5=B; extends FR-TASK-01/04 (no location restriction in the FR) | Player chose full coverage over inside-only; outdoor animals are handled during the outdoor-farm batch. |
| **DEV-U16-03** | Feeding draws hay from the building's **in-building hay hopper** (auto-supplied from the silo); the worker never walks to the silo. A silo with hay is still required; deluxe **auto-feed** buildings need no feeding. | FD-Q4=A + user note; refines FR-TASK-03 | Matches the vanilla hopper mechanic the player expects and keeps the feed action inside the building batch. |
| **DEV-U16-04** | Building/animal work keeps the **flat** `DepositHoursPolicy.FlatPreviewHours` deposit estimate; no building/animal-aware pricing in U-16. | FD-Q9=A; reaffirms DEV-U15-07 | Keeps U-16 scoped to work execution; refund still pro-rates on actual hours, so over/under-charge self-corrects. |

> **Design decisions (not deviations)**: FD-Q1=A location-batched visits; FD-Q2=A animal-buildings-first ordering; FD-Q6=A reuse the full tile scanner indoors; FD-Q7=A skip a bad building gracefully (no stuck escalation); FD-Q8=B single end-of-shift deposit run that warps into buildings.

---

## Shift composition & location batching (FD-Q1=A, FD-Q2=A)

**BR-LOC-01 — Work is partitioned into per-location batches.** A shift is an ordered list of `WorkBatch`es, one per `GameLocation` touched by the contract (the outdoor farm plus each selected building interior). *(FD-Q1=A)*

**BR-LOC-02 — Batch visit order.** Batches run in the order: all `AnimalBuilding` → all `Interior` → `OutdoorFarm` → deposit run. Within `AnimalBuilding` and `OutdoorFarm`, animal work precedes tile work. *(FD-Q2=A, FR-WORK-03)*

**BR-LOC-03 — Animal task ordering within a batch.** Animal work runs Feed → Pet → Collect, per `TaskPriorityOrderer`'s existing ranks (0/1/2). Tile work stays greedy nearest-neighbour. *(FD-Q2=A, DEV-02 carryover)*

**BR-LOC-04 — One enter + one exit per building.** A building is entered once, all its batch work is done, then it is exited back to the farm before the next batch. The worker is always on the farm between buildings and for the final exit. *(FD-Q1=A)*

**BR-LOC-05 — Navigation targets the current batch location.** All in-batch movement/scanning uses the batch's `GameLocation`; only spawn, farm-entrance exit, and the shipping bin reference `Game1.getFarm()`. *(FD-Q1=A)*

---

## Building entry / exit (FR-WORK-09, FD-Q7=A)

**BR-NAV-01 — Door-warp entry.** The worker enters a building by pathing to its outdoor door tile and warping to the interior entry tile; it exits by pathing to the interior door and warping back to the outdoor door tile. *(FR-WORK-09)*

**BR-NAV-02 — Skip an unreachable/invalid building.** If a building's interior cannot be resolved (demolished) or its door/interior cannot be reached (blocked), that batch is skipped: log it, keep all buffered items, and continue with the next batch. Building-nav failure does **not** trigger the stuck escalation. *(FD-Q7=A, DoD "handle missing/invalid interiors gracefully")*

**BR-NAV-03 — Mid-building cap/sleep returns the worker to the farm.** If the 8pm cap fires while inside a building, the worker stops batch work and exits to the farm before the deposit run. On sleep, the worker hard-stops in place (U-15 sleep-stop), mailing collected items. *(FR-WORK-06, DEV-U15-09)*

---

## Animal scope & detection (FD-Q5=B)

**BR-ANIM-01 — Animals come from selected animal buildings.** The animals a contract cares for are those that **live in** a selected `AnimalHouse` building. Drawing only an outdoor rectangle (no building) enables no animal care (except ground-truffle pickup, BR-PROD-04). *(FR-HIRE-05, FD-Q5=B)*

**BR-ANIM-02 — Pet/Collect follow the animal's current location.** A pet/collect work item is assigned to the batch matching where the animal **currently is**: inside its home building → that building's batch; grazing outdoors → the outdoor-farm batch. Feeding is always the building batch. *(FD-Q5=B, reconciles FD-Q1=A)*

**BR-ANIM-03 — Skip already-done animal work.** Animals already petted today are not re-petted; animals with no product ready are not collected; a building already fed (or auto-fed) is not re-fed. *(no-wasted-work assumption; NFR-PERF-02)*

---

## Feed animals (FD-Q4=A + hopper refinement, FR-TASK-03)

**BR-FEED-01 — Feed from the in-building hopper.** For each selected animal building with `FeedAnimals` enabled, the worker fills empty feed-bench slots from the building's hay hopper (auto-supplied by the silo). The worker does not travel to the silo. *(FD-Q4=A + user note, DEV-U16-03)*

**BR-FEED-02 — Hay portions and caps.** One hay portion is placed per housed animal, capped by available silo hay and bench capacity; each placement deducts from the silo total. *(FR-TASK-03)*

**BR-FEED-03 — No silo / no hay / auto-feed.** With no silo or no hay, feeding does nothing for that building (log only, no mail, no error). A deluxe **auto-feed** building is treated as already fed and skipped. *(DEV-U16-03)*

**BR-FEED-04 — Feed never mails, never outputs.** Feeding produces no buffered item and never mails hay (FR-TASK-09 spirit). *(FR-TASK-03, FR-TASK-09)*

---

## Collect animal products (FD-Q3=B, FR-TASK-04)

**BR-PROD-01 — Product taxonomy.** Products are gathered by kind: FloorForage (eggs etc. on the floor → pick up), ToolHarvest (milk via milk pail, wool via shears → use on the animal), GroundForage (truffles on the farm ground → pick up). *(FD-Q3=B)*

**BR-PROD-02 — Milk/wool are tool-independent.** The worker performs milk/shear on ready adult animals regardless of whether the player owns a milk pail or shears. *(DEV-U16-01, FD-Q3=B, DEV-U15-03)*

**BR-PROD-03 — Only ready products.** ToolHarvest applies only to adult animals with product ready that day; FloorForage/GroundForage applies only to existing product objects. *(FR-TASK-04)*

**BR-PROD-04 — Truffles via the outdoor batch.** When `CollectAnimalProducts` is enabled, ground truffle/forage objects within a selected outdoor zone are collected during the outdoor-farm batch. *(FD-Q3=B, FD-Q5=B)*

**BR-PROD-05 — Single destination, no filtering.** All animal products buffer as `CollectAnimalProducts` and route to `TaskDestinations[CollectAnimalProducts]` (chest or shipping bin); no per-product sorting. *(FR-TASK-04, FR-OUT-07)*

**BR-PROD-06 — Only animal-caused items.** The worker collects only animal products/forage (identified by item type), never arbitrary player-placed or -dropped items. *(NFR-SAFE-04)*

---

## Indoor tile work (FD-Q6=A)

**BR-IND-01 — Reuse the full scanner indoors.** Building interiors are scanned with the same `DetectTask` logic as the farm; whatever applies fires (greenhouse crops watered/harvested/fruit-collected; an empty interior yields nothing). *(FD-Q6=A)*

**BR-IND-02 — Whole-interior placeholder.** A building zone's `(0,0)..(999,999)` bounds mean "the entire interior"; the scan is clamped to the interior map's real dimensions. *(building-zone representation)*

**BR-IND-03 — Indoor crops watered on rainy days.** Water Crops applies to indoor crops even on rainy days, since interiors are not rained on. *(DEV-U15-05, FD-Q6=A)*

---

## Output routing & deposit (FD-Q8=B)

**BR-DEP-01 — Single end-of-shift deposit run.** Animal products, greenhouse harvest, and outdoor drops all buffer during the shift and are deposited in one run at shift end via the existing `DepositPlanner`. No depositing during building visits. *(FD-Q8=B)*

**BR-DEP-02 — Deposit run warps into buildings.** A trip to a building-interior chest warps the worker into that building, deposits, and warps back; farm-chest and shipping-bin trips run on the farm. `ChestResolver` resolves chests cross-location. *(FD-Q8=B)*

**BR-DEP-03 — Existing fallbacks hold.** Chest missing/destroyed → items to settlement mail (`ChestMissing`); chest full → remainder to settlement mail (`ChestFull`); shipping bin never overflows. *(FR-OUT-02/03/06, U-14 carryover)*

**BR-DEP-04 — Deposit/warp time is unbilled.** The refund is `clamp(deposit − hoursWorked × rate, 0, deposit)`; deposit-run walking and warping are not billed. *(FR-PAY-05, NFR-SAFE-02)*

**BR-DEP-05 — Flat deposit estimate.** The day's deposit uses `DepositHoursPolicy.FlatPreviewHours`; building/animal-aware estimation is deferred. *(FD-Q9=A, DEV-U16-04, DEV-U15-07)*

---

## Settlement & safety (carried from U-15, must still hold)

**BR-SET-01 — One mailed settlement letter.** Overflow items (incl. animal products that didn't land in a chest) and the refund go in one next-morning settlement letter per shift. *(DEV-U15-04, BR-REF-03)*

**BR-SAFE-01 — No items lost across new branches.** Building skip (BR-NAV-02), mid-building cap, sleep-stop, and the multi-location deposit run all preserve conservation: every collected item is deposited or mailed. *(NFR-SAFE-01)*

**BR-SAFE-02 — Tolerate absent data.** Missing silo/hopper, missing interior, no animals, or a demolished building degrade gracefully (skip/no-op), never a crash. *(NFR-SAFE-03)*

**BR-SAFE-03 — No new state-machine phase.** Warps ride inside `Working`/`Depositing`; the `ShiftStateMachine` phase set and its PBT-03 invariants are unchanged. *(NFR-MAINT-03, S-19)*

---

## i18n (S-20)

**BR-I18N-01 — New strings routed through `I18nHelper`.** The new building/animal **log** keys (`log.building.*`, `log.animal.*`) are added to `i18n/default.json`. No new mail strings — animal-product overflow and the refund reuse U-15's settlement letter. *(NFR-UX-02, S-20)*

---

## PBT obligations (Property-Based Testing extension — enabled, Partial mode)

U-16 is largely SMAPI/Stardew-driven (animal interactions, building warps) and primarily play-tested. Pure helpers carry PBT/unit coverage:

| Rule | Property / test |
|---|---|
| BR-LOC-01/02 | `BuildShiftPlan` partitioning + ordering: for any set of zones, batches are well-formed (each zone maps to exactly one batch) and ordered AnimalBuilding → Interior → OutdoorFarm (unit + property). |
| BR-LOC-03 | Animal work within a batch is ordered Feed → Pet → Collect — reuses `TaskPriorityOrderer` stable-sort property. |
| BR-DEP-01/03 | `DepositPlanner` conservation/trip-count invariants still hold with animal-product stacks added — reuses U-14 DepositPlanner properties. |
| BR-DEP-04 | Refund formula unchanged — reuses U-05 `RefundCalculator` invariants. |
| BR-SAFE-03 | `ShiftStateMachine` invariants unchanged — reuses U-10 state-machine properties (no new phase). |

SMAPI/Stardew-bound behavior (warp handoff, feed-bench placement, milk/shear, truffle pickup, building skip) is verified by play-test per the U-16 Definition of Done.

Security Baseline extension is **disabled** project-wide (no network/PII/auth surface); all its rules are **N/A** for U-16.
