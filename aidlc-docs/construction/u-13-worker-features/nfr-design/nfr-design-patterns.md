# U-13 — NFR Design Patterns

**Unit**: U-13 — Worker Features: Priority + Stuck + Tool Swap + Invulnerability

---

## Retained from U-10 (unchanged)

- **Throttled-Tick** (PERF-U13-01) — work/stuck/hit logic runs every 4th `UpdateTicked`.
- **Once-Per-Shift Scan** (PERF-U13-02) — the work list is built once at shift start; never rebuilt mid-shift. Extended below by the Capability-Filtered Scan.
- **Invoke-and-Poll** (PERF-U13-03 of U-10) — task actions invoked once, polled for completion.
- **Skip-and-Continue** (REL-U13-03) — unreachable nav targets advance without error.
- **Core-Purity Guard** (MAINT-U13-01) — Core types never touch Stardew assemblies.
- **Save-Exclusion** (SAFE-U13-03) — extended below for the Farmer.

---

## Pattern A — Capability-Filtered Scan
**Satisfies**: BR-SKIP-01..06, S-09, FR-SKIP-01..05

Inside the single shift-start scan, each candidate object is run through a Mod-side **ObjectTargetClassifier** that maps it to an `AxeTarget`/`PickTarget` (or "not a capability-gated object"), then the pure **CapabilityEvaluator** (C-06) decides keep/skip against the 6am `ToolSnapshot`:

```
for each tile in zone:
    task = DetectTask(tile)                     # enabled + present
    if task is null: continue
    if task is capability-gated:
        target = Classifier.Classify(object)    # Mod: Tree/FruitTree/ResourceClump/Stone → enum
        if target is null: continue             # unknown class → skip (REL-U13-04)
        if not CapabilityEvaluator.CanX(snapshot, target): 
            recordToolMissingIfWholeTaskSkipped(task)
            continue                            # FR-SKIP-01/02/03
    if task is HarvestCrops and crop not ready: continue   # FR-SKIP-05
    navTile = (trellis crop) ? nearestReachableOrthogonal(tile) : tile
    if navTile is null: continue                # FR-SKIP-04 surrounded
    emit WorkItem(navTile, tile, task)
```

Tool-missing accumulation feeds `ShiftContext.ToolMissingWarnings` (BR-TOOL-02); mail is U-14.

---

## Pattern B — Priority-Grouped Work Queue
**Satisfies**: BR-PRIO-01/02, FR-WORK-03

After the scan, work items are grouped by `TaskKind`; groups are ordered by `TaskPriorityOrderer.Order(enabledTasks)` (C-07, U-07); within each group, tiles are sorted nearest-first from the worker's position at the time that group begins. Result is a single `Queue<WorkItem>`. Replaces U-10's distance-only sort. Animal task kinds order correctly but contribute no items in U-13 (BR-PRIO-02).

---

## Pattern C — Multi-Successor State Machine + External Escalation Counter
**Satisfies**: BR-SM-01..03, FD-Q2=A, FR-WORK-12

`ShiftStateMachine` (Core) keeps a **set-valued** successor map and stays a pure transition table:

```
WaitingForSpawn → {Working}
Working         → {Depositing, Stuck}
Stuck           → {Recovering}
Recovering      → {Working, Depositing}
Depositing      → {Exiting}
Exiting         → {Done}
Done            → {}            # terminal
```

The escalation count (`RecoveryAttempts`) lives on `ShiftContext`/orchestrator, **not** the machine — so the machine remains a stateless-decision pure type and keeps its PBT invariants. Active phases (must carry an intent): Working, Stuck, Recovering, Depositing, Exiting.

---

## Pattern D — Progress-Sampling Stuck Detection
**Satisfies**: BR-STUCK-01/02/04, FR-WORK-11

On each sampled tick the orchestrator computes `madeProgress` (FD-Q3=A: tile coordinate changed during navigation; `true` during a task action) and the in-game-minutes elapsed since the last sample, then calls `StuckDetector.RecordTick(...)`. `ShouldFireStuck()` gates the escalation. `Reset()` is called on any progress tick and on every teleport. Threshold switches from `StuckInitialThresholdMinutes` to `StuckPostTeleportThresholdMinutes` after the first teleport.

