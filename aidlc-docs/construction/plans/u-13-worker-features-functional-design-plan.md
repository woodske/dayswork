# U-13 — Worker Features (Priority + Stuck + Tool Swap + Invulnerability): Functional Design Plan

**Unit**: U-13 — Worker Features: Priority + Stuck + Tool Swap + Invulnerability
**Stories**: S-07 (completes — tool-swap visuals), S-08 (completes — full priority queue + trellis-side harvest + not-ready skip), S-09 (completes — full capability matrix + skip rules + tool-missing warning queued), S-16 (3-step stuck escalation), S-17 (invulnerability + ouch emote)
**Phase**: CONSTRUCTION — Functional Design

---

## Plan Checklist

- [x] FD-Q1–Q6: Collect answers to design questions (Q1=A, Q2=A, Q3=A, Q4=B, Q5=B, Q6=A)
- [x] Resolve any ambiguities from answers (FD-Q5 reworked around full Farmer; implications confirmed)
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [ ] Present completion message and await approval

---

## Context Summary

U-13 takes U-10's thin worker ("walks to one tile, does one task, distance-ordered") and makes it a real worker.

**Components owned (new files)**:
- **C-09 StuckDetector** (`Dayswork.Core/Shifts/`) — pure Core: `RecordTick(bool madeProgress, int minutesElapsed)`, `ShouldFireStuck()`, `Reset()`.
- **M-10 ToolSwapAnimator** (`Dayswork/Worker/`) — `OnTaskChanged(prev, next)`, `Draw(b, worldPos)`.

**Components extended**:
- **C-08 ShiftStateMachine** — adds **Stuck** and **Recovering** states + transitions (FR-WORK-11/12).
- **M-09 FarmhandNpc** — overrides `takeDamage` → returns 0 + ouch emote (FR-NPC-02).
- **M-12 ShiftOrchestrator** — wires `StuckDetector`, `TaskPriorityOrderer` (C-07), `CapabilityEvaluator` (C-06), full skip-rule branches, and `ToolSwapAnimator`.

**What already exists (from U-10) that U-13 builds on**:
- `ShiftStateMachine` uses a strict **single-successor** transition table (WaitingForSpawn → Working → Depositing → Exiting → Done) with intent-carrying active phases. Illegal transitions throw (PBT-protected).
- `ShiftOrchestrator.BuildWorkList` currently sorts the open-farm work list **purely by Manhattan distance** from the farm entrance — it does **not** yet apply FR-WORK-03 task priority. `TaskPriorityOrderer` (C-07) exists but is unused.
- `CapabilityEvaluator` (C-06) + `CapabilityMatrix` exist (`CanChop`/`CanBreak` with axe/pickaxe thresholds; fruit trees hard-excluded) but are **unused** by the orchestrator. `DetectTask` currently matches only basic `"Stone"` for rocks and notes ore/boulder tool gates are "U-13 scope".
- `ToolSnapshot` is captured at 6am (Axe / Pickaxe / WateringCan levels) but no work-list filtering is applied yet.
- `FarmhandNpc.takeDamage` is **not** overridden (invulnerability deferred to U-13 per a code comment).

**Already decided / not in scope for questions**:
- FR-WORK-03 priority order is fixed: Feed animals → Pet animals → Collect animal products → Water crops → Harvest crops → Collect fruit → Clear weeds → Clear grass → Clear rocks → Cut trees.
- FR-SKIP-03 fruit-tree always-skip is already enforced (`CapabilityMatrix.CanChop` + `DetectTask` matching `Tree` not `FruitTree`).
- Stuck thresholds default to 10 in-game minutes each (initial + post-teleport), already present in `ConfigSnapshot` (`StuckInitialThresholdMinutes`, `StuckPostTeleportThresholdMinutes`). GMCM exposure of these is U-16.
- The tool-missing **mail** itself is delivered by U-14's `MailDispatcher`; U-13 only **queues/records** the warning intent (the `QueueMail` intent / a pending-warning set). No mail is actually sent in U-13.
- The early-end-shift deposit path (stuck step 3) deposits to the **shipping bin** only in U-13 (multi-chest deposit + overflow mail land in U-14). Refund computed per FR-PAY-05 from actual hours worked.
- Throttled-tick (every 4th tick) and Invoke-and-Poll task patterns from U-10 are retained.

