# U-13 — Logical Components

**Unit**: U-13 — Worker Features: Priority + Stuck + Tool Swap + Invulnerability

## Component Map

```
SMAPI Events
    │
    ├─ DayStarted ─────────► RecurringContractScheduler (U-10, unchanged)
    │                            └─ ShiftOrchestrator.StartShift()
    │
    ├─ UpdateTicked (÷4) ──► ShiftOrchestrator (extended)
    │                            ├─ sample madeProgress → StuckDetector.RecordTick()
    │                            ├─ StuckDetector.ShouldFireStuck()? → escalation (Pattern E)
    │                            ├─ HitReactionWatcher: player swing near worker? → "!" emote
    │                            ├─ reads ShiftStateMachine.CurrentIntent:
    │                            │   ├─[IntentMoveToTile]      → WorkerMovementDriver.HasArrived?
    │                            │   ├─[IntentPerformTaskAt]   → ToolSwapAnimator swing + Invoke-and-Poll
    │                            │   ├─[IntentPlayEmote]       → worker.doEmote(); → Recovering
    │                            │   ├─[IntentTeleportToTile]  → worker warp; Reset(); → Working
    │                            │   ├─[IntentTeleportHome]    → warp to entrance; → Depositing
    │                            │   ├─[IntentDepositInShippingBin] → deposit → Exiting
    │                            │   └─[IntentExitFarm]        → refund → remove worker → Done
    │                            └─ build work list (Pattern A + B) at StartShift
    │
    ├─ TimeChanged (8pm) ──► ShiftOrchestrator (retained: → Depositing)
    │
    ├─ Rendered/World ─────► WorkerRenderer (draw Farmer, Y-depth sorted)   ← new
    │
    └─ Saving ─────────────► ShiftOrchestrator.OnSaving (remove worker + refund)
```

---

## Component Responsibilities

### ShiftStateMachine *(Core — extended)*
- Adds `Stuck`, `Recovering` phases; successor map becomes set-valued (Pattern C).
- Active-phase set adds `Stuck`, `Recovering`.
- Still pure; PBT-tested (PBT-U13-01/02/03).

### StuckDetector *(Core — new, C-09)*
- `RecordTick(madeProgress, minutes)`, `ShouldFireStuck()`, `Reset()`.
- No Stardew refs. PBT-tested (PBT-U13-04/05/06).

### ShiftOrchestrator *(Mod — extended, M-12)*
- Owns `RecoveryAttempts` (escalation counter) on the `ShiftContext`.
- Drives Patterns A, B, D, E; dispatches the new intents.
- Wires `WorkerMovementDriver`, `ToolSwapAnimator`, `HitReactionWatcher`, `StuckDetector`, `CapabilityEvaluator`, `TaskPriorityOrderer`.
- Retains throttle, Invoke-and-Poll, 8pm cap, single-trip deposit, refund, `OnSaving` removal.

### FarmhandWorker *(Mod — M-09, re-founded on `Farmer`)*
- Replaces U-10's `FarmhandNpc`. Wraps a `StardewValley.Farmer`.
- Applies `WorkerAppearance` + `ToolSnapshot`; exposes position/facing; plays emotes.
- Never added to game-managed/serialized collections (Pattern I).

### WorkerMovementDriver *(Mod — new, supersedes M-11 PathFindControllerAdapter)*
- Computes route via game pathfinding; steps `Farmer.Position` + walk anim per tick.
- Exposes `HasArrived` / `NavigationFailed` (Pattern G).

### ToolSwapAnimator *(Mod — new, M-10)*
- `WorkerTool ForTask(TaskKind)` mapping; `OnTaskChanged(prev, next)`.
- Triggers `FarmerSprite.animateOnce(...)` swings during task actions (Pattern F / BR-VIS).

### WorkerRenderer *(Mod — new)*
- Subscribes to a world render event; draws the worker `Farmer` ordered by world Y (Pattern F). Honors BR-WORKER-03 fallback.

### HitReactionWatcher *(Mod — new; may be folded into the orchestrator)*
- Detects player melee swing within range; debounced "!" emote (Pattern H). No state change.

### ObjectTargetClassifier *(Mod — new)*
- Maps `Tree`/`FruitTree`/`ResourceClump`/`Object` to `AxeTarget`/`PickTarget`; returns null (→ skip) for unmapped classes (Pattern A / REL-U13-04).

### WorkerAppearanceRandomizer *(Mod — new)*
- Produces a `WorkerAppearance` from character-creation field ranges (TS-U13-05).

### Reused unchanged
- `CapabilityEvaluator`/`CapabilityMatrix` (C-06), `TaskPriorityOrderer` (C-07), `ToolLevelReader` (M-19), `ItemBuffer` (C-10), `ConfigSnapshot` (C-14), `RecurringContractScheduler` (M-13).

---

## Extension Compliance Summary

| Rule | Status | Rationale |
|---|---|---|
| PBT-02 (blocking) | N/A | U-13 introduces no new round-trip-serialized type |
| PBT-03 (blocking) | Compliant | 6 properties: extended `ShiftStateMachine` (terminal/legal/reachability) + `StuckDetector` (progress-reset/threshold/Reset) — PBT-U13-01..06 |
| PBT-07 (blocking) | N/A | No new shared generator obligation beyond U-10's `ItemBufferGen` |
| PBT-08 (blocking) | Compliant | Seed + shrunk-input logging convention followed (PBT-U13-07) |
| PBT-09 (blocking) | N/A | Framework established in U-02 |
| PBT-01/04/05/06/10 | Advisory | No action required |
| Security Baseline | N/A | Extension disabled (Q28) |
