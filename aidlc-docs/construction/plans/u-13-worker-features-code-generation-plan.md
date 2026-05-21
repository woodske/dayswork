# U-13 — Worker AI (Priority + Skip + Stuck + Invulnerability): Code Generation Plan

**Unit**: U-13 — Worker AI: Priority + Capability/Skip + Stuck + Invulnerability
**Stories**: S-08 (completes), S-09 (completes), S-16, S-17, S-19 (PBT)
**Phase**: CONSTRUCTION — Code Generation (Part 1: Planning)

> **Scope note (split):** The full-Farmer re-founding and tool-swap visuals (**S-07**) were split out into **U-13B — Farmer Worker + Tool Visuals**, which runs next. U-13 keeps the worker as the existing `NPC` and delivers all the *behavior* logic on that proven foundation. The Farmer-specific design content in U-13's design artifacts is retained for reference and migrates to U-13B.

> This plan is the single source of truth for U-13 Code Generation. Generation (Part 2) executes these steps in order after approval.

---

## Unit Context

**Components owned (new)**: C-09 StuckDetector; ObjectTargetClassifier (Mod).
**Components extended**: C-08 ShiftStateMachine (Stuck/Recovering), M-11 PathFindControllerAdapter (real walking, replacing the U-10 teleport stub), M-12 ShiftOrchestrator (priority + skip + stuck + invuln). M-09 FarmhandNpc stays an `NPC` in U-13 (re-founded as a Farmer in U-13B).
**Moved to U-13B**: M-10 ToolSwapAnimator, WorkerTool map, FarmhandWorker (Farmer), WorkerMovementDriver, WorkerRenderer, WorkerAppearance(+Randomizer), the `Display.RenderedWorld` hook, S-07.
**Dependencies satisfied**: U-04 (TileCoord, TaskKind), U-05 (RefundCalculator), U-07 (CapabilityEvaluator, CapabilityMatrix, AxeTarget/PickTarget, ToolSnapshot, TaskPriorityOrderer), U-10 (ShiftStateMachine, ItemBuffer, ShiftOrchestrator, PathFindControllerAdapter, ToolLevelReader).

**Key decisions**: FD-Q1=A (7 outdoor tasks; animals + buildings deferred — TODO-05), FD-Q2=A (first-class Stuck/Recovering + multi-successor table, orchestrator owns `RecoveryAttempts`), FD-Q3=A (tile-or-action progress), FD-Q4=B (nearest reachable orthogonal neighbor for trellis), FD-Q6=A ("!" emote, no interruption). FD-Q5=B (Farmer) is U-13B.

**Why real walking lands here:** U-10's movement is a `warpCharacter` teleport stub. Stuck detection (S-16) is only meaningful if the worker actually walks, so U-13 replaces the stub with native `PathFindController` walking on the NPC. This also lets us verify **TODO-01** (tree-seed drops) at a realistic pace. (U-13B later swaps NPC walking for the custom Farmer movement driver — a cheap, isolated redo.)

---

