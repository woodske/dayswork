# Code Generation Plan — U-SVE-02 SVE Farm Maps + Worker Entrance

**Stage**: CONSTRUCTION → U-SVE-02 → Code Generation (Part 1: Planning). No code is written until approved.

## Unit context
- **Story**: S-22. **Requirements**: FR-SVE-05/06/15. **Depends on**: U-SVE-01 (built).
- **Design**: map-signature identity (FD Q1=B); override-first → else existing heuristic (Q2=A); entrances + signatures verified from SVE source (Q3=A).
- **Brownfield**: refine U-SVE-01 Core types in place; modify `ShiftOrchestrator.FindFarmExitTile` in place; extend existing `Dayswork.Tests/Compat` tests.

## Grounded integration point (verified)
`ShiftOrchestrator`: `_farmExitTile = FindFarmExitTile(farm)` (line ~434, computed once per shift) feeds both the 6am spawn (line ~447) and the shift-exit navigation. Modifying the single static `FindFarmExitTile` covers both paths. It already uses `ModEntry` statics, so it can read `ModEntry.ExpansionCompat`.

## Steps

### Core (`Dayswork.Core/Compat/`)
- [x] **Step 1 — `FarmMapSignature.cs`.** `public readonly record struct FarmMapSignature(int Width, int Height, string Discriminator)` (Discriminator empty when unused). *(S-22)*
- [x] **Step 2 — Refine `IExpansionProfile.TryGetEntranceOverride`.** Change signature from `(string farmIdentity, out TileCoord)` to `(FarmMapSignature signature, out TileCoord tile)`. Update `VanillaExpansionProfile` (still always false). *(S-22)*
- [x] **Step 3 — `SveExpansionProfile` entrance table.** Add a `FarmMapSignature → TileCoord` table and implement `TryGetEntranceOverride` against it. Populate entries from SVE map source (Step 6); structure so tiles are trivially adjustable. *(S-22)*

### Mod (`Dayswork/Compat/`, `Dayswork/Orchestration/`)
- [x] **Step 4 — `ExpansionCompatService.TryGetFarmEntranceOverride`.** Replace the name-based call with: guarded extraction of `FarmMapSignature` from the live `farm.Map` (dimensions via `Map.Layers[0]`, optional verified map property), then delegate to `activeProfile.TryGetEntranceOverride(signature, …)`. On extraction failure, return false (→ heuristic). *(FR-SVE-06, NFRU2-02/04)*
- [x] **Step 5 — `ShiftOrchestrator.FindFarmExitTile`.** At method top: if `ModEntry.ExpansionCompat.TryGetFarmEntranceOverride(farm, out var p)` returns true, return `new TileCoord(p.X, p.Y)`; otherwise run the existing warp heuristic unchanged. *(FR-SVE-06)*

### Source verification (the "reference SVE source" step)
- [x] **Step 6 — Verify signatures + entrances from SVE source.** Read each supported farm's map/warp source (IF2R, Grandpa's Farm, Frontier Farm) to confirm `(width,height)` (+ tiebreaker if needed) and derive the farm-side BusStop entrance tile. Encode verified signature→tile entries. Confirmed so far: IF2R `163×156`, Frontier `156×65`; Grandpa's Farm confirmed during this step. **Playtest-pending**: because tiles can't be confirmed in-game here, encoded entrances are source-derived best values flagged for playtest confirmation in the Build & Test scenarios (a map whose heuristic already works needs no entry).

### Tests (`Dayswork.Tests/Compat/`)
- [x] **Step 7 — Update + add tests.** Update `ExpansionProfileNoOpTests` to the new `FarmMapSignature`-keyed `TryGetEntranceOverride`. Add `EntranceOverrideTests`: example (known signature → expected tile; unknown signature → no override; vanilla → no override) + FsCheck (lookup determinism; override strictly precedes a stub heuristic value). *(S-22, S-26 PBT)*

### Verify & document
- [x] **Step 8 — Verify.** `dotnet build Dayswork.sln /p:EnableModDeploy=false` (0/0) and `dotnet test` (all pass). Fix before completion.
- [x] **Step 9 — Code summary + state/audit.** Write `construction/u-sve-02-farm-maps-entrance/code/code-summary.md` (files, source-derived entrances + playtest-pending note, results); update `aidlc-state.md`; append `audit.md`.

## Story traceability
| Story | Steps |
|---|---|
| S-22 (entrance on SVE maps) | 1–7 |
| S-26 (seam extensibility / PBT) | 2, 3, 7 |

## Notes / guardrails
- Vanilla path unchanged: a vanilla signature isn't in the table → heuristic runs exactly as today.
- `Dayswork.Core` stays SMAPI-free (signature *extraction* is in the Mod adapter; the table/lookup are pure).
- Encoded entrance tiles are source-grounded and marked playtest-pending; no blind guesses (NFR-SVE-03).
- This refines U-SVE-01's `IExpansionProfile` signature; the change is small and isolated to the seam + its tests.
