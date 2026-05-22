# U-16 — Animals & Buildings: Business Logic Model

**Unit**: U-16 — Animals & Buildings
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=B, FD-Q4=A (+ hopper refinement), FD-Q5=B, FD-Q6=A, FD-Q7=A, FD-Q8=B, FD-Q9=A

Technology-agnostic flows for U-16. The unit turns the single-location shift into a **multi-location** one: the worker now visits selected buildings (entering through their doors), does animal and indoor tile work, and routes building output to the right chest. See [domain-entities.md](domain-entities.md) for shapes and [business-rules.md](business-rules.md) for enforceable rules.

---

## 0. Where this plugs into the existing shift

Today (`ShiftOrchestrator.StartShift`): scan farm zones → one flat `WorkList` → walk/act per item → one farm deposit run → exit. Everything runs on `Game1.getFarm()`.

U-16 inserts a **batch layer** above the existing per-item working loop and generalizes navigation/deposit to be location-aware. The per-item working loop, stuck escalation, hit-reaction, debris collection, and settlement mail are **unchanged** and reused inside each batch.

```
StartShift
  └─ BuildShiftPlan  ─────────────►  ordered List<WorkBatch>   (NEW, Flow 1)
        for each batch in order:                                (NEW, Flow 2)
          ├─ if building: walk to door → warp in                (NEW, Flow 3)
          ├─ run batch work:
          │     ├─ animal work  (Feed → Pet → Collect)          (NEW, Flow 4)
          │     └─ tile work    (existing greedy-NN loop)        (reuse)
          └─ if building: walk to interior door → warp out       (NEW, Flow 3)
  └─ Deposit run (warps into buildings for in-building chests)    (EXTENDED, Flow 5)
  └─ Exit + mailed settlement                                    (reuse, U-15)
```

---

## 1. Build the multi-location shift plan (`BuildShiftPlan`)

Runs once at shift start (NFR-PERF-02: scan once, not per frame).

**Inputs**: the active `Contract` (its `Zones`, `EnabledTasks`, `TaskDestinations`), the `ToolSnapshot`.

**Steps**:
1. **Partition zones by location.**
   - Outdoor zones → the single `OutdoorFarm` batch (`LocationName = "Farm"`).
   - Each building zone → a batch keyed by its interior `LocationName`. Classify the batch:
     - `AnimalBuilding` if the interior is an `AnimalHouse` (Coop/Barn family).
     - `Interior` otherwise (Greenhouse, Shed, Cabin, …).
2. **Outdoor farm batch.**
   - `TileWork` = the existing farm scan (`DetectTask` over the outdoor zones, greedy-NN ordered). Unchanged.
   - **Truffles & outdoor forage** (FD-Q3=B): if `CollectAnimalProducts` is enabled, ground truffle/forage objects lying inside a selected outdoor zone are added as `CollectAnimalProducts` tile items.
   - **Grazing animals** (FD-Q5=B): for every animal that *lives in a selected animal building* but is **currently outdoors on the farm**, add `AnimalWorkItem(LocationName="Farm", animal, PetAnimals)` and/or `CollectAnimalProducts` as applicable.
3. **Animal-building batch** (for each selected `AnimalBuilding`):
   - `FeedBuilding = true` if `FeedAnimals` is enabled.
   - `AnimalWork` = for each animal that lives here **and is currently inside**: `PetAnimals` (if enabled) and/or `CollectAnimalProducts` (if enabled and a product is ready).
   - `TileWork` = `IndoorWorkScanner.ScanInterior(...)` (usually empty in a barn; FD-Q6=A scans uniformly anyway).
4. **Interior batch** (Greenhouse/Shed/…): `TileWork = ScanInterior(...)`; no animal work; `FeedBuilding = false`.
5. **Order the batches** (FD-Q2=A): all `AnimalBuilding` batches → all `Interior` batches → the `OutdoorFarm` batch. Within `AnimalBuilding`/`OutdoorFarm`, animal work precedes tile work (Flow 2/4).
6. **Empty plan** ⇒ the existing empty-zone path fires (no worker / mailed full refund, FR-PAY-06 / DEV-U15-04). Empty includes "buildings selected but nothing actionable and no animals needing care."

