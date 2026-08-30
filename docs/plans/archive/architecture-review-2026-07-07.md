# Archived: Architecture review follow-ups (2026-07-07)

A pathing-and-efficiency architectural review produced eight work items (#0–#7). All were
implemented on **2026-07-07**; the solution built clean and **643** unit tests passed. New Core
surfaces (`GridPathfinder`, batch NN ordering, `DepositStop` ordering, segment sweep) came with
unit tests.

Original plans: `architecture-review-index.md` (the index), `architecture-doc-refresh.md` (#0),
`core-pathfinding-and-passability-cache.md` (#2), `travel-aware-batch-ordering.md` (#3),
`work-activity-abstraction.md` (#4), `deposit-trip-ordering.md` (#6), `pathing-polish.md` (#7).

Item **#5 (time-aware wrap-up)** is *not* archived here — only its measure-only Phase 0 shipped, so
it remains an active plan in [`../time-aware-wrapup.md`](../time-aware-wrapup.md).

Item **#1 was not a code item.** It was a gate: clear the pending in-game smoke-pass backlog before
stacking structural refactors on top of unverified refactors. That backlog (travel consolidation +
the 2026-06-10 orchestrator decomposition, serpentine sweep routing, the machines cross-location
fetch + auto-grabber additions, Manage Fish Ponds) all passed on 2026-07-07, opening the gate for
#2 and #4.

---

## #0 — Architecture doc refresh: the review's own premise was wrong

**Why:** stale doc claims mislead every later task, so this ran first.

The review claimed `docs/architecture.md`'s "throttled to every 4th tick" was drift and that no such
throttle existed. **Inspection found the throttle does exist** — an explicit
`if (++Session.TickCount % 4 != 0) return;` in `ShiftOrchestrator.OnUpdateTicked`. The reviewer had
grepped for SMAPI's `IsMultipleOf` helper and missed the raw `% 4` modulo: the observation was
technically true and the conclusion inverted.

So the doc was made **precise** rather than corrected. It now spells out both rates — tool animation,
the movement driver, and the debris pump run *every* tick; progress sampling / stuck detection, hit
reaction, shopping and idle waits, travel handling, and intent dispatch run on *every 4th* — and
notes that it is a raw modulo, not `IsMultipleOf`, so a future grep doesn't repeat the miss.
Adjacent claims (the `ShiftPhase` list, spawn flow, stop conditions) were spot-checked; no other
drift was found.

Two downstream corrections fell out: #5's "walk speed is per-tick" assumption was confirmed sound,
and #2's perf argument was clarified — BFS work-selection runs on the throttled ticks, but
throttling only caps selection *frequency*, it doesn't make a selection cheaper.

## #2 — Core pathfinding extraction + per-shift passability cache (the keystone)

**Why, two problems in one:**

1. **Perf.** Every work-item selection called `WorkerMovementDriver.ComputeRouteCostsFrom` — a
   whole-map BFS where every tile probe went through `IsTilePassableForWorker` →
   `location.isCollidingPosition`, which iterates buildings, resource clumps, furniture, and
   characters *per probe*. On an 80×65 farm that is ~5,200 collision probes in a single tick, once
   per work item, hundreds of times per shift: the likeliest frame-hitch source on large modded/SVE
   farms. Seven call sites all paid full price on every call.
2. **Testability.** All of this graph logic lived in `Dayswork/` solely because it needed
   `GameLocation` for the passability probe — so BFS correctness, route reconstruction, and cost
   maps had **no unit tests at all**.

**What was decided:**

- Extract the algorithms to `Dayswork.Core/Pathing/` behind an `IPassabilityView` seam
  (`PassabilityGrid`, `GridPathfinder`), lifted verbatim from `WorkerMovementDriver`. **BFS
  neighbour order (cardinal N,E,S,W) and first-wins tie-breaking are load-bearing** for both route
  selectors and were preserved exactly.
- Cache grids per shift on `ShiftSession` (`LocationPassabilityCache`), keyed by `NameOrUniqueName`
  — a fresh session is the reset. Invalidated three ways: the worker's own clears (footprint
  re-probe), SMAPI object/terrain-feature/furniture/building list-changed events, and a
  stuck-recovery invalidate before recovery-tile selection.
- **Staleness tolerance is what makes coarse invalidation acceptable.** The system already tolerates
  a wrong reachability answer: navigation failure defers the work item, deferred items retry, the
  stuck detector is the terminal backstop. A stale grid degrades to today's behavior — never to item
  loss or a stranded worker.
- The **live** probe is retained where a cache must not be trusted: `StartNavigation`'s validation
  of a vanilla `PathFindController` path, and rare single-tile spot checks.

**Hard rule 7 resolution (recorded in `docs/pathing.md`):** with `character: null`,
`isCollidingPosition` skips the animal loop entirely — **FarmAnimals were never in the probe's
answer**, so they need no special handling and no live-probe exemption. The inset-rect `+1/62`
corner math transfers unchanged because the grid is built from the same probe.

**Deferred:** phase 2 (targeted A* for sweep categories, where `SelectionKey = StableOrder` means
the flood fill is consulted only for reachability and stand-tile choice) — build only if profiling
says the remaining cost matters.

## #3 — Travel-aware batch ordering

**Why:** `ShiftPlanBuilder` ordered batches *within* a category alphabetically by location name.
Category priority itself is player-authored and correct — but within a category the worker could
visit Barn → Coop → Slime Hutch in *name* order even when that walks the farm's diagonal twice. For
a multi-building animal or machine contract, batch order dominates total walking distance far more
than any per-tile routing improvement. This was called out as likely the single largest visible
reduction in in-game walking time.

**What was decided:**

- Keep the planner pure (hard rule 1): the orchestrator resolves anchors at `StartShift` and passes
  them in as an **optional** `BatchOrderingContext`. Null ⇒ today's alphabetical order — which is
  the regression signal, since every existing planner test passes unchanged.
- Nearest-neighbor chained from the worker's spawn tile, **Manhattan on anchors**. Route cost via
  the cached grid is a later refinement, explicitly *not* a prerequisite. Ties break by
  location-name ordinal, and a missing anchor sorts last in name order — deterministic degradation.
- **Structural orderings deliberately preserved:** an AnimalBuilding batch and its OutdoorAnimals
  batch move as one pair (the "fully service one building before the next" grouping), FarmForage
  stays last within AnimalCare, and the fixed Crops phase sequence (managed non-farm → greenhouses →
  managed farm → outdoor crops → FarmCave) is untouched. NN applies only where today's order was
  alphabetical-arbitrary.
- Batch *contents* and scanning are untouched — this reorders skeletons only.

**v1 omission:** expansion-location anchors, which fall back to name order — matching the plan's
"fall back if the route doesn't validate" degradation.

## #4 — Work-activity abstraction, built as a handler list rather than the sketched design

**Why:** every activity the worker performs shares one lifecycle — pick next step → navigate →
on-arrive perform → advance; on nav-failure skip or defer — but each was wired in by hand.
`HandleMovement` was two priority-ordered if-chains (arrival + failure), `ShiftSession` carried
parallel per-feature step state whose mutual exclusion was maintained by convention only, and
`TravelPurpose` dispatch was a second switch that had to know every activity's continuation method.
Machines and fish ponds had each added a branch to *every* dispatch point, and the next feature
would too. With 5+ concrete implementations of the same shape, this cleared the no-ceremony bar.

**The design divergence, and why:** the plan sketched a single `ShiftSession.ActiveActivity`. What
was built is an **ordered `IWorkActivity` handler list** — `HandleMovement` walks it, each activity
either consumes the event or defers to the next, exactly the return-vs-fall-through of the old
chain in the same order, with `BatchWorkActivity` terminal.

The single-`ActiveActivity` form requires an activity lifecycle (set at batch begin, cleared at end)
and assumes the modes are strictly mutually exclusive — which means the old chain's "no reachable
step ⇒ fall through" tail would have to be **proven dead**. That proof needs in-game play-testing
that wasn't available in the session. The handler-list form is a mechanical 1:1 extraction (each
handler holds the former branch verbatim, order preserved), so it is behavior-identical *by
construction* and needed no such proof.

Activities are nested private classes, so they reach the orchestrator's private step methods without
widening any surface. Per-shift step state stays on `ShiftSession`.

**Sequencing constraints recorded:** land this before the next "Manage X" feature (which now adds
one list entry instead of a branch in two dispatch points), and land it before #5, because the
abstraction restructures the dispatch the wrap-up gate hooks into — the other order means writing
the gate twice.

**Deferred:** folding the `TravelPurpose` switch into per-activity continuations (explicitly a
later, separate change — the switch can coexist with activities indefinitely), and moving step state
off `ShiftSession` into the activities. Both add risk with no added verification.

## #6 — Cross-location deposit trip ordering

**Why:** `DepositPlanner.Plan` ordered deposit trips nearest-neighbor via an injected
`Func<TileCoord,TileCoord,int>` (the orchestrator passed Manhattan). But chest destinations can live
in building interiors and — since the 2026-07-06 cross-location machine-chest work — other locations
entirely. Manhattan between a farm tile and a shed-interior tile compares unrelated coordinate
spaces, so trip order could be arbitrary: farm-chest → shed-chest → farm-chest zig-zags whenever
interior chests were in the plan.

**What was decided:** keep the planner pure and make the *metric input* location-aware, rather than
teaching the planner about locations. `DepositStop(LocationName, Tile)` replaces the bare
`TileCoord` at the seam; `OrderNearestNeighbor` keeps the same greedy algorithm and tie-break.
The game-side metric is a two-level composite — `hops * 10_000 + Manhattan(door tiles)`. K = 10,000
dwarfs any intra-location Manhattan, so same-location chests always group and no farm→interior→farm
zig-zag is possible. Unresolvable locations sort last via a large sentinel: the intended degradation
for expansions without a farm warp.

The plan's open question — should two chests in the *same* building be forced adjacent even when the
greedy chain wouldn't pick them consecutively? — **is answered by the K hop cost**, and was verified
with a test rather than special-cased.

Trip *execution* (`DepositTripRunner`) was untouched; the change is a permutation of the same trips,
so hard rule 4 (stacks, consolidation keys, retained/eager semantics, overflow fallback) is
unaffected.

## #7 — Pathing polish: gates, sweep segments, string-pulling

Three independent sub-items. (a) had no dependencies and fixed a real bug; (b) and (c) needed #2's
grid.

**(a) Gates — a real behavior bug.** `OpenGatesAlongRoute` toggled **every** gate on the route the
moment the path was planned, so a gate 40 tiles ahead visibly opened before the worker was anywhere
near it. Worse, `TryCloseGate` only fired for waypoints actually *reached* — so any route cleared
mid-walk (work re-selection, stuck recovery, travel cancel, `Clear()`) left already-opened gates
open. Per `docs/fences-and-gates.md` the vanilla auto-close rule runs in `updateWhenCurrentLocation`
only, so an **off-screen leaked gate stays open indefinitely**.

The driver now records the route's openable gate tiles, opens each lazily as it becomes the next
waypoint (matching how a player reads the animation), tracks what it opened, and closes every
tracked gate it hasn't walked through on `Clear()` / navigation completion / `WarpWorker` —
excluding the worker's own tile, since closing a gate onto the worker would trap or clip it.
`toggleGate` occupant behavior was verified against `docs/fences-and-gates.md` (the NPC worker isn't
collision-blocked, so this is safe). Close-behind on waypoint pass was preserved.

**(b) Sweep segments.** `SerpentineSweep.Rank` was pure tile geometry, so a row bisected by a pond,
fence line, or building forced the worker to detour around the obstacle and come back for the row's
far half — repeatedly, once per affected row. This is the cheap intermediate ahead of the deferred
stand-coverage work. `Rank` gained an optional passability predicate (Core purity preserved via the
predicate seam) that splits each row into contiguous-reachable segments and greedy-nearest-end
chains them. **No predicate ⇒ exactly today's output**, which is the regression-safe default.

**(c) String-pulling — deferred by this plan's own condition.** The plan scoped it as "last, and
only if the staircase walking actually bothers anyone once (a)/(b) are in" — a play-observation
gate. It is purely cosmetic (vanilla NPCs also walk 4-directional staircases) and would add a
live-grid dependency to the movement hot path; building the supercover clearance function before
there is a consumer would be speculative code against the no-ceremony rule.

## Cross-cutting invariants held across every item

- **Walk-only:** no item introduced a new warp path.
- **Route selector parity:** BFS cardinal order and first-wins tie-breaking unchanged. #7(b) changes
  intra-batch visit order by design, but only within the sweep's own ordering contract.
- **Energy and pricing untouched.** Every item affects *where the feet go* or *in what order*,
  nothing else.
- **Item routing (hard rule 4) untouched**, including in #6, whose entire surface is deposit ordering.
- **Determinism:** same inputs ⇒ same plan. Tests depend on it, and so does debuggability.

## Explicitly not planned

An **up-front contract feasibility estimate** — a "this scope likely exceeds one shift" hint at hire
time — was reviewed on 2026-07-07 and **declined by the user**. Recorded here so it isn't
re-proposed.

## Open threads carried out of these plans

Deferred by decision, not blocked. None is scheduled.

- **Passability cache phase 2:** targeted A* for sweep categories, keeping the flood fill as
  fallback. Gated on profiling showing the remaining cost matters.
- **Cost-buffer pooling** (per shift or per location size class?) and whether
  `ResolvePassableNearby` / exit-tile resolution should migrate to the grid — both left open;
  migrating the rare live probes is cleanup, not perf.
- **Work-activity:** fold the `TravelPurpose` switch into per-activity continuations; move per-shift
  step state off `ShiftSession` into the activities. Also open: whether `BatchSelectionAttempts`-style
  loop protection is per-activity or stays a shared session guard (leaning shared — it protects the
  orchestrator, not the activity).
- **String-pulling (#7c):** the design stands; waiting on the staircase walking being observed to
  matter in play.
- **Batch ordering:** expansion-location anchors (currently name-order fallback); whether the *last*
  batch should get a small "end the day near home" weight (suggested no for v1 — measure first);
  cross-category chaining (deferred — category boundaries usually imply a deposit or location change
  anyway).
