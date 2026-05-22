# U-16 — Animals & Buildings: Functional Design Plan

**Unit**: U-16 — Animals & Buildings
**Stories**: S-08 (completes — animal task execution + building-interior work finish the FR-WORK-03 priority queue), S-03 / S-04 deepening (selected buildings become real work areas; building-interior output routing is exercised). Closes **TODO-05**.
**Phase**: CONSTRUCTION — Functional Design

---

## Plan Checklist

- [x] FD-Q1–Q9: Collect answers (Q1=A, Q2=A, Q3=B, Q4=A + hopper note, Q5=B, Q6=A, Q7=A, Q8=B, Q9=A)
- [x] Resolve any ambiguities/contradictions — no vague answers; three reconciliations designed in (Q1+Q5 animal-location batching, Q3+DEV-U15-03 milk/shear tool-independence → DEV-U16-01, Q4 hopper refinement → DEV-U16-03)
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [ ] Present completion message and await approval

---

## Context Summary

U-16 makes **selected buildings real work areas** and gives the worker its **three animal tasks**. Today the worker is entirely farm-bound: the orchestrator runs every phase on `Game1.getFarm()`, the work list is a flat `Queue<WorkItem>` with no location dimension, `BuildWorkList` skips any zone whose `LocationName != "Farm"`, and `InvokeTaskAction` has handlers for only the 7 outdoor tasks. This unit adds cross-location traversal (door-warp), animal task detection/invocation, and indoor tile scanning.

**Components owned (new files)**:
- **BuildingWorkNavigator** (`Dayswork/Orchestration/`) — drives the walk-to-door → warp-in → work → warp-out handoff for one building visit.
- **IndoorWorkScanner** (`Dayswork/Orchestration/`) — scans a building interior `GameLocation` for actionable work (tile tasks and/or animals).
- **AnimalTaskHandler** (`Dayswork/Orchestration/`) — performs Feed / Pet / Collect-animal-products against live `FarmAnimal` state.

