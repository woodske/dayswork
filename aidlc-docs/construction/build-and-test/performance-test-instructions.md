# Performance Test Instructions — Dayswork SMAPI Mod

## Status: N/A for v1

Dayswork is a single-player Stardew Valley SMAPI mod. There is no server, no concurrent user
load, and no network layer. Formal load/stress/throughput testing is not applicable.

## Relevant Performance Considerations

Performance concerns for a SMAPI mod are in-game frame-rate impact and tick-budget usage:

### Tick Budget
- SMAPI mods run on the game's update loop (~60 ticks/second).
- Dayswork's worker tick does: one pathfinding step, one task check, one action attempt.
- Heavy scanning (zone tile iteration) only occurs at shift-start, not every tick.
- No observable frame drops expected at normal farm sizes.

### Scanning Performance (informal)
To spot-check scan time, search the SMAPI log for `[Dayswork][scan]` summary lines.
Each line reports `scannedTiles`, `acceptedItems`, and detected/accepted counts per task kind.
A typical farm zone of ~500 tiles scans in well under one game tick.

### If Performance Issues Are Observed
1. Enable SMAPI `Trace` logging: type `log level trace` in the SMAPI console.
2. Look for repeated `[Dayswork][scan]` entries that should only appear once per shift.
3. Look for `[Dayswork][tick]` entries that fire more than once per game update — this
   would indicate an event subscription bug.
4. Profile with a .NET profiler attached to the `Stardew Valley.exe` process if deeper
   analysis is needed (JetBrains dotTrace or Visual Studio Diagnostic Tools).