---

## Design Questions

### FD-Q1 — Animal tasks & building-interior navigation: include in U-13 or defer?

This is the scope-defining question for U-13. Story S-08's "full 10-task priority queue" names all 10 task types — but three of them (**Feed animals, Pet animals, Collect animal products**) currently have **no tile detection and no action invocation** in `ShiftOrchestrator`, and the U-12 play-test confirmed the worker **cannot enter buildings** (coop/barn/greenhouse): navigation always runs against the outdoor Farm map, so indoor tiles fail pathfinding and are silently skipped. Making animals work requires both indoor navigation (FR-WORK-09: walk to building door → warp inside) *and* the three animal task actions — a sizeable, distinct chunk of work.

**A) Defer animals + building interiors to a dedicated future unit (Recommended).** U-13 delivers full FR-WORK-03 priority **ordering across all 10 task types** (so the queue is correct), plus stuck recovery, tool swap, invulnerability, and full skip rules — but **only for the 7 outdoor task types** that already have detection/invocation (Water, Harvest, Collect fruit, Clear weeds/grass/rocks, Cut trees). Animal tasks and building-door warp navigation become a new unit (e.g., "U-13.5 / U-17 Animals & Buildings"). Keeps U-13 focused on its named component ownership (StuckDetector, ToolSwapAnimator) and avoids coupling the worker-AI work to a hard pathfinding problem. The TODO is logged in `aidlc-state.md`.

**B) Include everything in U-13.** Add building-door warp navigation (FR-WORK-09), indoor tile scanning that actually pathfinds, and Feed/Pet/Collect-animal-products invocation. U-13 becomes a much larger unit covering all 10 tasks end-to-end indoors and outdoors.

**C) Middle ground — buildings yes, animals no.** Add building-interior navigation (so greenhouse crop watering/harvest and indoor weed/grass clearing work via door warp) but **defer the animal-specific tasks** (Feed/Pet/Collect animal products) to a later unit. Captures the greenhouse case players will hit most, without the animal-care surface.

[Answer]: A

---

### FD-Q2 — How to model the Stuck and Recovering states in the state machine

The current `ShiftStateMachine` has a strict single-successor table (each phase maps to exactly one legal next phase). FR-WORK-12's 3-step escalation needs richer transitions: Working → (stuck) → emote → teleport to next reachable tile → **resume** Working; and if stuck **again** in the next window → teleport home → **end shift** (deposit + exit).

**A) First-class phases + multi-successor table; orchestrator owns the escalation count (Recommended).** Add `Stuck` and `Recovering` to the `ShiftPhase` enum. Change the transition table from single-successor to **sets of legal successors**: `Working → {Depositing, Stuck}`, `Stuck → {Recovering}`, `Recovering → {Working, Depositing}`. The "have we already teleported once?" escalation counter lives in the orchestrator (or `ShiftContext`), not the machine. The machine stays pure and keeps its PBT invariants (never leaves `Done`; only listed transitions are legal; illegal transitions throw).

**B) No new phases — model stuck handling as orchestrator sub-state inside Working.** Keep the 5-phase machine unchanged; track a recovery flag + escalation counter entirely in the orchestrator. (Note: contradicts unit-of-work.md, which explicitly says U-13 "adds Stuck and Recovering states + transitions".)

**C) First-class phases + the machine itself owns the escalation counter.** Add `Stuck`/`Recovering` as phases AND give the state machine a recovery-attempt counter so it decides `Recovering → Working` vs `Recovering → Depositing` internally. Richer machine, but it stops being a pure transition table and gains mutable counting state.

[Answer]: A

---

### FD-Q3 — What counts as "progress" for the StuckDetector

`StuckDetector.RecordTick(bool madeProgressThisTick, int minutesElapsed)` is pure; the **orchestrator** decides `madeProgressThisTick`. FR-WORK-11: "no progress toward its current target tile and completes no task work … progress is measured in tile movement or completed task ticks." The worker stands still while performing a task — that must **not** count as stuck.

**A) Tile-movement-or-action (Recommended).** During navigation (`IntentMoveToTile`), progress = the worker's **tile coordinate changed** since the last sampled tick. During a task action (`IntentPerformTaskAt`), progress = **true** (performing work is progress). So stuck only meaningfully accrues while navigating without advancing tiles.

