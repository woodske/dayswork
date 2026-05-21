# U-13B Code Summary — NPC Worker + Task Visuals

**Unit**: U-13B — Worker Actor + Tool Visuals  
**Status**: Review-change implementation complete; build and automated tests pass.  
**Verification**: `dotnet build` succeeded with 0 errors / 0 warnings. `dotnet test` passed 184 tests with 1 expected skipped PBT-08 smoke demo.

## Architectural Review Change

After play-testing the Farmer-backed worker, the implementation has pivoted back to an NPC-backed worker. The current worker is again a normal `FarmhandNpc` added to the farm character list, so Stardew's normal world rendering/depth behavior applies. The accepted U-13/U-13B behavior fixes remain in place: nearest-task routing, resource-clump support, action diagnostics, pause gating, morning entrance hold, direct walking fallback, deterministic rock removal, repeated axe swings until vanilla tree removal completes, and a visible final walk out through the farm entrance after deposit.

The visual animation target is now Stardew-Squad-style NPC task animation rather than vanilla Farmer tool animation. `ToolSwapAnimator` plays a small callback-free two-frame NPC work beat by facing direction, spawns a matching world tool-swing sprite, then returns the NPC to the matching idle frame. This avoids the Farmer callback/null-ref/body-corruption path entirely.

## Created / Restored

| File | Purpose |
|---|---|
| `Dayswork/Worker/FarmhandNpc.cs` | Restored the NPC-backed worker actor using the Marnie placeholder sprite/portrait, with helper methods for idle/task animation state. |

## Modified

| File | Change |
|---|---|
| `Dayswork/Orchestration/ShiftOrchestrator.cs` | Rewired worker creation/removal to `FarmhandNpc` + `farm.addCharacter`; preserved pause gating, morning entrance hold, navigation/action diagnostics, nearest-task routing, recovery, refund, deposit, repeated action behavior, and now starts a visible final exit walk before cleanup. |
| `Dayswork/Worker/WorkerMovementDriver.cs` | Retargeted the path-copy/manual-step movement driver from standalone `Farmer` to `FarmhandNpc`; uses NPC walk frames, a worker-passable BFS fallback that rejects farm building footprints, and a short forced pixel route for the final exit animation. |
| `Dayswork/Worker/ToolSwapAnimator.cs` | Replaced Farmer tool-pose animation with callback-free NPC work frames plus world `TemporaryAnimatedSprite` swings for axe, pickaxe, watering can, and scythe. |
| `Dayswork/ModEntry.cs` | Removed manual `RenderedWorld` worker drawing and restored the NPC portrait asset redirect. |
| `Dayswork.Core/Domain/WorkerTool.cs` | Retained the pure task-to-tool map for future visual/tool policy use. |
| `Dayswork.Tests/Domain/WorkerToolTests.cs` | Retained exhaustive finite mapping coverage. |

## Removed From Active Path

| File | Reason |
|---|---|
| `Dayswork/Worker/FarmhandWorker.cs` | Farmer-backed actor path rejected after play-test. |
| `Dayswork/Worker/WorkerAppearance.cs` | Farmer character-creation appearance no longer used. |
| `Dayswork/Worker/WorkerAppearanceRandomizer.cs` | Farmer character-creation appearance no longer used. |
| `Dayswork/Worker/WorkerRenderer.cs` | Normal NPC rendering through the farm character list replaces manual `RenderedWorld` drawing. |

## Behavior Preserved From Play-Test Fixes

| Feedback / issue | Current behavior |
|---|---|
| Worker should pause when menu/time is stopped | `ShiftOrchestrator.OnUpdateTicked` still gates movement, task animation, and decision logic behind `Game1.shouldTimePass(false)`. |
| NPC shows only a static tool icon, not a swing | `ToolSwapAnimator` now spawns direction-specific world swing sprites from vanilla tool/animation sheets, matching the Stardew Squad approach more closely. |
| Worker starts before the farmer wakes up | Shift startup still holds the worker briefly at the farm entrance before movement/action updates begin. |
| Worker prioritized grass | Work-list construction still routes greedily to the nearest accepted item; object/resource-clump detection runs before grass fallback. |
| Tiny rock-only zone completed with 0 hours and no work | Scan/nav/action diagnostics, reachable navigation tiles, resource-clump handling, and deterministic regular stone removal are retained. |
| Worker teleported after tasks | The direct fallback remains a tile-by-tile walking route rather than a position jump. |
| Worker cut trees in one hit | Tree tasks repeat action beats until vanilla `Tree.performToolAction` reports removal. |
| NPC walks through buildings | Manual fallback routing now uses four-way BFS over worker-passable tiles and rejects farm building footprints; stand-tile selection uses the same stricter passability check. |
| Wood pieces remain on the ground / materials are missed | Debris collection now handles Stardew material chunk debris such as `woodDebris` and `bigWoodDebris`, not only debris with a non-null `item`. |
| Wood appears after the tree-fall animation | Non-stump tree hits now queue a short delayed debris sweep around the tree tile, so wood spawned after the fall/break animation is still collected before deposit/save. |
| NPC teleports away after depositing | After depositing, the NPC walks to the entrance, then continues through a short final visible exit route before the shift-complete cleanup removes the actor. |

## Extension Compliance

| Rule | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled project-wide; this change adds no network, PII, auth, or external-input surface. |
| PBT-02 | N/A | No new serialization, parsing, or round-trip operation. |
| PBT-03 | Compliant | The finite `WorkerTool.ForTask` map remains exhaustively tested across every `TaskKind`. |
| PBT-07 | N/A | No new domain generator needed. |
| PBT-08 | Compliant | Existing FsCheck seed/shrinking convention remains in the test suite; no new property test was required. |
| PBT-09 | Compliant | Existing xUnit + FsCheck stack unchanged. |

## Play-Test Checklist

- Worker appears at the farm entrance as the placeholder NPC and then walks into the farm after the short morning hold.
- Worker draws with normal world object depth because it is back in the farm character list.
- Worker movement no longer uses standalone Farmer pose/rendering code.
- Each task plays a brief facing-direction NPC work beat without Farmer tool callbacks.
- Axe, pickaxe, watering can, and scythe tasks show visible swing sprites during the work beat.
- Worker should route around buildings instead of through their footprints.
- Wood/hardwood/stone/ore chunk debris created by worker actions should be converted into buffered shipping items and removed from the ground.
- Wood created a moment after a falling tree turns into a stump should be collected during the delayed sweep window.
- After depositing, the worker should visibly walk through/past the farm entrance before disappearing.
- Nearest-target routing should still pick the closest accepted grass/weeds/rocks/trees/logs item, with animal tasks still deferred.
- Rocks, weeds, grass, resource clumps, twigs, and trees should continue to clear using the accepted task-action behavior.
- Save during an active shift removes the NPC worker and preserves refund behavior.
