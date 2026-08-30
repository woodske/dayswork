# Archived: Manage Machines & Manage Fish Ponds

Two plans for the "worker services a producer and routes its output" family. Machines shipped first
(2026-06-19, smoke pass passed 2026-06-28); fish ponds were spun out of it as a deliberately separate
subsystem (2026-06-23, smoke pass passed 2026-07-07). Both are release-ready.

Original plans: `machine-management.md`, `fish-ponds.md`.
Game-content reference: [`docs/machines.md`](../../machines.md) — verified `Data/Machines` schema +
API and, in its "Fish ponds" section, the `FishPond` API, both for SDV 1.6.15.

---

## Manage Machines (2026-06-19)

**Why:** the farmhand learns to operate **machines** — placed objects that turn input into output
over time (mayonnaise machine, keg, preserves jar, bee house, fish smoker, dehydrator, furnace,
tapper). Each shift the worker *visits* each selected machine to collect finished output and reload
empty ones with input fetched — physically — from a chest. "Machine" is the game's own term
(`Data/Machines`, `MachineData`).

### Design decisions (locked)

1. **Visit model, not tool model.** Machine work is a per-machine *visit* doing up to two things:
   collect ready output, then reload if empty. Paced like animal care (interaction beats, no tool
   swing), gated by machine ready-state, inherently async across days. Collect-before-reload within
   one visit falls out naturally — you never collect what you just loaded.
2. **Per-instance selection**, by `(location, tile, expectedMachineId)`, re-resolved at shift start;
   if a machine moved, vanished, or changed type it is skipped with a dev log, exactly like
   managed-crop missing-tile skips. The cost — rearranging a machine means re-selecting it — was
   accepted.
3. **Map selection supports click *and* drag**, extending `ZoneDrawMenu`, with a **location
   switcher** so one selection session can span farm + sheds + greenhouse.
4. **Groups are the config dimension; batches are the location dimension — orthogonal.** A group
   bundles `{machines, input filter, input chest, output destination, mode}`; a machine belongs to
   exactly one group; a group may span locations. At plan time machines bucket by location.
5. **Input is a physical fetch trip**, *not* the abstract-pool shortcut crops use. This makes input
   and output symmetric (both physical), honoring the mod's "the farmhand walks for everything"
   philosophy. Crops' abstract seed consumption is a known pre-existing shortcut; retrofitting it was
   deliberately deferred rather than bloating this feature.
6. **One input chest per group**, player-chosen, any location.
7. **Per-group mode:** collect+reload (default) or collect-only. No-input machines (bee houses,
   tappers, crystalariums, `AllowLoadWhenFull`) are inherently collect-only and ignore the toggle.
8. **Machines is a first-class reorderable `TaskCategory`** (4th), alongside AnimalCare / Crops /
   Fieldwork.
9. **Energy:** collect = 1, load = 1, **per machine interaction, not per item** — a 5-fruit
   dehydrator load is one charge. Pricing is **gate-only, no surcharge** — decided with the user:
   energy is the cost.

### The group editor redesign (2026-06-19)

The per-group editor was reworked mid-plan into a **type-first, gated** flow, superseding the
original chest-derived input flow: pick a machine **type** first (so a group is single-type), then
select machines *restricted to that type*, then choose inputs from the **type's** accepted-input list
— data-derived via `MachineReader.EnumerateAcceptedInputs`, **not** from chest contents — with
"Any &lt;category&gt;" bulk shortcuts and **required companions (coal) shown auto-selected and
locked**. Then input chest (decoupled from the filter; the worker skips it at runtime if the material
is absent), then output destination.

Changing the type clears the group's machines and input filter, since both are type-scoped. No
migration was needed — the feature was unreleased.

### Additions after v1

- **Flavored inputs carry the real item (2026-06-26).** `ShiftSession.CarriedInputs` holds the real
  withdrawn chest items, and the acceptance probe uses a real chest sample rather than a flavorless
  `ItemRegistry.Create(id)` rebuild — so a Preserve Jar fed flavored roe produces **Caviar** /
  correctly-flavored **Aged Roe**, not generic output.
- **Per-group, fetch-first, single-visit cycle (2026-06-28)**, superseding the original
  two-pass collect-all-then-reload-all model. A batch services groups one at a time as a full cycle;
  within a group the worker walks to the input chest **first**, withdraws, then visits each machine
  **exactly once** (collect, then immediately reload). Groups are planned lazily so a shared input
  chest reflects earlier groups' withdrawals.
- **Auto-Grabbers as input chests (2026-07-05).** A grabber's `heldObject` chest of collected animal
  products feeds reload machines. Grabbers stay out of output/deposit pickers.
- **Input chest in any location (2026-07-06)**, lifting the original "same location as the machines"
  limit. A cross-location chest becomes a fetch **excursion** routed through the farm hub like a
  deposit trip; building↔building routes X→farm→Y because buildings connect only via the farm. Every
  hop uses `TravelFailurePolicy.WarpToDestination` so the worker is never stranded, and an
  unreachable, missing, or busy chest degrades that group to **collect-only** and returns the worker
  to the machines — inputs are never lost.

### Verified in the smoke passes

