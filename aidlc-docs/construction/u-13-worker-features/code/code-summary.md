# U-13 — Code Summary

**Unit**: U-13 — Worker AI: Priority + Capability/Skip + Stuck + Invulnerability
**Build**: ✅ 0 errors / 0 warnings
**Tests**: ✅ 173 passed / 1 skipped / 0 failed

---

## Files Created

| File | Purpose |
|---|---|
| `Dayswork.Core/Shifts/IStuckDetector.cs` | Interface: RecordTick / ShouldFireStuck / Reset |
| `Dayswork.Core/Shifts/StuckDetector.cs` | Pure Core impl; threshold ctor; zero Stardew refs (C-09) |
| `Dayswork.Tests/Shifts/StuckDetectorTests.cs` | PBT-U13-04/05/06 + 3 sanity facts |
| `Dayswork/Worker/ObjectTargetClassifier.cs` | Mod: maps Tree/FruitTree/ResourceClump/Object → AxeTarget/PickTarget |

---

## Files Modified

| File | What Changed |
|---|---|
| `Dayswork.Core/Shifts/ShiftPhase.cs` | Added `Stuck`, `Recovering` enum values |
| `Dayswork.Core/Shifts/ShiftIntent.cs` | Added `IntentPlayEmote`, `IntentTeleportToTile`, `IntentTeleportHome` |
| `Dayswork.Core/Shifts/ShiftStateMachine.cs` | Multi-successor `Dictionary<ShiftPhase, HashSet<ShiftPhase>>`; `Stuck`/`Recovering` added to `_activePhases` |
| `Dayswork.Core/Shifts/WorkItem.cs` | `WorkItem(TileCoord NavTile, TileCoord TaskTile, TaskKind Task)` — nav/action tile split for trellis crops |
| `Dayswork.Core/Shifts/ShiftContext.cs` | Added `RecoveryAttempts`, `ToolMissingWarnings` |
| `Dayswork.Tests/Shifts/ShiftStateMachineTests.cs` | PBT-U13-01/02/03 (terminal, illegal, reachability); updated helpers for multi-successor; table-driven legal-edges fact; stuck-recover sanity fact |
| `Dayswork/Worker/PathFindControllerAdapter.cs` | Replaced `warpCharacter` teleport stub with native `PathFindController` walking (`StardewValley.Pathfinding`) |
| `Dayswork/Orchestration/ShiftOrchestrator.cs` | Major rewrite: priority-grouped work list (Pattern B); capability + trellis skip rules (Pattern A); 3-step stuck escalation (Patterns D/E); new intent handlers; hit-reaction watcher (Pattern H); tool-missing accumulation (BR-TOOL-02) |
| `Dayswork/ModEntry.cs` | Pass `config` to `ShiftOrchestrator` constructor |

---

## Architecture Notes

### Real walking (Step 9)
`PathFindControllerAdapter` now assigns `npc.controller = new PathFindController(...)` and lets the game's own `NPC.update()` loop advance the walk animation. `HasArrived` is set via the `endBehavior` callback; `NavigationFailed` is set when `pathToEndPoint` is null after construction. This replaces the U-10 `warpCharacter` teleport stub and makes stuck detection meaningful.

### Stuck-time accumulation
`SampleProgress` computes elapsed in-game minutes as `(Game1.timeOfDay - _lastSampledGameTime) / 10`. Most sampled ticks contribute 0 minutes; when the game clock advances (every ~7 real seconds = 10 in-game minutes), the full 10 minutes is accumulated. Threshold default = 10 in-game minutes for both initial and post-teleport windows.

### Tool-missing warnings (BR-TOOL-02)
`BuildWorkList` collects `capSkippedKinds` and `anyItemForKind` during the scan. A kind enters `toolMissingWarnings` only when it appears in `capSkippedKinds` **and** produced zero work items — i.e., the entire task type was skipped because of the tool level. Warning mail delivery is U-14.

### Trellis adjacency (FD-Q4=B / FR-SKIP-04)
`IsTrellisCrop` checks `dirt.crop.raisedSeeds.Value`. `FindOrthogonalNeighbour` tries N/E/S/W in order and returns the first passable tile. `WorkItem.NavTile` may differ from `WorkItem.TaskTile` for trellis crops. The `IntentPerformTaskAt` always uses `TaskTile` (the crop tile), while `IntentMoveToTile` uses `NavTile` (where the worker stands).

---

## Play-Test Checklist

- [ ] Worker visibly **walks** across the farm rather than teleporting (TODO-01 re-check: do tree seeds now appear in the shipping bin?)
- [ ] Work executes in **priority order**: Water → Harvest → Collect fruit → Weeds → Grass → Rocks → Trees (verify with a mixed farm)
- [ ] Trellis crops (e.g. hops, beans) are harvested from a **neighbour tile** rather than on top of the crop
- [ ] **Capability skip**: large log not felled without Gold axe; large boulder not broken without Steel pickaxe; fruit trees never felled
- [ ] **Tool-missing log/warning**: shift completes but logs which task kind was skipped (mail in U-14)
- [ ] **Stuck step 1**: worker plays "?" emote when stuck (verify emote ID = 8 is question mark — play-test TODO, see EmoteQuestion constant)
- [ ] **Stuck step 2**: worker teleports to next reachable task tile and resumes
- [ ] **Stuck step 3**: on second stuck window (or no reachable tile), worker teleports to farm entrance, deposits items, refunds partial payment
- [ ] **Invulnerability**: player weapon swing near worker plays "!" emote (verify emote ID = 2 is exclamation — play-test TODO, see EmoteExclamation constant); worker takes no damage
- [ ] **8pm cap**: shift ends correctly if 8pm fires while in Working or Recovering phase
- [ ] **Save during shift**: worker removed, full deposit refunded, no save corruption
- [ ] Existing U-10/U-11/U-12 scenarios still work (regression)

### Emote ID verification TODO
`EmoteQuestion = 8` and `EmoteExclamation = 2` are best guesses from available SDV docs.
Confirm during play-test that:
- "?" emote (stuck step 1) = `doEmote(8)` shows the question mark bubble
- "!" emote (hit reaction) = `doEmote(2)` shows the exclamation bubble
Adjust constants in `ShiftOrchestrator.cs` (`EmoteQuestion`, `EmoteExclamation`) if wrong.

---

## Stories Completed

| Story | Status |
|---|---|
| S-08 Full priority + skip | ✅ |
| S-09 Capability snapshot + tool-missing | ✅ |
| S-16 Stuck escalation | ✅ |
| S-17 Invulnerability + ouch emote | ✅ |
| S-19 Pure logic + PBT | ✅ |

## Deferred

- S-07 (tool-swap visuals), Farmer re-founding, WorkerRenderer → **U-13B**
- Animal tasks + building interiors → **TODO-05 / future unit**
- Tool-missing warning mail → **U-14**
