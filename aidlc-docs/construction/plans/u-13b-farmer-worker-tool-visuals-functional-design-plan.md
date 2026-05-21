# U-13B — Farmer Worker + Tool Visuals: Functional Design Plan

**Unit**: U-13B — Farmer Worker + Tool Visuals
**Stories**: S-07 (completes — Farmer worker + visible tool-swap when changing task class)
**Phase**: CONSTRUCTION — Functional Design
**Runs**: immediately after U-13 (before U-14), per the 2026-05-21 split.

---

## Plan Checklist

- [x] FD-Q1–Q6: Collect answers to design questions below ([Answer]: tags) — all answered **A**
- [x] Resolve any ambiguities / vague answers — none; all six are clear letter choices and mutually consistent (lowest-risk path + immersion-friendly appearance)
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [ ] Present completion message and await approval

---

## Context Summary

U-13B re-founds the worker on `StardewValley.Farmer` so it **visibly uses tools the way the player does** (real axe swing, watering-can pour, scythe sweep, pickaxe strike) and completes S-07 with the tool-swap animation. It replaces U-13's `NPC` + native `PathFindController` walking with a `Farmer` + custom movement + custom rendering. **All U-13 behaviour (priority queue, capability/skip rules, stuck escalation, invulnerability emote) must be preserved.**

This is the highest-uncertainty unit in the project — it was deliberately split off U-13 so that a play-test problem here cannot entangle the already-validated worker-AI logic.

### What already exists (U-10 → U-13) that U-13B modifies or replaces

- **`Dayswork/Worker/FarmhandNpc.cs`** — an `NPC` (recolored Marnie). **Superseded** by `FarmhandWorker` (a `Farmer`).
- **`Dayswork/Worker/PathFindControllerAdapter.cs`** — drives the NPC via `npc.controller = new PathFindController(...)` and lets `NPC.update()` tick the walk. **Superseded** by `WorkerMovementDriver` (manual path-follow; a `Farmer` has no NPC update/controller loop).
- **`Dayswork/Orchestration/ShiftOrchestrator.cs`** — heavily coupled to the NPC: `farm.addCharacter(_farmhand)`, `_farmhand.TilePoint`, `_farmhand.doEmote(...)`, `Game1.warpCharacter(...)`, and `_nav` (the adapter). Each of these touchpoints must be re-pointed at the Farmer + new movement driver. The work-list build, skip rules, stuck escalation, deposit/exit flow, and hit-reaction watcher logic stay the same — only the entity/movement/draw seams change.
- **`Dayswork/ModEntry.cs`** — `OnAssetRequested` redirects `Portraits/DaysworkFarmhand` to Marnie (NPC-only; **dropped** under a Farmer). A `Display.RenderedWorld` subscription is **added** for the worker renderer.
- **`Dayswork/Worker/ObjectTargetClassifier.cs`** — unchanged (pure classification).
- **Core `Shifts/` + `Capabilities/`** — unchanged. U-13B adds **one** new Core type: the `WorkerTool` map (task class → tool kind), introduced here.

### Components owned by U-13B (from unit-of-work.md ownership matrix)

- **M-10 ToolSwapAnimator** (`Dayswork/Worker/`) — tracks the worker's currently-held tool class and triggers the correct one-shot swing animation when the task class changes / on each task action.
- **`WorkerTool` map** (Core, `Dayswork.Core/`) — pure mapping from `TaskKind` → tool class (Axe / WateringCan / Scythe / Pickaxe / None) and the verified animation frame sets. Pure, testable, no Stardew refs.
- **FarmhandWorker** (a `Farmer`, supersedes M-09 `FarmhandNpc`).
- **WorkerMovementDriver** (supersedes M-11 `PathFindControllerAdapter`).
- **WorkerRenderer** (depth-aware draw via render hook).
- **WorkerAppearance + WorkerAppearanceRandomizer** (randomized character-creation appearance).

### Play-test note carried from U-13 (drives FD-Q1)

At U-13 approval the user reported: *"The worker is just standing there now in a grass field."* The NPC `PathFindController` appears to stall on the first work item (no progress, stuck detection not rescuing it). Because U-13B replaces the entire movement system, this is expected to be resolved here — **reliable arrival at each work tile is an explicit Definition-of-Done item for U-13B**, not just the visual upgrade.