**B) Target-distance-decreasing.** Progress = path/Manhattan distance to the current target tile **decreased** since last tick; task actions exempt (treated as progress). More precise about "toward its target" but sensitive to pathfinder wiggle.

**C) Pixel-movement epsilon.** Progress = worker pixel position moved more than a small epsilon since last tick; task actions exempt. Finest-grained but noisiest (a worker shuffling in place against an obstacle could read as progress).

[Answer]: A

---

### FD-Q4 — Trellis-crop adjacency: which stand tile does the worker path to (FR-SKIP-04)?

Trellis crops (grapes, hops, etc.) sit on a tile the worker cannot stand on, so harvest happens from an **adjacent reachable** tile; if no adjacent tile is reachable, the crop is skipped. When several neighbors are reachable, which one becomes the navigation target?

**A) First reachable orthogonal neighbor (fixed N/E/S/W scan order).** Simple and deterministic; pick the first of the 4 cardinal neighbors that is pathable.

**B) Nearest reachable orthogonal neighbor to the worker's current position (Recommended).** Among pathable cardinal neighbors, choose the one closest to where the worker is now — minimizes back-and-forth travel and keeps the priority/distance ordering coherent.

**C) Nearest reachable neighbor including diagonals (8-neighborhood).** Allows more harvest opportunities (matches that vanilla tool reach can be diagonal), at the cost of slightly more complex reachability checks.

[Answer]: B

---

### FD-Q5 — Worker rendering: authentic player-style tool animations (revises FR-NPC-01)

**Decision driver (player request):** the worker should *visibly use tools the way the player character does* — a real axe swing, watering-can pour, scythe sweep, pickaxe strike — not a tool icon floating next to a walking villager. Randomized appearance pulled from the character-creation options is desired.

**Verified technical findings** (Stardew Valley Wiki — Modding:Farmer sprite; corroborated by FarmerSprite.cs frame data):
- **`NPC` sprite sheets contain no tool-use frames** — only walking + expressions. The current `FarmhandNpc` (recolored Marnie) physically cannot animate tool use; the best an NPC can do is an overlaid icon.
- **`Farmer` sprites do** — `FarmerRenderer` composites body/skin/hair/clothing **and draws the held tool with correct swing frames**. Tool-use rows exist for all four directions: heavy tools (axe/pickaxe) R12/R9/R7; watering can R10/R5/R8/R11; scythe/melee R5/R6/R7. `FarmerSprite.animateOnce(int[] frames)` plays a one-shot swing.
- The game already draws **non-player** Farmers (multiplayer farmhands), so drawing a Farmer that isn't `Game1.player` is supported.
- **Open risk:** our working navigation uses `PathFindController`, which targets `NPC`/`Character`. A `Farmer`'s movement is normally input/network-driven, so pointing PathFindController at a `Farmer` directly is unverified and likely fragile.

That risk shapes the options below — they differ in how much of the working U-10 navigation we keep.

Tool mapping (flag if you disagree): Water crops → **Watering Can** swing; Clear weeds & Clear grass → **Scythe/melee** swing; Clear rocks → **Pickaxe** swing; Cut trees → **Axe** swing; Harvest crops & Collect fruit → **no tool** (vanilla hand-pick — brief grab/no swing).

**A) Hybrid — invisible NPC drives movement, a synced `Farmer` is drawn for visuals + tool swings (Recommended).** Keep `FarmhandNpc` (NPC) for the navigation/collision/update loop that already works through `PathFindControllerAdapter`, but render it invisible. Maintain a parallel `Farmer` instance with **randomized character-creation appearance**, mirror its position + facing to the NPC every tick, draw it via a render hook, and call `FarmerSprite.animateOnce(...)` for the matching tool swing during `IntentPerformTaskAt`. Pros: reuses the proven pathfinding untouched; authentic player-style tool use; invulnerability still works (the invisible NPC keeps its hitbox, so `takeDamage` still fires per FD-Q6); the Farmer is a pure visual layer, so a glitch degrades gracefully. Cons: must keep NPC↔Farmer in sync and exclude the Farmer from save serialization (same removal-on-`OnSaving` pattern the NPC already uses).

