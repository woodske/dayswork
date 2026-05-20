# U-13 — Business Logic Model

**Unit**: U-13 — Worker Features: Priority + Stuck + Tool Swap + Invulnerability
**Stories**: S-07 (completes), S-08 (completes), S-09 (completes), S-16, S-17
**Design answers**: FD-Q1=A (defer animals + buildings), FD-Q2=A (first-class Stuck/Recovering phases), FD-Q3=A (tile-or-action progress), FD-Q4=B (nearest reachable orthogonal neighbor), FD-Q5=B (full Farmer), FD-Q6=A ("!" emote, no interruption)

This unit turns U-10's thin worker ("walks to one tile, does one task, distance-ordered, recolored-villager sprite") into a real worker: full FR-WORK-03 priority queue, full capability/skip rules, stuck recovery, visible player-style tool use, and invulnerability. The single biggest change is FD-Q5=B — **the worker is re-founded on `Farmer` instead of `NPC`.**

---

## 0. Scope boundary (FD-Q1 = A)

U-13 implements the **7 outdoor task types** that already have detection + invocation: Water crops, Harvest crops, Collect fruit, Clear weeds, Clear grass, Clear rocks, Cut trees.

**Deferred to a future unit** (logged as TODO-05 in `aidlc-state.md`):
- The 3 animal tasks: Feed animals, Pet animals, Collect animal products.
- All **building-interior** work, including the **greenhouse** (requires door-warp navigation per FR-WORK-09).

Consequence: U-13 **removes the U-10 building pre-pass** in `BuildWorkList` (it only ever produced unreachable indoor tiles that were silently skipped). The priority orderer is still fed all enabled task kinds, but only outdoor task kinds yield work items; animal kinds order correctly in the sequence and simply contribute zero items for now.

---

## 1. Worker entity overhaul — Farmer instead of NPC (FD-Q5 = B, revises FR-NPC-01)

The worker is re-implemented from a `StardewValley.NPC` subclass to a driven `StardewValley.Farmer`. The component identity **M-09 FarmhandNpc** is retained but its implementation now wraps/IS a `Farmer` (the class will be renamed during code generation, e.g. `FarmhandWorker`).

### 1.1 Why (recorded for traceability)
- `NPC` sprite sheets contain only walking + expression frames — **no tool-use frames**. `Farmer` sprites, via `FarmerRenderer`, draw the **held tool with correct swing frames** (verified: heavy tools R12/R9/R7, watering can R10/R5/R8/R11, scythe/melee R5/R6/R7).
- Post-V1 roadmap (energy bar, own tools, food/buffs) is **Farmer-native** (`Farmer.Stamina`, `Farmer.Items`, `CurrentTool`, eating/buffs).

### 1.2 Worker lifecycle (replaces U-10 §1 spawn / §6 exit mechanics)
1. **Create at 6am** with a **randomized appearance** (see [domain-entities.md](domain-entities.md) `WorkerAppearance`) drawn from the same fields the character-creation menu sets (skin, hair + color, shirt, pants, accessory, eye color).
2. Position at the farm entrance; assign the captured `ToolSnapshot` so the worker carries the right tool levels.
3. **Drive movement manually** (§1.3) and **draw manually with depth sorting** (§1.4) — neither is free once we leave the `NPC` base.
4. **Remove at shift end** and **on save** — the `Farmer` is never written into the save file. This reuses U-10's existing `OnSaving` removal+full-refund pattern; the worker is held only by our own reference and is **never** added to any serialized collection (not `location.characters`, not `location.farmers`).

### 1.3 Movement driver (replaces M-11 PathFindControllerAdapter for the Farmer)
`PathFindController` moves an `NPC`/`Character` by ticking its walk logic; a non-player `Farmer`'s movement is normally input/network-driven, so it will not drive a `Farmer` cleanly. U-13 introduces a **path-follower**:
1. Compute a tile path from the worker's current tile to the navigation target (reuse the game's A* path search to produce the tile sequence).
2. Each sampled tick, step the worker toward the next path node: update `Farmer.Position`, set facing direction, and advance the **walking** animation so the sprite animates while moving.
3. Raise an "arrived" signal when the final node is reached, and a "no path" signal if the search fails (→ Skip-and-Continue, same as U-10).

The orchestrator consumes arrived / no-path exactly where it consumed `PathFindControllerAdapter`'s `HasArrived` / `NavigationFailed` today, so the Working-state loop shape is preserved.

### 1.4 Rendering with depth sorting
A standalone `Farmer` drawn in a render hook renders on top of the world by default. U-13 draws the worker each frame ordered by its world Y so it sorts behind/in front of trees, buildings, and crops like a normal character. (If exact depth parity proves costly, the v1 fallback is documented in [business-rules.md](business-rules.md) BR-WORKER-03.)

---

## 2. Work list building — full FR-WORK-03 priority (replaces U-10 distance-only sort)

