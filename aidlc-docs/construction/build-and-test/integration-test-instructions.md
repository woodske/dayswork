# Integration Test Instructions - U-WR Worker Routing and Dynamic Task Selection

## Purpose

Validate that the routing changes work across scanner, animal handling, movement, orchestration, output deposit, and live Stardew map collision behavior.

Automated tests cover the pure routing seams and focused regression logic. Full SMAPI/Stardew integration still requires in-game play-testing because building interiors, animal positions, placed objects, and map collision are runtime-owned.

## Automated Integration-Like Coverage

Run:

```powershell
dotnet test Dayswork.sln
```

Relevant automated scenarios:

- route-ranked active-batch selection
- blocked candidate filtering and retry
- feed/product blocker handling
- building exit approach tile selection
- chest deposit stand-tile selection
- output routing and overflow behavior

## Manual In-Game Integration Scenarios

### Scenario 1: Barn/Coop Local Routing

Setup:

- Start a shift with barn or coop animal work enabled.
- Include animals needing attention and placed products such as eggs where possible.

Expected results:

- Worker chooses a reachable side of eggs/products.
- Worker does not abandon reachable eggs because the top side is blocked.
- Worker handles nearer actionable animals before walking past them to far animals.
- Feed work can proceed after enabled product collection clears blockers.
- Sheep wool collection plays the vanilla `scissors` cue, and SMAPI logs no invalid audio-id warning.

### Scenario 2: Outdoor Tile Routing

Setup:

- Start a shift with a large outdoor batch: weeds, grass, rocks, crops, trees, or mixed clearing/crop work.

Expected results:

- Worker chooses nearer reachable tasks within the active broad batch.
- FPS does not collapse when outdoor tile work begins.
- Worker continues to recompute routes after completed work changes passability.
- If an `Error Item` appears on an outdoor work tile, SMAPI logs a narrow `[Dayswork][debris] worker-created debris could not be resolved...` warning with task/location context so the offending drop source can be identified.

### Scenario 3: Building Exit Walk-Out

Setup:

- Start barn, coop, or greenhouse work.
- Let the worker finish all interior tasks.

Expected results:

- Worker walks to a reachable interior exit approach tile.
- Worker transitions outside only after reaching the exit approach.
- Worker does not visibly warp out from the final task location.

### Scenario 4: Chest Deposit Travel

Setup:

- Assign at least one task output to a chest.
- Run work that produces depositable materials.

Expected results:

- Worker walks to a reachable tile adjacent to the chest.
- Items are transferred only after arrival.
- If the chest cannot be reached, items are mailed as undelivered rather than inserted automatically.

### Scenario 5: Missing or Full Chest

Setup:

- Assign outputs to a chest.
- Move/destroy the chest or fill it before the deposit run.

Expected results:

- Missing chest items are mailed with `ChestMissing` reason.
- Full chest leftovers are mailed with `ChestFull` reason.
- No items are lost.

### Scenario 6: Exit To Title Or Desktop Mid-Shift

Setup:

- Start a worker shift and let the worker make visible progress so buffer, stamina, and location state are non-default.
- Use `Exit to Title`, then reload the same save. Optionally repeat by exiting the game entirely and relaunching.

Expected results:

- The previous in-memory worker does not continue from the old mid-shift position.
- The old shift's buffered items, stamina, deferred work, and navigation state do not carry across the session boundary.
- Reload follows the save's real morning/day-start state, so any worker spawn is a fresh day-start execution rather than a resumed in-memory shift.
- No stale worker UI/runtime state survives after returning to the title screen.

## Logs To Check

- SMAPI console/log for `[Dayswork][routing]`, `[Dayswork][building]`, `[Dayswork][deposit]`, `[Dayswork][exit]`, and `[Dayswork][debris]` diagnostics.
- For title-return repros, look for `[Dayswork] Resetting in-memory worker runtime for session boundary ReturnedToTitle.` and `... SaveLoaded.`
- Warnings should be narrow and explain skipped blocked work or unreachable deposit destinations.

## Cleanup

- Remove or reset any test chests, placed products, and farm blockers created for play-testing.
- Rebuild or redeploy the mod only after source changes.