**Components extended**:
- **M-12 ShiftOrchestrator** — generalize the single-location assumption to a multi-location shift (location context, building-door warp navigation, animal task detection/invocation, deposit routing for indoor chests).
- **WorkerMovementDriver** — door approach + cross-location warp handoff (today `StartNavigation` binds the worker to one location and `IsTilePassableForWorker` special-cases `Farm`).
- **C-07 TaskPriorityOrderer** — the animal priority slots (Feed → Pet → Collect, currently never produced) become executable.
- **M-20 ChestResolver / C-11 DepositPlanner** — animal-product and greenhouse/building-interior output routing (ChestResolver already resolves cross-location chests by name+tile; the deposit *run* is what's farm-only today).
- **`i18n/default.json`** — any new log/mail strings for animal/building work, routed through `I18nHelper` (S-20).

**What already exists that U-16 builds on**:
- A **selected building is stored as a Zone** `Zone(interiorName, (0,0), (999,999))` (see `HiringFlowCoordinator` building-select completion). `interiorName` is the building's *interior* `GameLocation` name (e.g. `"Coop"`, `"Barn2"`, `"Greenhouse"`). The `(0,0)..(999,999)` bounds are a whole-interior placeholder.
- **`ChestResolver.ResolveChest(ChestRef)`** already does `Game1.getLocationFromName(ref.LocationName)` — so a chest inside a building resolves correctly regardless of where the worker is.
- **`ChestResolver.GetBuildingOutlines(farm)`** already enumerates building footprints (and falls back to a by-type lookup for the Greenhouse whose interior isn't linked via `building.indoors`).
- **`DepositPlanner`** is pure and location-agnostic: it groups buffered items by destination key and produces ordered trips. The trip `.Tile` is a chest tile; only the orchestrator's deposit *walk* assumes the Farm.
- **`ShiftContext`** carries the work queue, item buffer (`Buffer.Add(itemId, qty, sourceTask)`), the overflow/settlement list, refund math, and the state machine.
- **`ShiftStateMachine`** phases: WaitingForSpawn, Working, Stuck, Recovering, Depositing, Exiting, Done. Intents live in `ShiftIntent` (`IntentMoveToTile`, `IntentPerformTaskAt`, `IntentTeleport*`, `IntentDeposit*`, `IntentExitFarm`).
- The **stuck escalation** (Pattern D/E), **hit-reaction**, **debris collection/sweep**, and **settlement-mail** machinery already exist and are reused.

**Already decided / NOT in scope for these questions** (carried from prior units — feed these into the design rather than re-deciding):
- **Single active contract** (DEV-U15-01): buildings + animals are zones inside the one active contract; no multi-worker.
- **NPC-backed placeholder worker** (DEV-01, FR-NPC-01): U-16 does not change the worker entity or add custom art.
- **No festival skip** (DEV-U15-02, per your call): animal/building shifts run on festival days like any other day.
- **Missing tool → lowest tier, no skip/warning** (DEV-U15-03): applies to any tool an indoor/animal task needs; the tier gate still blocks out-of-tier targets.
- **Settlement by mail next morning** (DEV-U15-04): animal/building overflow + refund flow through the same single settlement letter; no new save structure.
- **Rain keeps Water Crops enabled** (DEV-U15-05): this exists *specifically* so indoor (greenhouse/building) crops still get watered on rainy days — directly relevant to FD-Q6.
- **No-op already-done work** (assumption, stated as a business rule, not a question): the worker skips animals already fed / already petted / with no ready product, and crops already watered/not-ready — same "no wasted beats" principle as outdoor work.
- **Animal-building detection** (assumption): a selected building counts as an animal building when its interior is an `AnimalHouse`; the Greenhouse/sheds are non-animal interiors. Exact API is a Code-Generation detail.

---

## Design Questions

> Answer each by writing after its `[Answer]:` tag. Option **A** is the recommendation. Pick a letter, and add a sentence if you want to steer the detail. If none fit, choose "Other" and describe.

### FD-Q1 — Cross-location work model (how a multi-location shift is structured)

`WorkItem` has no location today and the whole orchestrator runs on `Game1.getFarm()`. Buildings are separate `GameLocation`s, so you cannot pathfind or measure distance from a farm tile to an indoor tile — you must *warp*. We need a model for structuring work across the farm + selected buildings.

**A) Location-batched visits (Recommended).** Treat each location as a self-contained batch. The shift becomes an ordered list of batches; for each building batch the worker walks to the door → warps in → does **all** that building's work → warps back to the farm. Outdoor farm work is just one batch. Within a batch, keep the existing greedy nearest-neighbour routing. `WorkItem` gains a `LocationName`. Minimizes warps (one enter + one exit per building) and means the worker is always back on the farm to exit.

**B) Single global queue, warp-on-demand.** Keep one flat work queue but tag each item with its location; the worker warps whenever the next item's location differs from where it is. Simpler queue model, but can bounce in and out of the same building repeatedly (ugly and warp-heavy) unless the queue is pre-sorted by location anyway.

**C) Other (describe after the tag).**

[Answer]: A

---

### FD-Q2 — Location visit order (where animal/building work sits vs. outdoor)

DEV-02 replaced strict FR-WORK-03 priority with greedy nearest-neighbour for *outdoor* tasks, explicitly noting animal tasks as "the future exception" that "can regain first-priority handling" in this unit. The unit definition lists "C-07 TaskPriorityOrderer (animal priority slots become executable)." So we need the order in which the worker visits locations (assuming FD-Q1=A batching).

**A) Animal buildings first, then other interiors (greenhouse/sheds), then outdoor farm, then deposit (Recommended).** Honors the time-sensitive Feed → Pet → Collect intent (FR-WORK-03 ranks animal tasks 1–3). Animal tasks *within* a building run in that fixed priority order; outdoor and indoor tile work stays greedy nearest-neighbour within their batches.

