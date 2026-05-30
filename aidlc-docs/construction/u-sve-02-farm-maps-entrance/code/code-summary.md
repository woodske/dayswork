# Code Summary — U-SVE-02 SVE Farm Maps + Worker Entrance

**Stage**: CONSTRUCTION → U-SVE-02 → Code Generation (Part 2). Executed the approved 9-step plan.

## What shipped

The **map-signature entrance mechanism**: the worker's entrance resolver now consults the compat seam (keyed by a signature of the live farm map) before its existing warp heuristic, with a blocked-tile fallback. The mechanism is complete and tested, and two per-farm entrance tiles are confirmed by playtest (see below).

## Files created
- `Dayswork.Core/Compat/FarmMapSignature.cs` — pure `(Width, Height, Discriminator)` value identity.
- `Dayswork.Tests/Compat/EntranceOverrideTests.cs` — xUnit examples + FsCheck (vanilla always-false; SVE deterministic; signature equality for dictionary keying).

## Files modified
- `Dayswork.Core/Compat/IExpansionProfile.cs` — `TryGetEntranceOverride` refined to key on `FarmMapSignature` (was generic `string farmIdentity`).
- `Dayswork.Core/Compat/VanillaExpansionProfile.cs` — signature-keyed no-op.
- `Dayswork.Core/Compat/SveExpansionProfile.cs` — added the `FarmMapSignature → TileCoord` override dictionary (empty pending playtest) + signature-keyed lookup; documented confirmed signatures and source-observed warp candidates.
- `Dayswork/Compat/ExpansionCompatService.cs` — `TryGetFarmEntranceOverride` now computes the signature from the live `farm.Map` (guarded; falls back to heuristic on any failure) and delegates to the active profile.
- `Dayswork/Orchestration/ShiftOrchestrator.cs` — `FindFarmExitTile` consults `ModEntry.ExpansionCompat` first, then the existing warp heuristic (one change covers both spawn and exit).
- `Dayswork.Tests/Compat/ExpansionProfileNoOpTests.cs` — updated to the signature-keyed method.

## Confirmed entrance overrides (2026-05-30 playtest)

Per the approved design (Q3=A), an override is added **only** when playtest confirms the heuristic spawns the worker at the wrong tile. Entrance tiles could not be attributed per farm from source alone (the `BusStop → Farm` warps are conditional), so they were confirmed in-game using a tile-step debug logger. Confirmed map signatures + spawn tiles, both documented and wired in `SveExpansionProfile.EntranceOverrides`:
- **Grandpa's Farm** — signature **140×93** (verified from `GrandpasFarm.tbin`, all 18 layers) → spawn **(112, 51)**.
- **Frontier Farm** — signature **156×65** (vanilla Standard Farm is 80×65) → spawn **(142, 16)**.
- **Immersive Farm 2 Remastered** — signature **163×156**: warp heuristic verified correct in-game, so no override (falls through).

The dimension-reading method was cross-validated against the Frontier/IF2R TMX headers before trusting the Grandpa's Farm `.tbin` parse.

### Blocked-tile fallback
Each configured tile is a **preference**. `ShiftOrchestrator.FindFarmExitTile` routes overrides through `ResolvePassableNearby(preferred, farm)`: returns the preferred tile when a worker can stand on it, else searches outward in expanding Chebyshev rings (radius ≤ 12) for the nearest passable tile via `WorkerMovementDriver.IsTilePassableForWorker`, falling back to the preferred tile (with a warning) if none is found. This covers a spawn tile that is seasonally blocked by debris/placed objects.

## Vanilla invariance
On a vanilla farm the signature isn't in the SVE table → `FindFarmExitTile` runs the existing heuristic byte-for-byte. No vanilla behavior change.

## Verification
- `dotnet build Dayswork.sln /p:EnableModDeploy=false` → **0 warnings / 0 errors**.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` → **343 passed / 1 expected skip / 0 failed** (was 336; +7 from the new/updated Compat tests, incl. the confirmed Grandpa's/Frontier overrides).

## Story coverage
- **S-22** (entrance on SVE maps): mechanism implemented; Grandpa's Farm + Frontier Farm entrance tiles confirmed by playtest, IF2R verified to need none.
- **S-26** (seam extensibility / PBT): signature-keyed seam + FsCheck properties.
