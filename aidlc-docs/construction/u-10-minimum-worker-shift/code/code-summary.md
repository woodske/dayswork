# U-10 — Minimum Worker Shift: Code Summary

**Build result**: 0 errors, 0 warnings  
**Auto-deployed**: `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`

---

## Files Created

### Dayswork.Core — Pure Domain (zero Stardew references)

| File | Purpose |
|---|---|
| `Dayswork.Core/Shifts/ShiftPhase.cs` | Enum: `WaitingForSpawn, Working, Depositing, Exiting, Done` |
| `Dayswork.Core/Shifts/ShiftIntent.cs` | Abstract record hierarchy: `IntentMoveToTile`, `IntentPerformTaskAt`, `IntentDepositInShippingBin`, `IntentExitFarm` |
| `Dayswork.Core/Shifts/IShiftStateMachine.cs` | Interface: `Phase`, `CurrentIntent`, `Transition`, `SetIntent` |
| `Dayswork.Core/Shifts/ShiftStateMachine.cs` | Linear state machine enforcing `WaitingForSpawn→Working→Depositing→Exiting→Done`; throws on illegal transitions; `Done` is terminal |
| `Dayswork.Core/Shifts/WorkItem.cs` | Immutable record: `(TileCoord Tile, TaskKind Task)` |
| `Dayswork.Core/Shifts/ShiftContext.cs` | Mutable shift runtime state: work queue, item buffer, tool snapshot, timestamps, refund computation |
| `Dayswork.Core/Inventory/IItemBuffer.cs` | Interface: `Add`, `TakeAll`, `Snapshot`, `IsEmpty` |
| `Dayswork.Core/Inventory/ItemBuffer.cs` | List-backed buffer; `TakeAll` clears; `Snapshot` copies without clearing |

### Dayswork — SMAPI Mod

| File | Purpose |
|---|---|
| `Dayswork/Integration/ToolLevelReader.cs` | Reads player inventory to build `ToolSnapshot`; returns `ToolLevel.Basic` for missing tools |
| `Dayswork/Worker/PathFindControllerAdapter.cs` | U-10 thin slice: warps NPC to destination via `Game1.warpCharacter`; passability checked before warp; real pathfinding in U-13 |
| `Dayswork/Worker/FarmhandNpc.cs` | `NPC` subclass with placeholder Marnie sprite; display name from i18n |
| `Dayswork/Orchestration/RecurringContractScheduler.cs` | `DayStarted` handler; multiplayer guard; write-before-spawn deduplication; one-time contracts only (recurring deferred to U-15) |
| `Dayswork/Orchestration/ShiftOrchestrator.cs` | Core shift loop: work list building (building pre-pass + nearest-first open-farm), throttled UpdateTicked dispatch, invoke-and-poll task execution, deposit run, exit + refund |

### Dayswork.Tests

| File | Purpose |
|---|---|
| `Dayswork.Tests/Generators/ItemBufferGen.cs` | Shared FsCheck generators for `ItemBuffer` scenarios |
| `Dayswork.Tests/Shifts/ShiftStateMachineTests.cs` | PBT-U10-01 (Done is terminal), PBT-U10-02 (illegal transitions throw), Full_Legal_Sequence_Completes |
| `Dayswork.Tests/Inventory/ItemBufferTests.cs` | PBT-U10-03 (Snapshot non-destructive), PBT-U10-04 (TakeAll conserves total quantity) |

---

## Files Modified

| File | Change |
|---|---|
| `Dayswork.Core/Domain/ContractStatus.cs` | Added `Executed` value (write-before-spawn deduplication guard) |
| `Dayswork/ModEntry.cs` | Added `ToolLevelReader`, `ShiftOrchestrator`, `RecurringContractScheduler` singletons; wired `DayStarted`, `UpdateTicked`, `TimeChanged` events |
| `Dayswork/i18n/default.json` | Added `"npc.farmhand.name": "Farmhand"` |

---

## Key Design Decisions & Deviations from Plan

| Topic | Decision |
|---|---|
| PathFindController | Replaced with `Game1.warpCharacter` for U-10 thin slice; real walking in U-13 |
| `FruitTree.fruitsOnTree` | Removed — SV 1.6 API renamed to `fruit` (List<Item>); use `fruit.Count` |
| `isBreakableRock()` | Method does not exist in current SV version; replaced with `obj.Name == "Stone"` (basic stones only; ore nodes/boulders in U-13) |
| `takeDamage` override | Removed from `FarmhandNpc` — method signature mismatch; invulnerability deferred to U-13 (FR-NPC-02) |
| `FsCheck Gen.zip` | Does not exist in FsCheck C#; replaced with `SelectMany` chaining |
| `_pendingTask` init | Added initialization in `StartShift` for the first `WorkItem` (bug fix) |
| Log operator precedence | Fixed: `((_ctx.ShiftEndTime ?? Game1.timeOfDay) - _ctx.ShiftStartTime) / 60` |
| Animal tasks | Deferred to U-13 (user decision: Option C — animals aren't tile-based) |
| `ToolSnapshot` | 3 fields only: Axe, Pickaxe, WateringCan (no Scythe level) |

---

## NFR Compliance Summary

| NFR | Status | Notes |
|---|---|---|
| PERF-U10-01 (throttled tick) | Compliant | Every 4 ticks via `_tickCount % 4` |
| PERF-U10-02 (once-per-shift scan) | Compliant | `BuildWorkList` called once in `StartShift` |
| PERF-U10-03 (debris sweep bounded) | Compliant | 128px radius check in `CollectDebrisAt` |
| SAFE-U10-01 (multiplayer guard) | Compliant | First check in `OnDayStarted` |
| SAFE-U10-02 (single active shift) | Compliant | `_ctx is not null` guard in `StartShift` |
| SAFE-U10-03 (deduplication) | Compliant | Write-before-spawn in `RecurringContractScheduler` |
| REL-U10-01 (no crash if no tasks) | Compliant | Early return when `workList.Count == 0` |
| PBT-U10-01 (Done terminal) | Compliant | Test: `ShiftStateMachineTests.Done_Is_Terminal` |
| PBT-U10-02 (illegal transitions throw) | Compliant | Test: `ShiftStateMachineTests.Illegal_Transitions_Always_Throw` |
| PBT-U10-03 (Snapshot non-destructive) | Compliant | Test: `ItemBufferTests.Snapshot_Is_NonDestructive` |
| PBT-U10-04 (TakeAll conserves qty) | Compliant | Test: `ItemBufferTests.TakeAll_Preserves_Total_Quantity` |
| MAINT-U10-01 (Core purity) | Compliant | `Dayswork.Core` has zero Stardew/SMAPI references |
| MAINT-U10-02 (intent-carrying SM) | Compliant | `ShiftIntent` hierarchy in `ShiftStateMachine` |
