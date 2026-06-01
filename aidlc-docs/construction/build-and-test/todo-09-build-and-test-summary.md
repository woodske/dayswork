# Build & Test Summary — TODO-09 Per-Building Animal Work Ordering (u-t09-animal-ordering)

**Status**: ✅ COMPLETE — approved 2026-05-31 (user: deployed, in-game tested, "everything is approved and tested").

## Scope
Single-unit scheduling change. Base build and unit-test instructions are unchanged from the project baseline (`dotnet build Dayswork.sln` / `dotnet test Dayswork.sln`); no new build steps, dependencies, or tooling.

## Automated Verification
- **Build**: `dotnet build Dayswork.sln -p:EnableModDeploy=false` → 0 warnings / 0 errors.
- **Tests**: `dotnet test Dayswork.sln -p:EnableModDeploy=false` → **382 passed / 1 expected skip / 0 failed** (baseline 378 + 4 new TODO-09 example tests; the per-building grouping PBT property `AnyScopeShape_ProducesPerBuildingGroupedPlan` replaced the prior Kind-monotonic property).
- **Deployed build**: `dotnet build Dayswork.sln` succeeded and copied to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.

## Unit / Property Coverage (added or updated)
- `ShiftPlanBuilderTests`: EX-T09-1 (two buildings Feed/Pet → per-building pairs, no forage), EX-T09-2 (+Collect → single trailing `FarmForage`), EX-T09-3 (one building Collect-only → pair + forage), EX-T09-4 (Feed-only → interior batches only), mixed-scope ordering example, and PBT P-T09-1..6 (per-building pairing/contiguity, building order preserved, single forage positioned last among animal work, grazing-batch count, bounded families, empty skeletons).

## Manual Integration Scenario (confirmed in-game by maintainer)
- With multiple animal buildings spread across the farm, the worker performs all of one building's animal work — interior housed animals, then that building's grazing animals — before moving to the next building (no longer all-interiors-then-one-combined-outdoor-pass).
- Farm-wide truffle forage is still collected (single final `FarmForage` pass, with late-spawn rescan retained).
- No animal left unserviced; vanilla and SVE animal-building behavior otherwise unchanged.

## Deviations
None. Implements TODO-09 as specified (FR-T09-01..08); building visit order intentionally unchanged (proximity routing remains out of scope, FR-T09-07).

## Result
TODO-09 is complete and closed. Operations is a placeholder for this SMAPI mod (deploy = build to Mods folder, already automated).