> **Why animal buildings first**: FR-WORK-03 ranks Feed/Pet/Collect as the top three (time-sensitive for animal happiness). DEV-02 kept outdoor tile work on greedy-NN; animal tasks reclaim priority here (FD-Q2=A).

---

## 2. Execute a batch

For the current batch (driven by `ShiftContext.CurrentBatchIndex`):

1. **Enter** (if `Kind != OutdoorFarm`): run Flow 3 (door approach + warp in). On failure → **skip this batch** (FD-Q7=A), keep all buffered items, advance to the next batch.
2. **Animal work first** (if any), in Feed → Pet → Collect order:
   - `FeedBuilding` ⇒ emit `IntentFeedBuilding(location)` once (Flow 4a).
   - Each `PetAnimals` `AnimalWorkItem` ⇒ approach the animal's current tile, then `IntentPetAnimal` (Flow 4b).
   - Each `CollectAnimalProducts` `AnimalWorkItem` ⇒ approach, then `IntentCollectFromAnimal` (Flow 4c).
3. **Tile work** next: load the batch's `TileWork` into the working `WorkList` and run the **existing** per-item loop (walk → perform → collect debris → advance), greedy-NN ordered, including the stuck/hit-reaction machinery.
4. **Batch complete**: if it was a building, run Flow 3 exit (warp back to the farm). Advance `CurrentBatchIndex`; if more batches remain, go to step 1; else proceed to the deposit run (Flow 5).

All navigation in this flow targets **the batch's location**, not `Game1.getFarm()`.

---

## 3. Building entry / exit (door-warp, FR-WORK-09)

Handled by `BuildingWorkNavigator`.

**Enter**:
1. Resolve the building's outdoor **door tile** from its footprint. If the building/interior can't be resolved (demolished) → report failure (caller skips, FD-Q7=A).
2. Navigate the worker to the door tile on the farm (existing movement driver). If unreachable (blocked) → report failure.
3. **Warp**: move the worker out of the farm's character list into the interior's, set its position at the interior entry tile, set `currentLocation` to the interior. Log `log.building.entering`.

**Exit**:
1. Navigate to the interior's door/warp tile.
2. **Warp back**: move the worker into the farm's character list at the building's outdoor door tile, set `currentLocation` to the farm.

One enter + one exit per building per shift (FD-Q1=A), minimizing warps. The worker always returns to the farm between buildings, so the final deposit/exit happens from the farm.

---

## 4. Animal task execution (`AnimalTaskHandler`)

### 4a. Feed animals (FD-Q4=A + hopper refinement, FR-TASK-03)

Building-level action inside an `AnimalBuilding`:
1. If the building has the **auto-feed (deluxe) upgrade** ⇒ animals already fed; **skip** (no hay used).
2. Read hay available from the building's **hay hopper**, which is auto-supplied from the silo — the worker does **not** walk to the silo. Hay availability still depends on the player owning a **silo with hay**.
3. Place hay on empty feed-bench slots, one portion per housed animal, capped by available hay and bench capacity. Each placement deducts from the silo's hay total.
4. Edge cases: **no silo / no hay** ⇒ do nothing for this building, log `log.animal.no_silo` (no mail, no error); **benches already full / animals already fed** ⇒ skip. Feed produces no output and never mails (FR-TASK-09 spirit; hay stays in the world).

### 4b. Pet animals (FR-TASK-01 / FR-WORK-03)

For each target animal not yet petted today: approach its current tile, perform the pet interaction, mark petted. Already-petted animals are skipped at scan time (no wasted beats). No output.

### 4c. Collect animal products (FD-Q3=B, FR-TASK-04)

