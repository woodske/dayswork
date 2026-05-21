# U-13B — Business Logic Model

**Unit**: U-13B — Farmer Worker + Tool Visuals
**Stories**: S-07 (completes — Farmer worker + visible tool-swap when changing task class)
**Design answers**: FD-Q1=A (reuse `PathFindController` for path computation only, drive manually), FD-Q2=A (self-consistent draw on top of world; foreground over-draw accepted as v1 cosmetic limitation), FD-Q3=A (appearance deterministic from contract ID), FD-Q4=A (full appearance randomization), FD-Q5=A (no-tool tasks face-and-pause hand-pick), FD-Q6=A (instant tool swap)

U-13B re-founds the worker on `StardewValley.Farmer` so it visibly uses tools the way the player does, and completes S-07 with the tool-swap animation. **It changes only the entity / movement / rendering / appearance / tool-visual seams** — every behavioural rule shipped in U-13 (priority queue, capability/skip, stuck escalation, invulnerability emote, deposit/exit, save handling) is preserved unchanged.

This is the project's highest-uncertainty unit; it was split off U-13 precisely so a play-test failure here cannot regress validated worker-AI logic.

---

## 0. Scope boundary

**In scope (U-13B):**
- Replace the `NPC` worker (`FarmhandNpc`) with a driven `Farmer` worker (`FarmhandWorker`).
- Replace `PathFindControllerAdapter` with a manual `WorkerMovementDriver` (path computed via vanilla A*, stepped manually).
- Add `WorkerRenderer` (manual draw via `Display.RenderedWorld`).
- Add `WorkerAppearance` + `WorkerAppearanceRandomizer` (full randomized look, stable per contract).
- Add `ToolSwapAnimator` (M-10) + the Core `WorkerTool` map.
- Re-point `ShiftOrchestrator`'s entity/movement/draw/emote touchpoints; add a render hook in `ModEntry`; drop the NPC portrait asset redirect.

**Out of scope / unchanged (already shipped in U-13):**
- Priority-grouped work list, capability/skip rules, trellis adjacency, tool-missing warnings — `BuildWorkList`, `DetectTask`, `ObjectTargetClassifier` are untouched.
- The `ShiftStateMachine` phases/transitions, `StuckDetector`, escalation logic, intent vocabulary.
- The hit-reaction *logic* (`CheckHitReaction`) — only its entity reference changes from `NPC` to `Farmer`.
- Deposit/exit flow, refund math, `OnSaving` removal + refund pattern.
- Animal tasks + building interiors (TODO-05, a future unit).

---

## 1. Worker entity: `Farmer` instead of `NPC` (FD-Q5=B from U-13, revises FR-NPC-01 → DEV-01)

The worker is re-implemented from an `NPC` subclass to a driven `StardewValley.Farmer`. The component identity **M-09** is retained; the class is renamed `FarmhandNpc` → `FarmhandWorker` and now holds/IS a `Farmer`.

### 1.1 Why (traceability)
- `NPC` sprite sheets contain only walking + expression frames — **no tool-use frames**. A `Farmer`, via `FarmerRenderer`, draws the **held tool with correct swing frames**. This is the only way to satisfy FR-WORK-10 / S-07 without bespoke art.
- The post-V1 roadmap (energy bar, worker-owned tools, food/buffs) is **Farmer-native** (`Farmer.Stamina`, `Farmer.Items`, `CurrentTool`, eating/buffs), so the Farmer foundation pays forward.

### 1.2 Worker lifecycle (replaces the U-10/U-13 NPC spawn + exit mechanics)
1. **Create at 6am**: build a `Farmer` instance, apply a **randomized appearance** seeded by the contract ID (§4), and assign the captured `ToolSnapshot` so the worker's real tools match the player's upgrade levels.
2. **Position** at the farm entrance tile.
3. **Drive movement manually** (§2) and **draw manually** (§3) — neither is automatic once we leave the `NPC` base, and the worker is deliberately kept out of `location.characters`/`location.farmers`.
4. **Remove at shift end and on save**: the `Farmer` is held only by our own reference, is never added to any serialized collection, and is never written to the save. The existing `OnSaving` removal + refund pattern from U-10/U-13 is reused verbatim (only the field type changes).

### 1.3 What the orchestrator must re-point (mechanical seam list)
The U-13 `ShiftOrchestrator` couples to the NPC at these exact points; each is re-pointed at the Farmer + movement driver, with **no change to the surrounding control flow**:

