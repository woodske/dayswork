# U-13B — NFR Design Patterns

**Unit**: U-13B — Farmer Worker + Tool Visuals

U-13B owns the Farmer-specific patterns that U-13 drafted and deferred at the split (Patterns F and G), and adds patterns for appearance and tool visuals. All behavioural patterns from U-10/U-13 (A–E) are retained unchanged and not restated. The unit changes only the entity/movement/draw/appearance/tool seams.

---

## Retained unchanged (from U-10/U-13)

- **Throttled-Tick** — work-list dispatch, stuck sampling, and hit detection run every 4th `UpdateTicked` (~15 Hz). *(U-13B adds a per-tick position step on top; see Pattern G.)*
- **Once-Per-Shift Scan**, **Capability-Filtered Scan (A)**, **Priority-Grouped Work Queue (B)**, **Multi-Successor State Machine (C)**, **Progress-Sampling Stuck Detection (D)**, **Hybrid 3-Step Escalation (E)** — unchanged; no behavioural code is touched (BR-PRESERVE-01).
- **Core-Purity Guard** — Core types never reference Stardew. The one new Core type (`WorkerTool`) complies.
- **Invoke-and-Poll** — task effects still produced by the existing `InvokeTaskAction`; the tool swing is a visual layer over it (Pattern K).

---

## Pattern F — Farmer-as-Worker Rendering (owned by U-13B; resolves the U-13 TS-U13-04 carry)
**Satisfies**: FD-Q5=B, FD-Q2=A, FR-WORK-10, S-07, BR-WORKER-01/03, BR-VIS-03, SAFE-U13B-01

The worker is a `StardewValley.Farmer` drawn by our own **`Display.RenderedWorld`** subscription each frame — **never** added to `location.characters`/`location.farmers` (preserves SAFE-U13B-01). Because `RenderedWorld` fires after the world draw pass:

- The worker is composited (body + held tool via `FarmerRenderer` + shadow + active emote bubble) at its correct screen position, internally Y-ordered so its own parts layer correctly.
- **Accepted v1 limitation (FD-Q2=A / BR-WORKER-03)**: the worker may draw *over* foreground objects it stands behind. True world-interleaved occlusion (a Harmony draw-pass injection) is **rejected** for v1 — not worth an invasive render patch on the riskiest unit; also keeps NFR-MAINT-04 (no new Harmony patches).
- The emote bubble (stuck "?" / hit "!") is drawn by the renderer, since the worker is outside the game's character-draw pass.

Rejected alternative: registering the Farmer in `location.characters` (would give free depth-sort + update but risks schedules, serialization, and player interaction). *Final confirmation is a code-generation play-test point.*

---

## Pattern G — Manual Path-Follow Movement (owned by U-13B)
**Satisfies**: FD-Q1=A, TS-U13B-01, BR-WORKER-02/05, PERF-U13B-02, REL-U13B-01

`PathFindController` drives an `NPC` via `NPC.update()`; a `Farmer` outside `location.characters` has no such loop, so a **`WorkerMovementDriver`** owns movement:

1. **Path compute (FD-Q1=A)**: on a new navigation target, construct a `PathFindController(worker, location, target, ...)` solely to read its `pathToEndPoint` (`Stack<Point>` waypoints from the game's A*), copy the waypoints, and **discard the controller**. `null` → `NavigationFailed`; empty → immediate `HasArrived`. No per-tick path search (PERF-U13B-02).
2. **Per-tick stepping (TS-U13B-01)**: a lightweight `Update()` runs **every** `UpdateTicked` to advance `Farmer.Position` toward the next waypoint at the vanilla player base walk speed (BR-WORKER-05), set facing, and advance the walk animation — so the walk is smooth at 60 fps rather than stuttering on the 15 Hz throttle. Arrival is detected on the throttled dispatch tick (≤~66 ms latency — imperceptible).
3. **Signal surface**: exposes `HasArrived` / `NavigationFailed` / `Clear()` identical to the old `PathFindControllerAdapter`, so the orchestrator's Working-loop branches are unchanged.

This pattern is the direct fix for the U-13 "worker stands still in a grass field" symptom: per-tick stepping is *our* responsibility and cannot stall on an un-ticked controller (REL-U13B-04 / BR-PRESERVE-01).

Teleport recovery (stuck steps 2/3) sets `Farmer.Position` directly + resets the driver, replacing `Game1.warpCharacter` with identical end-state semantics (SAFE-U13B-02).

---

## Pattern J — Contract-Seeded Appearance
**Satisfies**: FD-Q3=A, FD-Q4=A, BR-APPEAR-01/02/03, REL-U13B-03, PERF-U13B-04

`WorkerAppearanceRandomizer.Generate(contractId)` seeds a deterministic RNG from a stable integer derived from the contract ID, so a recurring contract's worker looks identical every day with **no serialization**. Full character-creation field set is randomized (gender/body, skin, hair style+colour, shirt, pants+colour, accessory, eye colour), every index **clamped to its valid range** so no invalid sprite index reaches `FarmerRenderer` (REL-U13B-03). Generated once per shift at worker creation (PERF-U13B-04). Appearance is cosmetic-only and never feeds behaviour (BR-APPEAR-03).

---

## Pattern K — Pure Tool Map + Mod-side Swing
**Satisfies**: FR-WORK-10, S-07, BR-VIS-01/02/04/05, FD-Q5=A, FD-Q6=A, MAINT-U13B-01

Separation of the pure mapping from the game-coupled animation:

- **Core `WorkerTool.ForTask(TaskKind)`** — a pure, finite, total map (Water→Can, Weeds/Grass→Scythe, Rocks→Pickaxe, Trees→Axe, Harvest/Fruit→None). Zero Stardew refs (MAINT-U13B-01); exhaustively table-tested.
- **Mod `ToolSwapAnimator`** — holds the verified per-direction `FarmerSprite` frame sets (heavy R12/R9/R7; can R10/R5/R8/R11; scythe R5/R6/R7). `OnTaskChanged(prev, next)` performs an **instant** tool swap (FD-Q6=A — the new tool simply appears on the next swing, no equip interval for the stuck/cap logic to special-case). During an `IntentPerformTaskAt`, it faces the task tile and triggers a one-shot `FarmerSprite.animateOnce(...)` swing — except `None`-mapped tasks (Harvest/Collect fruit) play the **face-and-pause hand-pick** beat instead of a swing (FD-Q5=A). The actual chop/water/clear is still produced by the unchanged `InvokeTaskAction` (Invoke-and-Poll), with the swing as the visible layer.

---

## Pattern H — Inherent Invulnerability + Swing-Proximity Emote (carried from U-13)
**Satisfies**: BR-INVULN-01/02, FR-NPC-02, FD-Q6=A (U-13)

Unchanged in logic; only the entity reference type changes from `NPC` to `Farmer`. A `Farmer` has no single-player friendly-fire path → inherently invulnerable. The hit-reaction watcher checks each sampled tick whether the player is mid-melee-swing within range; on a fresh swing it plays the worker's "!" emote (debounced one-per-swing, drawn by `WorkerRenderer`) and changes nothing else.

---

## Pattern I — Save-Exclusion Guard (carried from U-13)
**Satisfies**: SAFE-U13B-01, BR-WORKER-01

The Farmer is referenced only by the orchestrator and is never in any serialized collection. On `OnSaving` during an active shift the worker is removed and the existing refund handling applies. Never written to the save.

---

## Resilience Assessment

| Failure scenario | Handling | Pattern |
|---|---|---|
| Tile unreachable (no path) | Skip and continue | G / retained |
| Worker wedged | 3-step escalation (unchanged) | D + E (carried) |
| Worker fails to advance (the U-13 "stands still" bug) | Per-tick manual stepping; cannot stall on an un-ticked controller | G |
| Invalid randomized appearance index | Clamped to valid ranges | J / REL-U13B-03 |
| Renderer/movement called between shifts | No-op (null worker guard) | F / G / REL-U13B-02 |
| Player attacks the worker | No damage; debounced "!" emote | H |
| Save during active shift | Worker removed + refund | I |
| Any U-13 behavioural regression | Guarded by BR-PRESERVE-01 + U-13 Core test suite staying green | retained |

## Scalability Assessment
N/A — single-player mod.

## Security Assessment
N/A — Security Baseline extension disabled (Requirements Analysis Q28).
