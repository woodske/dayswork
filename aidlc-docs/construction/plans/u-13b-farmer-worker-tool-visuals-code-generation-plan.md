# U-13B — Farmer Worker + Tool Visuals: Code Generation Plan

**Unit**: U-13B — Farmer Worker + Tool Visuals
**Stories**: S-07 (completes — Farmer worker + visible tool-swap)
**Phase**: CONSTRUCTION — Code Generation (Part 1: Planning)

> This plan is the single source of truth for U-13B Code Generation. Generation (Part 2) executes these steps in order after approval.

> **Highest-risk unit in the project.** It replaces the worker's entity, movement, and rendering. The riskiest steps (creating/driving/drawing a standalone `Farmer`) are isolated in Steps 5–7 and flagged; everything behavioural is preserved (BR-PRESERVE-01) so the U-13 Core test suite is the regression net.

---

## Unit Context

**Components owned (new)**: M-10 ToolSwapAnimator; `WorkerTool` map (Core); FarmhandWorker (Farmer); WorkerMovementDriver; WorkerRenderer; WorkerAppearance(+Randomizer).
**Components superseded/removed**: M-09 `FarmhandNpc` (→ `FarmhandWorker`), M-11 `PathFindControllerAdapter` (→ `WorkerMovementDriver`).
**Components extended**: M-12 ShiftOrchestrator (seams re-pointed, behaviour unchanged), M-01 ModEntry (add `Display.RenderedWorld`, drop NPC portrait redirect).
**Unchanged & reused**: ShiftStateMachine, StuckDetector, ShiftContext/ShiftIntent/WorkItem, ObjectTargetClassifier, CapabilityEvaluator, TaskPriorityOrderer, ToolLevelReader, ItemBuffer, ConfigSnapshot, RecurringContractScheduler.
**Dependencies satisfied**: U-04 (TileCoord, TaskKind, ContractId), U-07 (ToolSnapshot), U-10/U-13 (ShiftOrchestrator + behaviour). No forward deps.

**Key decisions baked in**: FD-Q1=A (reuse `PathFindController` for path-compute-only, drive manually), FD-Q2=A (manual `RenderedWorld` draw, on-top accepted), FD-Q3=A (appearance seeded by `ContractId`), FD-Q4=A (full randomization), FD-Q5=A (no-tool face-and-pause), FD-Q6=A (instant swap), TS-U13B-01 (per-tick `Position` stepping + 15 Hz decision logic).

---