| U-13 (NPC) touchpoint | U-13B (Farmer) replacement |
|---|---|
| `_farmhand` typed `FarmhandNpc?` | typed `FarmhandWorker?` (Farmer-backed) |
| `farm.addCharacter(_farmhand)` | hold our own reference + register with `WorkerRenderer`; **no** `addCharacter` |
| `farm.characters.Remove(_farmhand)` | drop our reference + unregister from renderer |
| `_nav` (`PathFindControllerAdapter`) | `WorkerMovementDriver` (same `HasArrived`/`NavigationFailed` surface) |
| `_nav.StartNavigation(tile, farm, npc)` | `_movement.StartNavigation(tile, farm, worker)` |
| `Game1.warpCharacter(_farmhand, ...)` (teleport steps 2/3) | set `worker.Position` directly + reset the movement driver |
| `_farmhand.doEmote(id)` | `worker.doEmote(id)` (`Farmer` inherits `doEmote` from `Character`) |
| `_farmhand.TilePoint` | `worker.TilePoint` (`Farmer` inherits it from `Character`) |

The throttled-tick loop, intent dispatch `switch`, `SampleProgress`, `CheckHitReaction`, `BuildWorkList`, deposit/exit handlers and `OnSaving` keep their structure.

---

## 2. Manual movement driver (FD-Q1=A; supersedes M-11 `PathFindControllerAdapter`)

`PathFindController` moves an `NPC`/`Character` by ticking its walk logic inside `NPC.update()`. A `Farmer` not in `location.characters` has no such update loop, so we drive movement ourselves. **FD-Q1=A**: reuse the game's A* for *path computation only*, then step the Farmer manually.