**B) Nearest-door order.** Visit whichever location's door is closest to the worker's current position, regardless of task type; ignore animal time-sensitivity. Most "efficient walking," least faithful to the priority spec.

**C) Outdoor farm first (greedy), then buildings.** Do all open-farm work, then sweep the buildings. Simple, but defers time-sensitive animal care to the end of the day.

**D) Other (describe after the tag).**

[Answer]: A

---

### FD-Q3 — "Collect animal products" scope (which products) (FR-TASK-04)

Animal products differ in *how* they're gathered: **eggs** sit on the coop floor (pick up), **truffles** are foraged on the **farm ground** (placed by pigs roaming outdoors), while **milk** (milk pail) and **wool** (shears) require using a tool **directly on the animal**.

**A) Floor + forage products only (Recommended for v1).** Collect items on coop/barn floors (eggs, duck feathers, rabbit's foot, etc.) and truffles on the farm ground. **Skip milk and wool** (which need a tool used on each animal). Documented v1 narrowing of FR-TASK-04; milk/wool deferred post-v1.

**B) All products including milk & wool.** The worker uses the milk pail / shears on each ready animal, plus floor eggs and farm truffles. Fuller coverage; adds per-animal tool-use interactions and milk-pail/shears entries in the capability snapshot.

**C) Floor/forage + milk & wool, but capability-gated.** Like B, but milk/wool are collected only if the player owns the milk pail / shears (same "owned tool" gating as axe/pickaxe); otherwise those are skipped while eggs/truffles still collect.

**D) Other (describe after the tag).**

[Answer]: B

---

### FD-Q4 — "Feed animals" mechanic & hay edge cases (FR-TASK-03)

Vanilla feeding places **hay on the building's feeding-bench tiles**, drawn from the **silo**; animals then eat from the bench. (Grass-grazing happens outdoors; auto-feed needs a hopper.)

**A) Fill feeding benches from silo hay (Recommended).** For each selected animal building, place hay on its feed-bench tiles up to the building's animal count, deducting from the silo. Edge cases: silo empty → feed as many as the hay covers, skip the rest (no mail, no error); benches already full / animals already fed → skip the building; player has no silo → the task does nothing for that building. Matches the visible vanilla mechanic.

**B) Abstracted feed.** Mark each animal fed and deduct the equivalent hay from the silo total, without simulating bench-tile placement. Less code, but diverges from what the player sees in the building and from `FR-TASK-03`'s "consumes hay from the silo" wording (still satisfied numerically).

**C) Other (describe after the tag).**

[Answer]: A, but note that each animal building has a hay hopper that automatically moves hay from the silo so the worker does not need to go to the silo directly. The farmer will still need a silo for the worker to extract hay from the hopper.

---

### FD-Q5 — Animal scope: inside the building only vs. also outdoors grazing

Pet/Collect target individual `FarmAnimal`s. On sunny non-winter days animals are often **let out to graze on the farm** and aren't inside the coop/barn.

**A) Inside the assigned building only (Recommended for v1).** Feed / Pet / Collect act on animals currently **home** in a selected barn/coop. Animals out grazing are skipped that day (the player can keep them in, or they're handled next morning). Avoids chasing roaming animals around the farm and keeps animal work inside the batched building visit (FD-Q1). Documented v1 narrowing.

**B) All of a selected building's animals, wherever they are.** Pet/Collect also handle animals grazing outdoors on the farm — the worker walks to each outdoor animal. Fuller coverage; adds outdoor animal targeting/pathing and blurs the "building batch" model (animal work would also occur in the farm batch).

**C) Other (describe after the tag).**

[Answer]: B

---

### FD-Q6 — Indoor tile-task scope (greenhouse / building interiors)