U-10 sorted the open-farm work list purely by Manhattan distance, mixing task types. U-13 applies the fixed priority order via **C-07 TaskPriorityOrderer**, with distance as the secondary key (matches U-10's FD-Q2-A intent).

Executed once on `WaitingForSpawn → Working`:

1. **Scan** every tile in the contracted open-farm zone(s). For each tile, `DetectTask` returns the applicable enabled task kind (if any).
2. **Apply capability + skip rules at scan time** (§3) — a tile that fails a skip rule produces no work item.
3. **Resolve the navigation tile** per work item:
   - Normal tile: nav tile = the object's tile.
   - **Trellis crop (FR-SKIP-04, FD-Q4=B)**: nav tile = the **nearest reachable orthogonal neighbor** to the worker's current position; if no orthogonal neighbor is reachable, the crop is **skipped** (no work item).
4. **Order**: group work items by `TaskKind`; order the groups by `TaskPriorityOrderer.Order(enabledTasks)` (FR-WORK-03: Feed → Pet → Collect animal products → Water → Harvest → Collect fruit → Clear weeds → Clear grass → Clear rocks → Cut trees). Within each group, order tiles **nearest-first** from the worker's position at the time that group begins.
5. **Result**: a single `Queue<WorkItem>` ordered by (priority group, then distance). Each `WorkItem` carries a nav tile and a task tile (equal except for trellis). See [domain-entities.md](domain-entities.md).

If the resulting queue is empty, the shift is skipped (unchanged from U-10).

---

## 3. Capability snapshot & skip rules (S-09 completes; FR-SKIP-01..05)

The `ToolSnapshot` captured at 6am is now actually **used** via **C-06 CapabilityEvaluator** during the scan. Object → capability-target classification:

| In-game object | Maps to | Rule |
|---|---|---|
| Standing tree (`Tree`) | `AxeTarget.StandingTree` | choppable at any axe level |
| Small player-left stump (`Tree` stump / `Twig`) | `AxeTarget.SmallStump` | any level |
| Large stump (`ResourceClump`) | `AxeTarget.LargeStump` | **Steel+** (FR-SKIP-01) |
| Large log (`ResourceClump`) | `AxeTarget.LargeLog` | **Gold+** (FR-SKIP-01) |
| Fruit tree (`FruitTree`) | `AxeTarget.FruitTree` | **always skip** for felling (FR-SKIP-03) |
| Small rock / stone (`Object` "Stone", ore nodes) | `PickTarget.SmallRock` | any level |
| Large boulder (`ResourceClump`) | `PickTarget.LargeBoulder` | **Steel+** (FR-SKIP-02) |
| Meteorite (`ResourceClump`) | `PickTarget.Meteorite` | **Gold+** (FR-SKIP-02) |

Skip rules applied at scan time:
- **FR-SKIP-01 / 02**: a chop/break target whose `CapabilityEvaluator.CanChop` / `CanBreak` returns false given the snapshot is **silently skipped** (no work item, does not count toward hours).
- **FR-SKIP-03**: fruit trees never produce a Cut-trees work item (already enforced; `CanChop(FruitTree)` is unconditionally false).
- **FR-SKIP-04**: trellis crops resolved per §2 step 3.
- **FR-SKIP-05**: crops not yet ready to harvest produce no Harvest work item (extends U-10's `IsReadyToHarvest`).

This requires extending U-10's `DetectTask`, which currently matches only basic `"Stone"` and standing trees, to also classify `ResourceClump`s (large stumps/logs/boulders/meteorites) and ore nodes into the targets above.

### 3.1 Tool-missing warning (S-09)
If an **entire enabled task type** is skipped solely because the player lacks the tool (snapshot level 0 / tool not owned), the worker records a **tool-missing warning intent** for that task kind. In U-13 this is only **queued** into a pending-warning set on the shift context; the actual mail is delivered by U-14's `MailDispatcher`. No mail is sent in U-13.

---

## 4. Tool-swap animation (M-10 ToolSwapAnimator; S-07 completes; FR-WORK-10)

With the full Farmer, the tool is the Farmer's real `CurrentTool` and is drawn by `FarmerRenderer` during the swing — no overlay icon. **ToolSwapAnimator's** responsibility:

1. Maintain the worker's tool mapping per task kind:

   | Task | Tool shown |
   |---|---|
   | Water crops | Watering Can |
   | Clear weeds, Clear grass | Scythe (melee frames) |
   | Clear rocks | Pickaxe |
   | Cut trees | Axe |
   | Harvest crops, Collect fruit | none (vanilla hand-pick) |

2. `OnTaskChanged(previous, next)`: when the **tool class** differs from the previous task's, set the worker's current tool to the new tool and (optionally) play a brief equip beat. Tasks mapped to "none" show no tool.
3. During `IntentPerformTaskAt`, trigger the matching one-shot swing via `FarmerSprite.animateOnce(...)` using the verified frame set for the worker's facing direction (heavy tools for pickaxe/axe, watering-can set for watering, melee set for scythe). The visible swing is synchronized with the existing Invoke-and-Poll task action.

The underlying game effect (the actual chop/water/harvest) is still produced by the existing `InvokeTaskAction` logic; the animation is the visible layer over it.

---

## 5. Stuck detection & 3-step escalation (C-09 StuckDetector; S-16; FR-WORK-11/12)

### 5.1 StuckDetector (pure Core)
- `RecordTick(bool madeProgressThisTick, int inGameMinutesElapsed)` — if progress was made, reset the accumulator; otherwise add the elapsed minutes.
- `ShouldFireStuck()` — true once the no-progress accumulator reaches the active threshold.
- `Reset()` — zero the accumulator (called on progress, on teleport, on phase change).

### 5.2 Progress definition (FD-Q3 = A)
The orchestrator computes `madeProgressThisTick`:
- During navigation (`IntentMoveToTile` / teleport-recovery): progress = the worker's **tile coordinate changed** since the last sampled tick.
- During a task action (`IntentPerformTaskAt`): progress = **true** (performing work is progress; standing still to swing a tool is never "stuck").

### 5.3 Escalation (FR-WORK-12, hybrid 3-step)
The escalation counter (`RecoveryAttempts`) lives on the **shift context / orchestrator** (FD-Q2=A), not in the state machine.

```
Working ──ShouldFireStuck──► Stuck
                              │  Step 1: play "?" confused emote
                              ▼
                           Recovering
                              │  Step 2: teleport to the next reachable task tile in the queue,
                              │          Reset() detector, RecoveryAttempts++ , resume
                              ├──teleport ok──► Working
                              │
                              └──stuck again (RecoveryAttempts ≥ 1) OR no reachable tile──►
                                 Step 3: teleport to farm entrance, end shift early
                                 (treat like the 8pm cap) ──► Depositing ──► Exiting ──► Done
```

- Thresholds come from `ConfigSnapshot` (`StuckInitialThresholdMinutes`, `StuckPostTeleportThresholdMinutes`, both default 10). The post-teleport window uses the second threshold.
- On Step 3, buffered items are deposited at the **shipping bin** (always reachable in U-13; multi-chest + mail-on-unreachable is U-14). Refund is computed from actual hours worked per FR-PAY-05 — the early end time is captured exactly like the 8pm cap.

### 5.4 State machine extension (C-08; FD-Q2 = A)
`Stuck` and `Recovering` become first-class `ShiftPhase` members. The transition table changes from single-successor to **sets of legal successors**:

| From | Legal successors |
|---|---|
| WaitingForSpawn | Working |
| Working | Depositing, **Stuck** |
| **Stuck** | **Recovering** |
| **Recovering** | **Working**, Depositing |
| Depositing | Exiting |
| Exiting | Done |
| Done | (none — terminal) |

PBT invariants preserved: `Done` is terminal; any transition not in the table throws; active phases (Working/Depositing/Exiting/Recovering) carry an intent, non-active phases (WaitingForSpawn/Stuck?/Done) — note **Stuck** carries the emote intent and is therefore active. See [domain-entities.md](domain-entities.md) for the exact active-phase set.

---

## 6. Invulnerability + ouch emote (M-09; S-17; FR-NPC-02; FD-Q6 = A)

A `Farmer` is not damaged by the player's weapon (no single-player friendly fire), so there is no `takeDamage` hook — the worker is **inherently invulnerable**. The "ouch" reaction is therefore detected manually:

1. Each sampled tick, check whether the player is mid-swing with a melee weapon (`Game1.player.UsingTool` and `CurrentTool is MeleeWeapon`) and the worker is within melee range of the player.
2. On a fresh detection, play the worker's **"!" surprised emote** (FD-Q6=A) and **do not interrupt** the current intent.
3. Debounce so one swing yields at most one emote (track the swing so a held animation doesn't spam emotes).

This path is independent of the stuck/work loop and never changes the shift state.

---

## 7. Orchestrator wiring summary (M-12)

`ShiftOrchestrator` gains, on top of its U-10 responsibilities:
- the **path-follower** movement driver (§1.3) in place of `PathFindControllerAdapter`;
- **TaskPriorityOrderer** + **CapabilityEvaluator** in `BuildWorkList` (§2, §3);
- **StuckDetector** ticking + the Stuck/Recovering escalation (§5);
- **ToolSwapAnimator** invocation on task-class change and during task actions (§4);
- the **hit-detection** ouch-emote watcher (§6);
- manual **depth-sorted drawing** of the worker (§1.4).

Retained from U-10 unchanged: throttled-tick (every 4th tick), Invoke-and-Poll task completion, 8pm cap → Depositing, single-trip shipping-bin deposit, refund-at-exit, `OnSaving` worker removal + full refund.