### 2.1 Path computation (the answered question)
1. On `StartNavigation(target, location, worker)`, construct a `PathFindController(worker, location, targetPoint, finalFacing, endBehavior)` **solely to read its `pathToEndPoint`** — a `Stack<Point>` of tile waypoints produced by the game's proven A*.
2. **Copy the waypoints into our own queue and discard the controller.** The controller is never assigned to anything that ticks it — we do not rely on `NPC.update()` (which a Farmer doesn't run anyway). This is the key fix for the U-13 "worker stands still in a grass field" play-test bug: stepping is now *our* responsibility every tick, not a controller loop that may stall.
3. If `pathToEndPoint` is `null` → raise `NavigationFailed` (→ Skip-and-Continue, identical to U-13). If it is empty (already on the target tile) → raise `HasArrived` immediately.

### 2.2 Per-tick stepping
Each sampled tick while an `IntentMoveToTile` is active:
1. Read the next waypoint. Compute the pixel delta from `Farmer.Position` toward that waypoint's tile centre.
2. Advance `Farmer.Position` by the worker's per-tick move distance toward the waypoint (worker speed = the vanilla player base walk speed; no running — keeps the pace natural and consistent with how stuck thresholds were tuned in U-13).
3. Set `worker.FacingDirection` to the dominant movement axis and advance the **walking** animation (`FarmerSprite` walk frames) so the sprite animates while moving; when stationary, show the idle frame for the current facing.
4. When the worker reaches the waypoint's centre (within a small epsilon), pop it. When the final waypoint is consumed, raise `HasArrived`.

### 2.3 Signal surface (unchanged contract)
`WorkerMovementDriver` exposes `bool HasArrived` and `bool NavigationFailed` exactly as `PathFindControllerAdapter` did, plus a `Clear()` for shift end. The orchestrator consumes them at the same call sites, so the Working-state loop shape is preserved. Teleport recovery (stuck steps 2/3) sets `Position` directly and resets the driver instead of calling `Game1.warpCharacter`.

---

## 3. Manual rendering with depth handling (FD-Q2=A; `WorkerRenderer`)

A standalone `Farmer` we draw ourselves renders on top of the world by default, because `Display.RenderedWorld` fires **after** the world's draw pass. **FD-Q2=A**: accept that limitation for v1.

1. `ModEntry` subscribes a `WorkerRenderer.OnRenderedWorld` handler to `helper.Events.Display.RenderedWorld`.
2. Each frame, if a worker is active and on the currently-rendered location, draw it at its correct screen position (world `Position` → screen via the viewport), compositing body + held tool (via `FarmerRenderer`) + shadow + any active emote bubble, **internally Y-ordered so the worker's own parts layer correctly**.
3. The worker may visually draw **over** a tree canopy or building edge it is standing behind. This is the **BR-WORKER-03 cosmetic fallback**, accepted for v1 and logged as a play-test note. True world-interleaved occlusion (a Harmony draw-pass injection) is explicitly deferred — not worth adding an invasive render patch to the riskiest unit.
4. Emote rendering: because the worker is drawn manually, the "?" / "!" emote bubbles (used by stuck step 1 and the hit reaction) must be drawn by the renderer too — `doEmote` sets the emote state, and the renderer draws the bubble above the worker rather than relying on the game's character-draw pass.

---

## 4. Appearance: randomized, stable per contract (FD-Q3=A, FD-Q4=A)

The worker `Farmer` is never serialized, so its appearance is regenerated in memory whenever a shift starts. To make a recurring contract's worker recognizable, the appearance is **deterministic from the contract ID**.

### 4.1 Stability (FD-Q3=A)
`WorkerAppearanceRandomizer.Generate(contractId)` seeds its RNG from the contract ID. The same contract therefore yields the **same appearance every day** with zero serialization; different contracts yield different-looking workers. (If the contract ID is not a convenient integer seed, derive a stable 32-bit seed from it, e.g. a deterministic hash of its string value.)

### 4.2 Field scope (FD-Q4=A)
Full randomization across the vanilla character-creation ranges: gender/body, skin tone, hair style + colour, shirt, pants (+ colour), accessory, and eye colour. Each worker reads as a distinct person. Ranges are bounded to the valid character-creation indices so no invalid sprite indices are produced.

### 4.3 Application
The generated `WorkerAppearance` is applied to the `Farmer` at creation (set the corresponding `Farmer` appearance fields) before the first draw. Appearance is purely cosmetic and never affects work behaviour.

---

## 5. Tool-swap animation (M-10 `ToolSwapAnimator`; FR-WORK-10; S-07 completes)

With the full Farmer, the visible tool is the Farmer's real held tool, drawn by `FarmerRenderer` during the swing — no overlay icon.

### 5.1 Tool mapping (LOCKED; via the Core `WorkerTool` map)
| Task | Tool shown |
|---|---|
| Water crops | Watering Can |
| Clear weeds, Clear grass | Scythe (melee frames) |
| Clear rocks | Pickaxe |
| Cut trees | Axe |
| Harvest crops, Collect fruit | none (hand-pick) |

### 5.2 Swap on task-class change (FD-Q6=A — instant)
`OnTaskChanged(TaskKind previous, TaskKind next)`: if `WorkerTool.ForTask(next)` differs from the previous task's tool, set the worker's current tool to the new tool **immediately** — the new tool simply appears on the next swing, with **no equip delay**. This keeps the work loop tight and avoids introducing a non-working interval that the stuck detector / 8pm cap would have to special-case. Tasks mapped to `None` show no held tool.

### 5.3 Swing during task actions
During an `IntentPerformTaskAt` for a tool-using task, trigger a one-shot swing via `FarmerSprite.animateOnce(...)` using the verified frame set for the worker's current facing direction:
- heavy tools (Axe, Pickaxe): R12 / R9 / R7
- Watering Can: R10 / R5 / R8 / R11
- Scythe / melee: R5 / R6 / R7

The worker faces the task tile before swinging. The swing is the **visible layer only** — the actual chop/water/clear effect is still produced by the existing `InvokeTaskAction` logic (unchanged), synchronized with the Invoke-and-Poll completion check.

### 5.4 No-tool tasks (FD-Q5=A)
Harvest crops and Collect fruit show **no tool** and play **no swing**. Instead the worker **faces the target tile and pauses briefly** (face-and-pause hand-pick) while the harvest resolves — a clear visual that work is happening, mirroring vanilla hand-harvesting. No idle-only "standing" gap.

---

## 6. Invulnerability + ouch emote (carried from U-13, FR-NPC-02; unchanged logic)

A `Farmer` is not damaged by the player's weapon in single-player (no friendly fire), so the worker is **inherently invulnerable** — no damage override exists or is needed. U-13's `CheckHitReaction` watcher carries over unchanged except for the entity reference type:

1. Each sampled tick, check whether the player is mid-swing with a melee weapon and within melee range of the worker.
2. On a fresh swing, play the worker's **"!" surprised emote** once and do not interrupt the current intent or change shift state.
3. The emote is drawn by `WorkerRenderer` (§3 step 4) since the worker is no longer in the game's character-draw pass.

---

## 7. Orchestrator + composition-root wiring summary

`ShiftOrchestrator` changes (no control-flow changes, only the seams in §1.3):
- swap `FarmhandNpc` → `FarmhandWorker`;
- swap `PathFindControllerAdapter` → `WorkerMovementDriver`;
- replace `Game1.warpCharacter` teleports with direct `Position` set + driver reset;
- construct `WorkerAppearanceRandomizer.Generate(contractId)` and `ToolSwapAnimator`; call `OnTaskChanged` when advancing the work list and trigger swings during task actions;
- expose the active worker to `WorkerRenderer`.

`ModEntry` changes:
- **add** `helper.Events.Display.RenderedWorld += workerRenderer.OnRenderedWorld`;
- **drop** the `OnAssetRequested` portrait redirect (NPC-only; a Farmer has no villager portrait asset).

Retained unchanged: throttled-tick (every 4th tick), Invoke-and-Poll task completion, priority/skip work-list build, stuck escalation, 8pm cap → Depositing, single-trip shipping-bin deposit, refund-at-exit, `OnSaving` worker removal + refund.