**B) Full Farmer — replace the NPC entirely with a driven `Farmer`.** The worker *is* a `Farmer`; we adapt or replace `PathFindController` to drive its movement, plus animation and drawing, directly. Purest model, single entity. Cons: rewrites U-10's working navigation, carries the most integration risk (Farmer movement isn't PathFindController-native), and the invulnerability/damage path differs from the NPC one.

**C) Fallback — stay on the placeholder NPC + tool-icon overlay.** Keep the Marnie NPC and draw the tool sprite near it (no real swing). Ships fastest, no Farmer integration — but does **not** deliver the player-style tool use you asked for. Listed only as a retreat if A proves too costly during code generation.

**Note:** A or B revises **FR-NPC-01** (which assumed a recolored-villager placeholder for v1) toward a randomized Farmer appearance. This will be recorded as a requirements deviation in the functional-design artifacts.

[Answer]: B (full Farmer) — **confirmed 2026-05-20**. User reaffirmed after reviewing the movement / depth-sorting / hit-detection implications and a post-V1 roadmap analysis: an energy bar (`Farmer.Stamina`) and giving the worker its own tools + food (native `Farmer` inventory / tool / eating / buff systems) are Farmer-native and would be very costly to reimplement on an NPC; dialogue is the one feature that's easier on an NPC, but it's additive ("interaction → dialogue box") rather than a foundational rebuild. Net: full Farmer is the right long-term foundation.

#### Design implications of choosing B (full Farmer replacement)

Recorded so the decision is made with full information. B is the cleaner *single-entity* model, but switching off the `NPC` base loses three things the NPC gave us for free, which U-13 must now rebuild:

1. **Movement must be driven manually.** `PathFindController` moves an `NPC` by ticking its walk logic; a `Farmer`'s position/walk animation is normally input- or network-driven, so PathFindController is unlikely to drive a `Farmer` cleanly. U-13 will need a movement driver — either a custom path-follower that sets `Farmer.Position` + advances the walk animation each tick, or an adapter that coerces PathFindController onto the Farmer. This replaces the working U-10 `PathFindControllerAdapter` path.
2. **Depth-sorted drawing is no longer automatic.** An `NPC` in `location.characters` is drawn by the game with correct Y-depth sorting (behind trees/buildings as appropriate). A standalone `Farmer` we draw in a render hook will, by default, render on top of the world. U-13 must either replicate depth sorting or accept the worker drawing over foreground objects in v1.
3. **Hit-reaction for FR-NPC-02 must be detected manually.** The player's weapon does not damage a `Farmer` (no friendly-fire path in single-player) — and in fact does not damage a plain villager `NPC` either, so a `takeDamage` override was never going to be the hook. Either way, the "ouch emote on hit" (FD-Q6) requires watching for the player swinging a weapon near the worker and triggering the emote ourselves. This is the same work under A or B, so it is **not** a point against B — noted only to correct the FD-Q6 framing, which assumed a `takeDamage` override.

**Reframing the A-vs-B trade:** Option A was *not* a "partial Farmer" that would need a future migration — under A the `Farmer` already does 100% of what is visible (appearance + tool swings); the `NPC` was only an invisible kinematic+hitbox helper. So A carried no deferred Farmer capability. The real trade is: **A** = reuse proven pathfinding + free depth sorting, at the cost of syncing two entities; **B** = one clean entity, at the cost of rebuilding movement + depth sorting. The user chose B for long-term entity cleanliness.

---

### FD-Q6 — Invulnerability reaction on player hit (FR-NPC-02)

`FarmhandNpc.takeDamage(...)` is overridden to return **0** (no damage, shift never abandoned). FR-NPC-02 also wants a brief "ouch / surprised" animation + emote. What reaction?

**A) Exclamation emote, no interruption (Recommended).** Play the vanilla "!" surprised emote (`doEmote`) and continue the current intent without pausing. Minimal, clearly signals "that did nothing," no impact on the work loop.

**B) Question/confused emote, no interruption.** Same as A but with the "?" emote.

**C) Exclamation emote + brief pause.** Worker stops for ~0.5 s on hit, plays the "!" emote, then resumes. More reactive but momentarily interrupts work and risks interfering with stuck detection.

[Answer]: A

---

## Artifact output (after answers collected)

- `aidlc-docs/construction/u-13-worker-features/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-13-worker-features/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-13-worker-features/functional-design/business-rules.md`