---

## Pre-decided design (LOCKED — feed into the artifacts, do NOT re-open)

These were settled during U-13's Functional Design (FD-Q5=B), the U-13/U-13B split, and U-13's NFR Design (TS-U13-04). They are inputs to U-13B, not questions:

1. **Full Farmer**, not a hybrid NPC+Farmer (FD-Q5=B; revises FR-NPC-01 — recorded as DEV-01).
2. The worker `Farmer` is kept **out of** `location.characters` / `location.farmers` and is **never serialized** (BR-WORKER-01/03). Removal-on-`OnSaving` pattern continues.
3. **Manual render hook** (`Display.RenderedWorld`) with Y-depth handling — reject registering in `location.characters` for drawing. (Fidelity detail is FD-Q2.)
4. **Manual movement driver**: compute a route via the game's pathfinding, then step `Farmer.Position` + advance the walk animation each tick. (Path-computation mechanism is FD-Q1.)
5. **Tool swings via `FarmerSprite.animateOnce(int[] frames)`**; the held tool is drawn by `FarmerRenderer`. Verified frame sets (Stardew wiki + FarmerSprite.cs): heavy tools (axe/pickaxe) R12/R9/R7; watering can R10/R5/R8/R11; scythe/melee R5/R6/R7.
6. **Tool mapping** (LOCKED): Water crops → Watering Can; Clear weeds + Clear grass → Scythe/melee; Clear rocks → Pickaxe; Cut trees → Axe; Harvest crops + Collect fruit → no tool (hand pick). *(FD-Q5 only asks how the no-tool tasks animate, not the mapping itself.)*
7. **Randomized appearance** pulled from the character-creation field ranges. *(Stability + scope are FD-Q3 / FD-Q4.)*
8. **Invulnerability**: the hit-reaction is a swing-proximity watcher that plays the "!" emote (Pattern H, already implemented in `ShiftOrchestrator.CheckHitReaction`). A `Farmer` takes no friendly-fire damage, so no damage override is needed — this carries over unchanged. The only open implementation detail is ensuring the emote bubble renders for a manually-drawn Farmer (handled in business-logic, not a question).

---

## Design Questions

### FD-Q1 — Movement driver: how is the path computed?

The driver must move a `Farmer` that is **not** in `location.characters` and has **no** `NPC.update()`/`controller` tick loop. The pre-decided approach is "compute a route, then step `Farmer.Position` manually." The open question is **how to obtain the route**. (U-13's adapter built a `PathFindController` and read its `pathToEndPoint` waypoint stack; for a Farmer we must not let any controller *tick*, but we can still use one purely to compute the path.)

**A) Reuse `PathFindController` for path computation only, drive manually (Recommended).** Construct a `PathFindController(worker, location, endTile, ...)` solely to read its `pathToEndPoint` (a `Stack<Point>` of tile waypoints), then **discard the controller** and walk the `Farmer` waypoint-to-waypoint ourselves (set `Position`, set facing, advance `FarmerSprite` walk animation). Reuses the game's proven A* and matches the waypoint model U-13 already used; lowest new-pathfinding risk. The "standing still" U-13 bug is avoided because *we* own the per-tick stepping rather than relying on the NPC controller loop.

**B) Call lower-level pathfinding helpers directly.** Use a static path API (e.g. `PathFindController.findPath` / `findPathForNPCSchedules`) to get the tile list, then drive manually. Avoids constructing a throwaway controller object, but uses a less-trodden API surface and we must replicate the start/end and passability arguments correctly.

**C) Custom BFS/A* over passable tiles.** Write our own grid search using `farm.isTilePassable(...)`. Maximum control and zero coupling to vanilla pathfinding internals, but re-implements (and must re-validate) routing the game already does well — most code and most risk for a v1.

[Answer]: A

---

### FD-Q2 — Depth-sorted rendering: occlusion fidelity for v1

The worker is drawn in a `Display.RenderedWorld` hook, which fires **after** the world's own draw pass. That means anything we draw there lands **on top of** the finished frame — true interleaving with trees/buildings is not free. How much occlusion fidelity should v1 target? (NFR Design already chose "manual render hook" and retained the BR-WORKER-03 *cosmetic-fallback* note, which leans toward A.)

