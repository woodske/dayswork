# U-10 — NFR Design Patterns

## Pattern 1 — Throttled-Tick Pattern
**Satisfies**: PERF-U10-01 (UpdateTicked < 1ms per frame)

`ShiftOrchestrator` holds a private tick counter. On every `UpdateTicked` callback, the counter is incremented and the body is skipped unless `tickCount % 4 == 0`. All orchestrator logic runs only on the target tick.

```
OnUpdateTicked(sender, args):
    _tickCount++
    if _tickCount % 4 != 0: return
    // orchestrator logic here
```

Cost on skipped ticks: one integer increment + one modulo comparison. Immeasurable.
Detection latency added: ≤ 67ms at 60fps. Imperceptible to the player.

---

## Pattern 2 — Once-Per-Shift Scan Pattern
**Satisfies**: PERF-U10-02 (tile scanning once per zone entry, not per frame)

The work list is built exactly once, inside the `DayStarted` handler, at the moment the state machine transitions `WaitingForSpawn → Working`. The list is stored on `ShiftContext.WorkList` for the duration of the shift. No tile scanning occurs during `UpdateTicked`.

Completed `WorkItem`s are removed from the front of the list (`Queue<WorkItem>` or list with an index pointer). The list is never rebuilt mid-shift.

---

## Pattern 3 — Invoke-and-Poll Pattern
**Satisfies**: PERF-U10-03 (multi-hit object handling; N2: B)

When the state machine carries `IntentPerformTaskAt(tile, task)`:

1. **Invoke** (once, on the tick the intent is set): call the appropriate game API for the task type on the target tile.
2. **Poll** (every 4 ticks, via Throttled-Tick): check whether the target object is still present at the tile.
3. **Advance** (when poll returns "object gone"): transition intent to `IntentMoveToTile` for the next `WorkItem`, or transition state to `Depositing` if the work list is empty.

The `_currentActionPending` flag on the orchestrator distinguishes "just invoked, now polling" from "moving to next tile." The flag is set on invoke and cleared on advance.

**Animal task completion detection** differs from object removal: completion is detected by checking the animal's state flags (`wasPet`, `fullness`, `currentProduce`) rather than tile object presence.

---

## Pattern 4 — Skip-and-Continue Pattern
**Satisfies**: REL-U10-03 (unreachable tiles silently skipped; FR-WORK-08)

When `PathFindControllerAdapter.StartNavigation()` is called for a tile and the controller cannot produce a path (returns null or reports failure immediately), the orchestrator:
1. Logs a SMAPI trace-level message (not visible to the player).
2. Marks the `WorkItem` as skipped.
3. Immediately advances to the next `WorkItem` without waiting for the throttle cycle.

No retry. No player notification. The tile is not re-queued.

---

## Pattern 5 — Deduplication Guard Pattern
**Satisfies**: SAFE-U10-03 (contract fires exactly once)

`RecurringContractScheduler` follows this strict ordering in its `DayStarted` handler:

```
1. Check MultiplayerGuard → abort if multiplayer
2. Query ContractStore for Active + OneTime contracts for today
3. For each match:
   a. Set contract.Status = Executed  ← FIRST (write-before-spawn)
   b. Persist via ContractStore
   c. Spawn worker shift
```

Writing `Executed` before spawning ensures that if the handler is interrupted (or fires a second time), step 2's query finds no Active contracts and no double-spawn occurs.

---

## Pattern 6 — Core-Purity Guard
**Satisfies**: MAINT-U10-01 (NFR-MAINT-03 — Core has no SMAPI/Stardew refs)

`ShiftStateMachine` and `ItemBuffer` live in `Dayswork.Core`. They accept and return only primitive types and Core-defined records (`TileCoord`, `TaskKind`, `ShiftPhase`, `ShiftIntent`, `WorkItem`).

All Stardew API calls are made in `Dayswork` (Mod project) by the orchestrator, the NPC, and the adapters. The Core types are never passed Stardew objects directly.

The `Dayswork.Core.csproj` has no reference to `StardewValley` or `StardewModdingAPI` assemblies — this is the enforcement mechanism. Any attempt to import a game type into Core will fail to compile.

---

## Resilience Assessment

| Failure scenario | Handling | Pattern |
|---|---|---|
| Tile unreachable (no path) | Skip and continue | Pattern 4 |
| Game API call has no effect (object already gone) | Poll detects "object gone" immediately → advance | Pattern 3 |
| `DayStarted` fires twice | Status written to `Executed` before spawn | Pattern 5 |
| 8pm fires mid-task | `TimeChanged` handler transitions to `Depositing` unconditionally | Business Rule BR-12 |
| Animal state flags never change (stuck feeding) | Deferred to U-13 (StuckDetector); U-10 has no stuck detection | Out of scope |

## Scalability Assessment

N/A — single-player game mod. No scaling, load balancing, or capacity planning applies.

## Security Assessment

N/A — Security Baseline extension disabled at Requirements Analysis (Q28: no network/PII/auth surface).