Per-entry recipes load and collect correctly in-world (fish smoker fish+coal, dehydrator ×5, keg/jar
flavoring). `checkForAction` cleanly hands held output to the fake worker `Farmer` and the
`OutputCollected` re-trigger fires without a real player present. Filtered loads honor the filter via
probe-then-commit `PlaceInMachine`. The optional `Contract.MachineScope` field is back-compatible —
no save version bump; old saves load with an empty scope.

### Accepted v1 imperfection

Sleep-settle of *collected machine output* still sitting in the buffer routes by the buffer's nominal
task tag rather than by provenance — the same minor imperfection managed crops have. Items are never
lost, so this was accepted for v1.

### Explicit non-goals (v1)

Auto-buying machine inputs; incubators (`IsIncubator`); crystalarium reloading; reload-only mode;
carry-capacity caps (the worker carries unbounded, like the shopping trip); moving or placing
machines.

### Risks recorded at plan time

The cross-interior map UI was the largest *new* UX surface, and was deliberately contained inside
`ZoneDrawMenu` rather than becoming a new menu framework. `MachineInputPlanner` correctness (atomic
multi-input, "any"-tag matching, partial supply) was called the biggest correctness risk — hence it
lives in Core with unit tests. "Input never lost" was a *new* invariant surface (carried inputs on
stop) and was treated with the same rigor as output overflow. Exotic modded `InteractMethod` /
`OutputMethod` machines may still need collect-only fallbacks.

---

## Manage Fish Ponds (2026-06-23)

### Why a separate subsystem, not folded into Manage Machines

Decided with the user on 2026-06-23. A fish pond shares only the *collect → route to a destination*
half of Manage Machines, and almost nothing else:

| | Machines (`Data/Machines` objects) | Fish ponds (`Building`s) |
|---|---|---|
| Lives in | `location.objects` | `location.buildings` |
| Identity | `(loc, tile, qualifiedItemId)` | `(loc, tileX/tileY)` — **no item id** |
| Ready check | `readyForHarvest` + `heldObject` | `output.Value != null` |
| Collect | `checkForAction` on an `Object` (needs a guarded fake `Farmer`) | take `output.Value`, null it — **no fake Farmer** |
| Reload / input | the whole reader + planner + fetch trip + carry buffer | **none** — the player stocks the fish |

`MachineReader` is built entirely on `Object.GetMachineData()` / `location.objects`, so a pond cannot
be resolved through it at all. Ponds became a **parallel collect-only subsystem** mirroring the
existing parallel-scope pattern (`ManagedCropWorkScope`, `MachineWorkScope`) — not a machine special
case.

### Decisions (locked)

1. **Own hub spoke** ("Manage Fish Ponds"). The machine group editor's input-chest/type/companion
   flow is input-centric and would be empty and confusing for ponds. The pond page is just *select
   ponds on the map, pick one output destination*.
2. **Own `TaskCategory.FishPonds`** (5th, reorderable) — consistent with the parallel-scope design.
3. **Collect-only.** The player stocks the fish and supplies any capacity-quest item; both are out of
   scope. No input/reload/fetch machinery.
4. **One output destination for the whole scope**, not per-pond.
5. **Energy:** collect = 1 per pond visit, mirroring the machine collect charge. Gate-only pricing,
   no surcharge — same as machines.
6. **Direct-field collect.** Capture `pond.output.Value`, null it, credit the deposit buffer. This
   avoids the player-inventory / HUD / xp side effects of `FishPond.doAction`, so **no action guard
   is needed** — simpler than machine collect. It is duplication-safe because the buffer is credited
   only *after* the field is nulled.

### Output routing nuance (differs from machines)

`FishPondOutputRouter.BuildDestinationMap` **always** maps the pond provenance, including the
Automatic case. `MachineOutputRouter` omits Automatic and lets the deposit planner fall back to the
buffer's per-task destination — but for ponds that fallback could mis-route output to an unrelated
task's chest, because the buffer uses `TaskKind.HarvestCrops` as a nominal tag. Always-mapping the
provenance closes that hole.

---

## Flavored + quality preservation (2026-06-23) — the cross-cutting outcome

Started as a fish-pond concern and ended up backing hard rule 4 for the whole mod.

Pond roe is usually a `ColoredObject` carrying a `PreserveId` (fish flavor) plus a color, whose
identity and sell price (Sturgeon Roe ≫ plain Roe) **cannot be reconstructed from `(O)812` alone**.
The deposit pipeline was reworked so it never tries: a per-shift `FlavorItemRegistry` captures the
real item at collect time under an opaque `FlavorId`; the pure buffer and planner carry only that
token (consolidated into the key, so distinct flavors never merge); and the three deposit
reconstruction sites — chest, shipping bin, automatic overflow — **clone the captured template** via
`getOne()` instead of calling `ItemRegistry.Create`. Plain items are unaffected: a null token takes
the ordinary id path.

This also fixed **flavored machine output** — blueberry wine, aged roe, flavored honey.

**Quality** became a first-class field through the same chain. `getOne()` does *not* copy quality, so
it is re-applied per stack at rebuild from the captured value, and both collect sites now capture it.
That closed a pre-existing gap: **cask-aged silver/gold/iridium wine and cheese now keep their star.**

## Follow-ups carried out of these plans

- **Casks (cellar)** — the remaining machine-family phase: quality-aging "ready" semantics and cellar
  travel. Verify the cask state model first.
- **Retrofit managed-crop seed consumption to physical fetch trips**, making crops symmetric with
  machines (decision 5 above).
