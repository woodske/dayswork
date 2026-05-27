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

### Scenario 2: Outdoor Tile Routing

Setup:

- Start a shift with a large outdoor batch: weeds, grass, rocks, crops, trees, or mixed clearing/crop work.

Expected results:

- Worker chooses nearer reachable tasks within the active broad batch.
- FPS does not collapse when outdoor tile work begins.
- Worker continues to recompute routes after completed work changes passability.

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

## Logs To Check

- SMAPI console/log for `[Dayswork][routing]`, `[Dayswork][building]`, `[Dayswork][deposit]`, and `[Dayswork][exit]` diagnostics.
- Warnings should be narrow and explain skipped blocked work or unreachable deposit destinations.

## Cleanup

- Remove or reset any test chests, placed products, and farm blockers created for play-testing.
- Rebuild or redeploy the mod only after source changes.