## Code Location
- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Core**: `Dayswork.Core\` · **Mod**: `Dayswork\` · **Tests**: `Dayswork.Tests\` (references Core only)
- **Docs**: `aidlc-docs\construction\u-13b-farmer-worker-tool-visuals\code\`

---

## Steps

### A. Core type + test

**Step 1 — Create `Dayswork.Core/Domain/WorkerTool.cs`**
[x] `enum WorkerTool { None, WateringCan, Scythe, Pickaxe, Axe }` + static `WorkerTool ForTask(TaskKind task)` mapping per BR-VIS-01 (Water→WateringCan; ClearWeeds/ClearGrass→Scythe; ClearRocks→Pickaxe; CutTrees→Axe; HarvestCrops/CollectFruit→None; animal kinds→None for totality). Pure, zero Stardew refs (MAINT-U13B-01). *S-07.*

**Step 2 — Create `Dayswork.Tests/Domain/WorkerToolTests.cs`**
[x] Exhaustive `[Theory]` over **every** `TaskKind` asserting the expected `WorkerTool` (satisfies PBT-03 intent for a finite/total map — stronger than a property). *S-07, S-19.*

### B. Mod — appearance

**Step 3 — Create `Dayswork/Worker/WorkerAppearance.cs`**
[x] `record WorkerAppearance(bool IsMale, int Skin, int Hairstyle, Color HairColor, int Shirt, int Pants, Color PantsColor, int Accessory, Color EyeColor)`. Cosmetic only (BR-APPEAR-03). *S-07.*

**Step 4 — Create `Dayswork/Worker/WorkerAppearanceRandomizer.cs`**
[x] `WorkerAppearance Generate(ContractId contractId)` — derive a stable 32-bit seed from the contract ID (deterministic hash), seed a `System.Random`, pick each field from the **valid character-creation ranges**, all indices clamped (Pattern J, BR-APPEAR-01/02, REL-U13B-03). Mod layer (uses XNA `Color`) → play-tested, no Dayswork.Tests entry. *S-07.*

### C. Mod — worker entity (RISK)

**Step 5 — Create `Dayswork/Worker/FarmhandWorker.cs`; delete `Dayswork/Worker/FarmhandNpc.cs`**
[x] Farmer-backed worker (M-09 re-founded). Construct a standalone `StardewValley.Farmer` at the spawn position; apply `WorkerAppearance` via the `change*` appearance methods; assign the captured `ToolSnapshot` so its real tools match the player (BR-WORKER-04). Expose `Position` / `TilePoint` / `FacingDirection` and `doEmote(int)` (inherited from `Character`). Never added to `location.characters`/`location.farmers`; never serialized (BR-WORKER-01 / Pattern I). **RISK: standalone Farmer construction** — flagged for play-test. *S-07.*

### D. Mod — movement + rendering + tool swap (RISK)

**Step 6 — Create `Dayswork/Worker/WorkerMovementDriver.cs`; delete `Dayswork/Worker/PathFindControllerAdapter.cs`**
[x] `StartNavigation(TileCoord, GameLocation, FarmhandWorker)`: build a throwaway `PathFindController` purely to read `pathToEndPoint` (waypoints), copy them into an internal queue, **discard the controller** (FD-Q1=A). `null` path → `NavigationFailed`; empty → immediate `HasArrived`. `Update()` (called every tick): step `Farmer.Position` toward the next waypoint at vanilla base walk speed (BR-WORKER-05, TS-U13B-01), set facing, advance the walk animation; pop waypoints; raise `HasArrived` at the end. Expose `HasArrived`/`NavigationFailed`/`Clear()` (same contract as the old adapter). **RISK: manual Farmer walk animation** — flagged for play-test. *S-07 (fixes U-13 "stands still").*

**Step 7 — Create `Dayswork/Worker/WorkerRenderer.cs`**
[x] `SetActiveWorker(FarmhandWorker?)` + `OnRenderedWorld(object?, RenderedWorldEventArgs)`. Draw the worker (`Farmer.draw` / `FarmerRenderer`) + shadow + active emote bubble at the correct screen position, internally Y-ordered (Pattern F). No-op when no worker / wrong location (REL-U13B-02). On-top draw accepted (BR-WORKER-03). **RISK: manual draw + emote bubble** — flagged for play-test. *S-07.*

**Step 8 — Create `Dayswork/Worker/ToolSwapAnimator.cs` (M-10)**
[x] Hold verified per-direction `FarmerSprite` frame sets (heavy R12/R9/R7; can R10/R5/R8/R11; scythe R5/R6/R7). `OnTaskChanged(TaskKind prev, TaskKind next)`: instant tool swap via `WorkerTool.ForTask` (FD-Q6=A) — set the Farmer's current tool, no equip delay. `PlaySwing(TaskKind, int facing)`: face the tile + `FarmerSprite.animateOnce(frames)` for tool tasks; for `None` tasks play the face-and-pause hand-pick beat (FD-Q5=A). The real effect stays in `InvokeTaskAction` (Invoke-and-Poll). *S-07.*

### E. Mod — orchestrator + composition root

**Step 9 — Modify `Dayswork/Orchestration/ShiftOrchestrator.cs` (seams only; behaviour unchanged — BR-PRESERVE-01)**
[x] Retype `_farmhand` → `FarmhandWorker?`; replace `_nav` (`PathFindControllerAdapter`) with `WorkerMovementDriver`; add `ToolSwapAnimator` + injected `WorkerAppearanceRandomizer` + `WorkerRenderer` reference.
[x] `StartShift`: build appearance from `WorkerAppearanceRandomizer.Generate(contract.Id)`; create `FarmhandWorker`; register it with `WorkerRenderer` instead of `farm.addCharacter`.
[x] `OnUpdateTicked`: call `_movement.Update()` **every tick** (before the `% 4` throttle return) so the walk is smooth (TS-U13B-01); keep all decision logic on the throttle.
[x] Teleport recovery (`HandleTeleportToTile`/`HandleTeleportHome`): set `worker.Position` directly + reset the movement driver instead of `Game1.warpCharacter` (SAFE-U13B-02).
[x] Replace `_farmhand.doEmote(...)` calls → `worker.doEmote(...)` (Farmer); `CheckHitReaction` unchanged except the entity reference.
[x] On work-list advance / first item: call `ToolSwapAnimator.OnTaskChanged(prev, next)`; in `HandleTaskAction` trigger `PlaySwing(task, facing)` synchronized with the existing Invoke-and-Poll.
[x] `HandleExit`/`OnSaving`: drop the worker reference + unregister from `WorkerRenderer` (replaces `farm.characters.Remove`); refund logic unchanged.
*S-07; all U-13 behaviour preserved.*

**Step 10 — Modify `Dayswork/ModEntry.cs`**
[x] Construct `WorkerRenderer`, `WorkerAppearanceRandomizer`, `ToolSwapAnimator`; pass into `ShiftOrchestrator`. Subscribe `helper.Events.Display.RenderedWorld += workerRenderer.OnRenderedWorld`. **Remove** `OnAssetRequested` + the `Portraits/DaysworkFarmhand`→Marnie redirect (NPC-only; obsolete). Leave `i18n/default.json` untouched (no new keys; `npc.farmhand.name` may go unused — acceptable, no churn). *S-07.*

### F. Build, test, docs

**Step 11 — `dotnet build`**
[x] 0 errors / 0 warnings; mod auto-deploys to `Mods/Dayswork/`.

**Step 12 — `dotnet test`**
[x] New `WorkerToolTests` green; **full U-13 regression suite (173) still green** (BR-PRESERVE-01 / REL-U13B-04).

**Step 13 — Create `aidlc-docs/construction/u-13b-farmer-worker-tool-visuals/code/code-summary.md`**
[x] Files created / modified / **deleted**; play-test checklist: (a) worker is a randomized Farmer (stable per contract across days), (b) **walks reliably** to each tile — the U-13 "stands still in grass" symptom is gone, (c) visible correct tool swing per task (axe/can/scythe/pickaxe) + hand-pick for harvest/fruit, (d) instant tool swap on task-class change, (e) depth-draw caveat (may draw over foreground — BR-WORKER-03), (f) "?"/"!" emotes render, (g) invulnerability holds, (h) save-during-shift removes worker + refunds, (i) all U-10..U-13 scenarios regress clean.

**Step 14 — Update `aidlc-state.md` + `audit.md`**
[x] Mark U-13B Code Generation complete; append audit entry.

---

## Story Traceability

| Story | Steps |
|---|---|
| S-07 Farmer worker + tool-swap (completes) | 1–10 |
| S-19 Pure logic + test | 1, 2 |

## Review Change Addendum — Pivot Back To NPC Actor

User review rejected the Farmer-backed worker architecture after play-testing and accepted a Stardew-Squad-style NPC animation quality target. The revised implementation keeps the accepted scan/routing/action fixes from U-13/U-13B but returns the visible worker to a normal `NPC` in the farm character list.

**Step 15 — Restore `Dayswork/Worker/FarmhandNpc.cs`**
[x] Reintroduced the Marnie-placeholder `FarmhandNpc` actor with task-animation helpers and normal farm-character rendering/depth behavior.

**Step 16 — Remove Farmer-only worker visual files**
[x] Deleted `FarmhandWorker`, `WorkerAppearance`, `WorkerAppearanceRandomizer`, and `WorkerRenderer` from the active implementation path.

**Step 17 — Retarget movement + animation seams to NPC**
[x] Updated `WorkerMovementDriver` to path/step a `FarmhandNpc` and use NPC walk frames. Updated `ToolSwapAnimator` to play a callback-free two-frame NPC work beat instead of Farmer tool-pose frames.

**Step 18 — Rewire composition and orchestrator cleanup**
[x] Updated `ModEntry` and `ShiftOrchestrator` to create/add/remove the NPC worker through the farm character list, restore the placeholder portrait asset redirect, and keep the pause gate, morning entrance hold, nearest-task routing, diagnostics, and repeated action loop.

**Step 19 — Verify**
[x] `dotnet build` succeeded with 0 errors / 0 warnings and auto-deployed to `Mods/Dayswork`. `dotnet test` passed 184 tests with 1 expected skipped PBT-08 smoke demo.

**Step 20 — Add visible NPC tools**
[x] Replaced the static menu-icon overlay with Stardew-Squad-style world `TemporaryAnimatedSprite` swings. Axe/pickaxe/watering-can use direction-specific frames from `Game1.toolSpriteSheet`; scythe work uses the vanilla `TileSheets\\animations` swipe. The NPC remains the actor; task effects remain explicit invoke-and-poll calls. Verification: `dotnet build` succeeded with 0 errors / 0 warnings; `dotnet test` passed 184 tests with 1 expected skipped PBT-08 smoke demo.

**Step 21 — Fix building navigation and material debris pickup**
[x] Replaced the manual straight-line no-path fallback with a four-way BFS over worker-passable tiles, and tightened worker passability to reject farm building footprints. Reused the same passability check for reachable task tiles and orthogonal stand-tile selection. Extended debris collection to convert Stardew material chunk debris (`woodDebris`, `bigWoodDebris`, `stoneDebris`, ore debris, etc.) into buffered items instead of only collecting debris with a non-null `item`. Verification: `dotnet build` succeeded with 0 errors / 0 warnings; `dotnet test` passed 184 tests with 1 expected skipped PBT-08 smoke demo.

**Step 22 — Catch delayed tree-fall debris**
[x] Added a pending debris sweep window for non-stump tree chop actions. When a tree starts its fall/stump transition, the worker stores the pre-hit debris baseline and sweeps for newly spawned material debris around the tree tile for 240 ticks / 6 tiles, catching wood that appears after the fall/break animation and lands away from the original tile. Pending sweeps run while time passes and flush before deposit/save. Verification: `dotnet build` succeeded with 0 errors / 0 warnings; `dotnet test` passed 184 tests with 1 expected skipped PBT-08 smoke demo.

**Step 23 — Animate the final farm-exit walk**
[x] Added a short visible exit route after shipping-bin deposit. The NPC now walks to the farm entrance as before, then continues a few pixels/tiles past the entrance through a forced visual route before the shift-complete cleanup removes the character and applies the refund. Verification: `dotnet build` succeeded with 0 errors / 0 warnings; `dotnet test` passed 184 tests with 1 expected skipped PBT-08 smoke demo.

---

## Scope summary
**14 steps**: 1 Core type + 1 Core test + 8 Mod (2 appearance, 1 entity, 1 movement, 1 renderer, 1 tool-swap, 1 orchestrator, 1 ModEntry) + build/test + docs/state. Two file deletions (`FarmhandNpc`, `PathFindControllerAdapter`). Highest-risk steps (5–7: standalone Farmer create/drive/draw) isolated and play-test-flagged; all behavioural logic preserved with the U-13 Core suite as the regression net.