---

## Pattern E — Hybrid 3-Step Stuck Escalation
**Satisfies**: BR-STUCK-03/05, FR-WORK-12, S-16

```
ShouldFireStuck && RecoveryAttempts == 0:
    Working → Stuck   (IntentPlayEmote "?")          # step 1
    nextTile = nextReachableTaskTile()
    Stuck → Recovering (IntentTeleportToTile nextTile)# step 2
    on teleport done: Reset(); RecoveryAttempts++; Recovering → Working

ShouldFireStuck && RecoveryAttempts >= 1, OR nextTile == null:
    Recovering → Depositing (IntentTeleportHome → end shift early)  # step 3
    # deposit to shipping bin, refund from actual hours (= 8pm-cap path)
```

Bounded by `RecoveryAttempts`: no infinite recovery loop (REL-U13-01/02).

---

## Pattern F — Farmer-as-Worker Rendering (resolves TS-U13-04)
**Satisfies**: FD-Q5=B, FR-WORK-10, S-07, BR-VIS-01..03, BR-WORKER-01/03

The worker is a `StardewValley.Farmer`. **Decision: manual render hook** — the worker is drawn by our own render subscription each frame, ordered by world Y for depth sorting, and is **never** added to `location.characters`/`location.farmers` (preserves SAFE-U13-03). Tool swings play via `FarmerSprite.animateOnce(...)` with the verified per-direction frame sets; the held tool is the Farmer's real `CurrentTool`, drawn by `FarmerRenderer`. Rejected alternative: registering the Farmer in `location.characters` (free depth-sort/update but risks schedules/serialization/interaction). **Accepted fallback** (BR-WORKER-03): if exact depth parity is impractical, draw above foreground objects — cosmetic only, logged as a play-test TODO. *Final confirmation is a code-generation play-test point.*

---

## Pattern G — Manual Path-Follow Movement
**Satisfies**: TS-U13-03, BR-WORKER-02

A **WorkerMovementDriver** computes a tile route using the game's pathfinding, then on each sampled tick steps `Farmer.Position` toward the next node, sets facing, and advances the walk animation. It exposes `HasArrived` / `NavigationFailed` matching the old `PathFindControllerAdapter` surface, so the orchestrator's Working-loop branches are unchanged. Replaces the adapter for the Farmer.

---

## Pattern H — Inherent Invulnerability + Swing-Proximity Emote
**Satisfies**: BR-INVULN-01/02, FR-NPC-02, FD-Q6=A

No `takeDamage` hook exists for a `Farmer` (no single-player friendly fire) — the worker is inherently invulnerable. A **HitReactionWatcher** checks each sampled tick whether the player is mid-melee-swing within range of the worker; on a fresh swing it plays the worker's "!" emote (debounced one-per-swing) and changes nothing else. Independent of the work/stuck loop.

---

## Pattern I — Save-Exclusion Guard (extended)
**Satisfies**: SAFE-U13-03, BR-WORKER-01

The Farmer is referenced only by the orchestrator. On `OnSaving` during an active shift, the worker is removed and the full deposit refunded (U-10 pattern). It is never written to the save because it is never in any serialized collection.

---

## Resilience Assessment

| Failure scenario | Handling | Pattern |
|---|---|---|
| Tile unreachable (no path) | Skip and continue | Retained / G |
| Worker wedged (fence trap, etc.) | 3-step escalation: emote → teleport → end shift | D + E |
| Teleport target also unreachable | Escalate straight to end-shift | E / REL-U13-02 |
| Stuck during a task action | Not stuck — task ticks count as progress | D / BR-STUCK-01 |
| Unknown game object class | Classifier returns skip; no throw | A / REL-U13-04 |
| Player attacks the worker | No damage; debounced "!" emote | H |
| Save during active shift | Worker removed + full refund | I |
| 8pm cap mid-task | Transition to Depositing (retained) | U-10 BR-12 |

## Scalability Assessment
N/A — single-player mod.

## Security Assessment
N/A — Security Baseline extension disabled (Requirements Analysis Q28).
