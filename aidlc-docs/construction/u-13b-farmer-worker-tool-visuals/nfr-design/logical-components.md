# U-13B — Logical Components

**Unit**: U-13B — Farmer Worker + Tool Visuals

U-13B swaps the worker entity, movement, and rendering components and adds appearance + tool-visual components. Behavioural components (state machine, stuck detector, classifier, work-list logic) are unchanged.

## Component Map

```
SMAPI Events
    │
    ├─ DayStarted ─────────► RecurringContractScheduler (unchanged)
    │                            └─ ShiftOrchestrator.StartShift()
    │                                 └─ WorkerAppearanceRandomizer.Generate(contractId)  ← new
    │                                 └─ create FarmhandWorker (Farmer)                    ← changed
    │
    ├─ UpdateTicked (EVERY tick) ─► WorkerMovementDriver.Update()  ← new (per-tick position step, TS-U13B-01)
    │
    ├─ UpdateTicked (÷4) ──► ShiftOrchestrator (re-pointed seams; logic unchanged)
    │                            ├─ sample madeProgress → StuckDetector.RecordTick()  (unchanged)
    │                            ├─ StuckDetector.ShouldFireStuck()? → escalation       (unchanged)
    │                            ├─ HitReactionWatcher: player swing near worker → "!"  (entity = Farmer)
    │                            ├─ reads ShiftStateMachine.CurrentIntent:
    │                            │   ├─[IntentMoveToTile]      → WorkerMovementDriver.HasArrived?
    │                            │   ├─[IntentPerformTaskAt]   → ToolSwapAnimator (swap + swing/hand-pick) + Invoke-and-Poll
    │                            │   ├─[IntentPlayEmote]       → worker.doEmote()
    │                            │   ├─[IntentTeleportToTile]  → set Position + driver reset (was warpCharacter)
    │                            │   ├─[IntentTeleportHome]    → set Position + driver reset → Depositing
    │                            │   ├─[IntentDepositInShippingBin] → deposit → Exiting
    │                            │   └─[IntentExitFarm]        → refund → drop worker ref → Done
    │                            └─ ToolSwapAnimator.OnTaskChanged(prev, next) on work-list advance
    │
    ├─ TimeChanged (8pm) ──► ShiftOrchestrator (unchanged: → Depositing)
    │
    ├─ Display.RenderedWorld ─► WorkerRenderer.OnRenderedWorld (draw Farmer + tool + shadow + emote)  ← new wire
    │
    └─ Saving ─────────────► ShiftOrchestrator.OnSaving (drop worker ref + refund; unchanged)
```

---

## Component Responsibilities

### FarmhandWorker *(Mod — M-09, re-founded on `Farmer`; replaces FarmhandNpc)*
- Wraps a `StardewValley.Farmer`; applies `WorkerAppearance` + `ToolSnapshot`; exposes `Position`/`TilePoint`/`FacingDirection`; plays emotes via inherited `doEmote`.
- Never added to game-managed/serialized collections (Pattern I).

### WorkerMovementDriver *(Mod — new, supersedes M-11 PathFindControllerAdapter)*
- Path-compute via throwaway `PathFindController.pathToEndPoint`, then discard (Pattern G / FD-Q1=A).
- `Update()` runs **every** tick: steps `Farmer.Position` toward the next waypoint at vanilla walk speed, advances walk anim + facing (TS-U13B-01).
- Exposes `HasArrived` / `NavigationFailed` / `Clear()` (unchanged contract).

### WorkerRenderer *(Mod — new)*
- Subscribed to `Display.RenderedWorld`; draws the active worker (body + held tool via `FarmerRenderer` + shadow + emote), internally Y-ordered (Pattern F). On-top draw accepted per BR-WORKER-03. No-op when no worker active or wrong location (REL-U13B-02).

### WorkerAppearance *(Mod — new record)*
- Cosmetic field bundle (gender/body, skin, hair+colour, shirt, pants+colour, accessory, eye colour).

### WorkerAppearanceRandomizer *(Mod — new)*
- `Generate(ContractId)` → deterministic, range-clamped `WorkerAppearance` (Pattern J / FD-Q3/Q4=A).

### ToolSwapAnimator *(Mod — M-10, new)*
- Holds verified `FarmerSprite` frame sets; `OnTaskChanged(prev, next)` instant swap (FD-Q6=A); triggers one-shot swings during task actions; face-and-pause hand-pick for `None` tasks (FD-Q5=A). Uses the Core `WorkerTool` map (Pattern K).

### WorkerTool *(Core — new enum + ForTask map)*
- Pure, finite, total `TaskKind → WorkerTool`. Zero Stardew refs (MAINT-U13B-01). Exhaustively table-tested.

### ShiftOrchestrator *(Mod — M-12, seams re-pointed; logic unchanged)*
- Field types swapped (`FarmhandWorker`, `WorkerMovementDriver`); teleports set `Position` directly; constructs/uses `ToolSwapAnimator` + `WorkerAppearanceRandomizer`; exposes the worker to `WorkerRenderer`.
- All work-list build, skip rules, stuck escalation, deposit/exit, `OnSaving`, throttle, Invoke-and-Poll, 8pm cap retained (BR-PRESERVE-01).

### ModEntry *(Mod — M-01, composition root)*
- Constructs the new components; **adds** `Display.RenderedWorld` subscription; **removes** the `OnAssetRequested` NPC-portrait redirect.

### Removed
- `FarmhandNpc`, `PathFindControllerAdapter` (deleted).

### Reused unchanged
- `ShiftStateMachine` / `StuckDetector` (Core), `ObjectTargetClassifier`, `CapabilityEvaluator`/`CapabilityMatrix` (C-06), `TaskPriorityOrderer` (C-07), `ToolLevelReader` (M-19), `ItemBuffer` (C-10), `ConfigSnapshot` (C-14), `RecurringContractScheduler` (M-13), `ShiftContext`/`ShiftIntent`/`WorkItem`.

---

## Extension Compliance Summary

| Rule | Status | Rationale |
|---|---|---|
| PBT-02 (blocking) | N/A | No new round-trip-serialized type |
| PBT-03 (blocking) | Compliant | Only new pure logic is the finite/total `WorkerTool.ForTask` map → exhaustive table test satisfies the invariant intent; U-13 Core properties remain green (REL-U13B-04) |
| PBT-07 (blocking) | N/A | No new shared-generator obligation |
| PBT-08 (blocking) | Compliant | Seed + shrunk-input convention honored if any property is added |
| PBT-09 (blocking) | N/A | Framework established in U-02 |
| PBT-01/04/05/06/10 | Advisory | No action required |
| Security Baseline | N/A | Extension disabled (Q28) |