**A) Self-consistent draw, on top of world; accept foreground over-draw as a v1 limitation (Recommended).** Draw the worker (body + held tool + shadow + emote) at the correct screen position, internally Y-ordered so its own parts layer correctly. The worker may visually render *over* a tree canopy or building edge it walks behind. Documented as the BR-WORKER-03 cosmetic fallback; simplest, no draw-pass patching, lowest risk for the project's hardest unit.

**B) Harmony-inject the worker draw into the location draw pass for true occlusion.** Patch the location/farm draw so the worker is drawn mid-pass with a Y-based `layerDepth`, correctly going behind foreground objects. Best visual result, but adds an invasive draw-pass Harmony patch to the riskiest unit — more play-test surface and more ways to break vanilla rendering.

**C) `RenderedWorld` hook + manual occluder handling for common cases.** Keep the post-pass hook but special-case the most frequent occluders (large trees, building walls) by clipping/skipping. Middle ground; meaningful extra complexity for partial correctness.

[Answer]: A

---

### FD-Q3 — Appearance: stability across days

The Farmer is never serialized (locked decision #2), so its appearance is regenerated in memory each time a shift starts. Should a given contract's worker **look the same every day** or not?

**A) Deterministic from the contract ID (Recommended).** Seed the randomizer with the contract's ID so a recurring contract's worker has a **stable** appearance day after day, with no serialization needed. Different contracts get different-looking workers. Best immersion (the player recognizes "their" farmhand) for near-zero cost.

**B) Re-roll randomly every shift.** A fresh random appearance each morning. Simplest, but the worker looks like a different person each day — can feel off for a recurring hire.

**C) One fixed appearance for all workers.** No randomization; every worker (across all contracts) looks identical. Most predictable, least variety; contradicts the "randomized appearance" intent (locked decision #7) so listed only for completeness.

[Answer]: A

---

### FD-Q4 — Appearance: which fields are randomized

Within the character-creation field ranges, what gets randomized for the worker's look?

**A) Full randomization (Recommended).** Randomize gender/body, skin tone, hair style + colour, shirt, pants, accessory, and eye colour across the vanilla character-creation ranges. Maximum variety; each worker reads as a distinct person.

**B) Randomize body/hair, fix a neutral work outfit.** Randomize gender/body, skin, hair, eyes — but pin a fixed, plain shirt/pants ("work clothes") so workers look like hired labour rather than colourful villagers. Slightly more "uniform" feel.

**C) Other.**

[Answer]: A

---

### FD-Q5 — No-tool tasks (Harvest crops / Collect fruit): how do they animate?

The tool mapping (locked decision #6) gives these tasks **no tool**. What does the worker do during a hand-pick action?

**A) Brief hand-pick / face-and-pause (Recommended).** The worker faces the target tile and pauses briefly while the harvest resolves (no tool drawn, no swing) — mirrors how vanilla hand-harvesting reads. Clear visual that work is happening, no swing artifact.

**B) No animation.** The harvest resolves with the worker simply standing (idle frame) for the action's duration. Simplest; slightly less readable.

**C) Other.**

[Answer]: A

---

### FD-Q6 — Tool-swap timing when the task class changes

When the worker moves from, say, watering to chopping, does swapping tools cost any visible time?

**A) Instant swap (Recommended).** The held tool changes immediately; the new tool simply appears on the next swing. `ToolSwapAnimator.OnTaskChanged(prev, next)` just updates which tool is drawn — no equip delay. Keeps the work loop tight and avoids interfering with stuck detection / the 8pm cap.

**B) Brief visible equip pause (~0.3–0.5s).** On a task-class change the worker pauses momentarily ("reaches for" the new tool) before the first swing. More characterful, but introduces a small non-working interval that the stuck/cap logic must treat as progress.

**C) Other.**

[Answer]: A

---

## Artifact output (after answers collected)

- `aidlc-docs/construction/u-13b-farmer-worker-tool-visuals/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-13b-farmer-worker-tool-visuals/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-13b-farmer-worker-tool-visuals/functional-design/business-rules.md`