A selected greenhouse/shed interior is scanned for tile work. The Greenhouse holds crops (water / harvest / collect fruit); indoor crops are **never rained on** — which is exactly why DEV-U15-05 keeps Water Crops enabled on rainy days.

**A) Reuse the full tile scanner indoors (Recommended).** Run the same `DetectTask` scan inside any selected building interior; whatever applies fires (greenhouse crops get watered/harvested/fruit-collected; an empty shed yields nothing). Indoor crops are watered even on rainy days. One scanner, uniform behavior, least new code.

**B) Crop tasks only indoors.** Inside buildings, scan only for Water / Harvest / Collect fruit; never look for rocks/trees/weeds/grass (which don't occur in interiors). Marginally less scanning, an extra special-case branch.

**C) Other (describe after the tag).**

[Answer]: A

---

### FD-Q7 — Building navigation failure / invalid interior handling

A selected building may be **demolished mid-shift**, have its **door blocked**, or an **unreachable interior**. The DoD requires the worker to "handle missing/invalid interiors gracefully."

**A) Skip the building's remaining work and continue (Recommended).** If the door/interior can't be reached or resolved, log it, abandon that building's batch, and move to the next location. Anything already collected stays buffered and is deposited/mailed normally — no items lost (NFR-SAFE-01). Does **not** trip the stuck escalation.

**B) Route through the existing stuck escalation.** Treat building-nav failure like being stuck (emote → teleport attempt → if still stuck, end shift early). Reuses U-13 machinery, but one bad building can end the entire shift.

**C) Other (describe after the tag).**

[Answer]: A

---

### FD-Q8 — Where building/animal output is deposited (cross-location deposit)

Animal products and greenhouse harvest are buffered, then routed to the assigned destination — which may be a **chest inside a building**. The end-of-shift deposit run is farm-only today.

**A) Deposit a building's output to its in-building chest during that building's visit; farm/shipping-bin output waits for the end-of-shift run (Recommended).** While inside a building, the worker drops that location's collected items into the assigned chest then and there; only farm-chest and shipping-bin destinations are handled by the existing post-shift deposit loop (unchanged). Minimizes warps and reuses the farm deposit run as-is for outdoor destinations.

**B) Single end-of-shift deposit run for everything, extended to warp into buildings.** Keep one deposit phase but let it warp into a building for any building-interior chest (one extra enter/exit per such chest at deposit time). Uniform "deposit at the end" model, more warps.

**C) Logical deposit for building chests.** Add items directly to a building-interior chest via `ChestResolver` without the worker physically warping (chests are addressable cross-location). Simplest and avoids extra warps, but the worker doesn't visibly carry items to the chest.

**D) Other (describe after the tag).**

[Answer]: B

---

### FD-Q9 — Deposit / hours estimate for animal & building work (pricing scope)

DEV-U15-07 made recurring deposits use a flat `DepositHoursPolicy.FlatPreviewHours` (1.0 billable hour) because building zones are stored as `(0,0)..(999,999)` placeholders and the tile-based `HoursEstimator` produced impossible deposits over them. A proper building/animal-aware estimate is a known deferred pricing task.

**A) Keep the flat deposit estimate for U-16; defer building/animal-aware pricing (Recommended).** U-16 adds *work execution* only; deposits stay on the flat policy so this unit doesn't reopen pricing. The refund still pro-rates against actual hours worked (FR-PAY-05), so any over/under-charge self-corrects at exit. Revisit estimation in the deferred HoursEstimator/pricing pass.

**B) Build a building/animal-aware estimate now.** Count animals + indoor actionable tiles into estimated hours and the deposit, and fix the building-zone placeholder representation. More accurate up-front deposits, but pulls pricing rework into U-16's scope.

**C) Other (describe after the tag).**

[Answer]: A

---

## Artifact output (generated after answers are collected)

- `aidlc-docs/construction/u-16-animals-buildings/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-16-animals-buildings/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-16-animals-buildings/functional-design/business-rules.md`