By `AnimalProductKind`:
- **FloorForage** (eggs and similar on the coop/barn floor): collected as world `Object`s via the tile path → buffered as `CollectAnimalProducts`.
- **ToolHarvest** (milk via milk pail, wool via shears): approach the animal, perform the harvest, buffer the produced item. The worker performs this **regardless of whether the player owns a milk pail / shears** — those tools are un-tiered, so worker tool-independence (DEV-U16-01, consistent with DEV-U15-03) applies. Only adult animals with product ready that day yield output.
- **GroundForage** (truffles on the farm ground): collected during the `OutdoorFarm` batch as world `Object`s → buffered as `CollectAnimalProducts`.

All collected products are buffered (not deposited yet) and routed at shift end (Flow 5) to `TaskDestinations[CollectAnimalProducts]` (chest or shipping bin). No per-product filtering (FR-OUT-07). Items the worker did not produce/forage from animals are never taken (NFR-SAFE-04; floor eggs and ground truffles are identified as animal-product forage by type, not as arbitrary player-dropped items).

---

## 5. Deposit run — multi-location (FD-Q8=B)

At shift end (work exhausted or 8pm cap), the worker is back on the farm (Flow 3 guarantees this).

1. **Plan** with the existing pure `DepositPlanner` over the whole buffer → ordered `DepositTrip`s by destination (animal products, greenhouse harvest, and outdoor drops all included). Unchanged.
2. **Execute trips**, extended to be location-aware:
   - Shipping-bin and **farm**-chest trips: walk + deposit on the farm (existing behavior).
   - **Building-interior chest** trips: `BuildingWorkNavigator` warps the worker into that building, deposits at the chest, warps back (one extra enter/exit per such chest). `ChestResolver` resolves the chest cross-location.
   - Chest missing/destroyed ⇒ items → settlement mail (`ChestMissing`); chest full ⇒ remainder → settlement mail (`ChestFull`). Unchanged U-14 behavior.
3. **Exit + settle**: walk to the farm entrance, leave, and queue the single next-morning settlement letter (overflow items + refund gold), exactly as U-15 (DEV-U15-04). Refund pro-rates on actual hours worked; deposit-run walking/warping is **not** billed (FR-PAY-05).

> Trips group by chest, so two chests in the same building are two trips (minor extra warps). Grouping trips by building is a possible later optimization, not required for v1.

---

## 6. Interaction with existing edge cases (no regressions)

- **Sleep stop (U-15 / DEV-U15-09)**: unchanged. On save, the worker hard-stops wherever it is (including inside a building); collected-but-undelivered items + refund are mailed. No headless building/animal work runs. Remaining world tasks stay undone.
- **Stuck escalation (U-13)**: applies within a batch's tile work as today. Building **navigation** failure does **not** use stuck escalation — it skips the batch (FD-Q7=A).
- **8pm hard cap**: can fire mid-batch (inside a building). The worker stops batch work, returns to the farm if inside (Flow 3 exit), then runs the deposit run and exits (FR-WORK-06: items never lost).
- **Festival days**: no skip (DEV-U15-02 / user) — animal/building shifts run normally.
- **Rain**: indoor crops (greenhouse/building) are still watered because Water Crops stays enabled (DEV-U15-05); outdoor crops are rain-watered and skipped naturally.
- **Deposit/hours estimate**: flat `DepositHoursPolicy.FlatPreviewHours` (FD-Q9=A / DEV-U15-07); building/animal-aware pricing deferred.

---

## 7. Data flow summary

```
Contract.Zones ─┬─ outdoor zones ───────────────► OutdoorFarm batch (tile work + truffles + grazing animals)
                └─ building zones ─┬─ AnimalHouse ► AnimalBuilding batch (feed + inside animals + indoor scan)
                                   └─ other ──────► Interior batch (indoor tile scan)

batches (ordered: AnimalBuilding → Interior → OutdoorFarm)
   │  per batch: [enter] → animal work (Feed→Pet→Collect) → tile work (greedy-NN) → [exit]
   ▼
ItemBuffer (animal products + greenhouse harvest + outdoor drops, tagged by TaskKind)
   ▼
DepositPlanner → trips → multi-location deposit run (warps into buildings for in-building chests)
   ▼
overflow/refund → one next-morning settlement letter (U-15)
```
