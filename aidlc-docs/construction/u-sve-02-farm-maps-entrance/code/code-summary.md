# Code Summary — U-SVE-02 SVE Farm Maps + Worker Entrance

**Stage**: CONSTRUCTION → U-SVE-02 → Code Generation (Part 2). Executed the approved 9-step plan.

## What shipped

The **map-signature entrance mechanism**: the worker's entrance resolver now consults the compat seam (keyed by a signature of the live farm map) before its existing warp heuristic. The mechanism is complete and tested; the SVE entrance-override **table is empty pending playtest** (see below).

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

## Playtest-pending (important, per Q3=A)

The SVE entrance-override table is intentionally **empty**. Entrance tiles could not be confidently attributed per farm from source alone — the `BusStop → Farm` warps are conditional (e.g., IF2R's content.json sets `Farm 86 18` under `FarmType=Standard` + no Grandpa's Farm; warp patches show `Farm 79 17`). Per the approved design, an override is added **only** when playtest confirms the heuristic is wrong on a given farm. Confirmed signatures are documented in `SveExpansionProfile`:
- Immersive Farm 2 Remastered: **163×156**
- Frontier Farm: **156×65** (vanilla Standard Farm is 80×65)
- Grandpa's Farm: dimensions to confirm during its playtest

**Manual playtest step** (to be added to Build & Test): hire a worker on each SVE farm; if it spawns/exits at the wrong tile, add one line to `SveExpansionProfile.EntranceOverrides` mapping that farm's signature to the correct tile (candidates to try: `(79,17)`, `(86,18)`). The mechanism is in place, so this is a one-line, source-grounded change.

## Vanilla invariance
On a vanilla farm the signature isn't in the SVE table → `FindFarmExitTile` runs the existing heuristic byte-for-byte. No vanilla behavior change.

## Verification
- `dotnet build Dayswork.sln /p:EnableModDeploy=false` → **0 warnings / 0 errors**.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` → **341 passed / 1 expected skip / 0 failed** (was 336; +5 from the new/updated Compat tests).

## Story coverage
- **S-22** (entrance on SVE maps): mechanism implemented; entrance values playtest-pending.
- **S-26** (seam extensibility / PBT): signature-keyed seam + FsCheck properties.
