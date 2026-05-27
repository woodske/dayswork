# Unit Test Execution - U-WR Worker Routing and Dynamic Task Selection

## Run Unit Tests

### 1. Execute All Tests

```powershell
dotnet test Dayswork.sln
```

Expected result for the current U-WR review build:

- Passed: `318`
- Failed: `0`
- Skipped: `1`
- Total: `319`

The skipped test is the existing PBT seed/shrinking demonstration:

- `Dayswork.Tests.Smoke.SeedLoggingDemoTests.PBT08_seed_and_shrunk_input_logged_on_failure`

### 2. Optional Compile-Only Test Build Without Mod Deployment

If the game/mod folder is locked, run:

```powershell
dotnet test Dayswork.sln /p:EnableModDeploy=false
```

The default `dotnet test Dayswork.sln` path may copy the mod to the configured Stardew Valley `Mods\Dayswork` folder during the test build.

## Test Areas Covered

- Pure route selection:
  - shortest reachable route wins
  - equal-distance task priority
  - stable-order tie-breaks
  - unreachable candidate filtering
- Worker routing regressions:
  - wrong-side/current-side interaction
  - one-side-blocked egg/product collection
  - nearer animal before farther animal inside active batch
  - feed retry after product collection clears blockers
  - disabled product collection does not clear unpaid product blockers
  - blocked active batch terminates without looping
- Review fixes:
  - outdoor tile route-cost map selection
  - building exit reachable approach selection
  - chest deposit reachable adjacent stand-tile selection
  - sheep shears audio cue uses vanilla `scissors`
  - debris item-id normalization rejects incomplete IDs like bare `(O)` before they can enter the worker buffer
  - session reset handler clears stale in-memory worker state on title-return and before save-load hydration
- Existing project coverage:
  - pricing
  - persistence
  - output routing
  - config mapping
  - UI view-model logic
  - source lint checks

## If Tests Fail

1. Read the first failing test and stack trace.
2. If the failure is in FsCheck, preserve the replay seed from the output.
3. Fix the smallest relevant production or test seam.
4. Rerun:

```powershell
dotnet test Dayswork.sln
```

5. Update the build-and-test summary if final counts change.
