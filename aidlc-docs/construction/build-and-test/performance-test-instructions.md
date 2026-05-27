# Performance Test Instructions - U-WR Worker Routing and Dynamic Task Selection

## Purpose

Validate that worker route selection remains responsive during large outdoor tile batches and does not repeat expensive route searches per candidate.

## Performance Requirements

- Outdoor tile work must not drop the game to the reported 1 FPS failure mode.
- Route scoring should compute one exact route-cost map per active-batch selection.
- Route costs should be recomputed only at task boundaries or world-state changes, not every frame.
- Blocked work must terminate through bounded retry behavior, not loop indefinitely.

## Manual Performance Test

### 1. Prepare A Large Outdoor Batch

- Use a farm area with many weeds, grass, rocks, crops, trees, or mixed targets.
- Enable the relevant worker tasks in the contract.
- Prefer a setup with some blockers so rerouting and retry behavior are exercised.

### 2. Deploy And Run

```powershell
dotnet build Dayswork.sln
```

Then start Stardew Valley through SMAPI and start the worker shift.

### 3. Observe Runtime Behavior

Check:

- FPS remains playable when outdoor tile work begins.
- The game does not freeze or stutter heavily at each task boundary.
- Worker movement remains visibly route-driven.
- SMAPI log does not flood with repeated routing warnings.

### 4. Stress Blocked Routes

Add or preserve blockers around:

- eggs/products in coops
- feed hoppers or troughs
- outdoor debris clusters
- assigned output chests

Expected results:

- Worker performs reachable nearby work first.
- Worker retries blocked work after progress.
- Worker skips no-progress blocked remainder instead of looping.

## Automated Performance Proxy

Run:

```powershell
dotnet test Dayswork.sln
```

The automated tests do not measure FPS, but they protect the route-selection semantics and bounded retry examples that support the performance design.

## If Performance Regresses

1. Capture the SMAPI log around the slowdown.
2. Note the active work type: barn, coop, greenhouse, outdoor crop, outdoor clearing, or deposit.
3. Record whether the slowdown happens every frame or only at task boundaries.
4. Recheck for accidental per-candidate or per-frame route search loops.