## Code Location
- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Core**: `Dayswork.Core\` · **Mod**: `Dayswork\` · **Tests**: `Dayswork.Tests\`
- **Docs**: `aidlc-docs\construction\u-13-worker-features\code\`

---

## Steps

### A. Core types

**Step 1 — Modify `Dayswork.Core/Shifts/ShiftPhase.cs`**
[x] Add `Stuck` and `Recovering` (between `Working` and `Depositing`). *S-16.*

**Step 2 — Modify `Dayswork.Core/Shifts/ShiftIntent.cs`**
[x] Add `IntentPlayEmote(int EmoteId)`, `IntentTeleportToTile(TileCoord Destination)`, `IntentTeleportHome`. *S-16.*

**Step 3 — Modify `Dayswork.Core/Shifts/ShiftStateMachine.cs`**
[x] `_successors` → `Dictionary<ShiftPhase, HashSet<ShiftPhase>>` per BR-SM-01 (`Working→{Depositing,Stuck}`, `Stuck→{Recovering}`, `Recovering→{Working,Depositing}`, rest single). Add `Stuck`,`Recovering` to `_activePhases`. Keep terminal-`Done` + intent guards. *S-16, S-19.*

**Step 4 — Modify `Dayswork.Core/Shifts/WorkItem.cs`**
[x] `WorkItem(TileCoord NavTile, TileCoord TaskTile, TaskKind Task)` (trellis nav vs action tile). *S-08.*

**Step 5 — Modify `Dayswork.Core/Shifts/ShiftContext.cs`**
[x] Add `int RecoveryAttempts { get; set; }` and `HashSet<TaskKind> ToolMissingWarnings { get; } = new();`. *S-16, S-09.*

**Step 6 — Create `Dayswork.Core/Shifts/IStuckDetector.cs` + `StuckDetector.cs`**
[x] `RecordTick(bool madeProgress, int minutes)`, `ShouldFireStuck()`, `Reset()`; ctor takes threshold. Pure Core. *S-16, S-19.*

### B. Core tests (PBT)

**Step 7 — Modify `Dayswork.Tests/Shifts/ShiftStateMachineTests.cs`**
[x] Add **PBT-U13-01** (no transition from Done), **PBT-U13-02** (only legal successors incl. Stuck/Recovering edges; non-successors throw), **PBT-U13-03** (Stuck only from Working; Recovering only from Stuck; neither from Done). U-02 seed-logging. *S-19.*

**Step 8 — Create `Dayswork.Tests/Shifts/StuckDetectorTests.cs`**
[x] **PBT-U13-04** (progress → not stuck), **PBT-U13-05** (no-progress threshold monotonicity), **PBT-U13-06** (`Reset()` clears). *S-19.*

### C. Mod logic

**Step 9 — Modify `Dayswork/Worker/PathFindControllerAdapter.cs` — real walking**
[x] Replace the U-10 `warpCharacter` teleport stub with native `StardewValley.PathFindController` walking: `StartNavigation` assigns `npc.controller = new PathFindController(npc, location, targetTile, finalFacing)`; `HasArrived` = controller finished/null path consumed; `NavigationFailed` = no path produced (Skip-and-Continue). *S-16 (makes stuck meaningful), TODO-01 re-check.*

**Step 10 — Create `Dayswork/Worker/ObjectTargetClassifier.cs`**
[x] Map `Tree`/`FruitTree`/`ResourceClump`/`Object` → `AxeTarget`/`PickTarget`; null (→ skip) for unmapped classes (REL-U13-04). *S-09.*

**Step 11 — Modify `Dayswork/Orchestration/ShiftOrchestrator.cs` (major)**
[x] Update `WorkItem.Tile` refs → `NavTile`/`TaskTile`.
[x] `BuildWorkList`: remove building pre-pass (BR-PRIO-03); apply `ObjectTargetClassifier` + `CapabilityEvaluator` skip rules (FR-SKIP-01/02/03); expand `DetectTask` to classify `ResourceClump`/ore; trellis → nearest reachable orthogonal `NavTile`, else skip (FR-SKIP-04); not-ready crops skip (FR-SKIP-05); group by `TaskPriorityOrderer` then nearest-first (BR-PRIO-01); record `ToolMissingWarnings` (BR-TOOL-02).
[x] Per sampled tick: compute `madeProgress` (FD-Q3=A) → `StuckDetector.RecordTick`; on `ShouldFireStuck` run the 3-step escalation with `RecoveryAttempts` (Patterns D/E); handle `IntentPlayEmote` / `IntentTeleportToTile` / `IntentTeleportHome`.
[x] Invulnerability: hit-detection helper (player melee swing within range → `npc.doEmote("!")`, debounced, no state change) — S-17, FD-Q6=A. (NPC villagers are inherently undamageable, so this is the whole story.)
*S-08, S-09, S-16, S-17.*

### D. Build, test, docs

**Step 12 — `dotnet build`**
[x] 0 errors, 0 warnings; mod auto-deploys to `Mods/Dayswork/`.

**Step 13 — `dotnet test`**
[x] New U-13 PBTs + full regression all green.

**Step 14 — Create `aidlc-docs/construction/u-13-worker-features/code/code-summary.md`**
[x] Files created/modified; play-test checklist (real walking pace, priority order, skip rules, stuck recovery, invuln emote, **TODO-01 tree-seed re-check**).

**Step 15 — Update `aidlc-state.md` + `audit.md`**
[x] Mark U-13 Code Generation complete; append audit entry.

---

## Story Traceability

| Story | Steps |
|---|---|
| S-08 Full priority + skip | 4, 10, 11 |
| S-09 Capability snapshot + tool-missing | 5, 10, 11 |
| S-16 Stuck escalation | 1–3, 5, 6, 7, 8, 9, 11 |
| S-17 Invulnerability + ouch emote | 11 |
| S-19 Pure logic + PBT | 1–6, 7, 8 |

---

## Scope summary
**15 steps**: 6 Core (5 modify, 1 create) + 2 Core test + 3 Mod (1 walking, 1 classifier, 1 orchestrator) + build/test + docs/state. Builds entirely on proven U-07/U-10 foundations — no high-risk architectural change (that's U-13B). S-07 + Farmer re-founding deferred to U-13B.
