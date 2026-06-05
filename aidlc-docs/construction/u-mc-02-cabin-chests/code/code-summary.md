# Code Summary - U-MC-02 Cabin Chests

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - Code Generation
**Status**: Complete; review required before continuing
**Completed**: 2026-06-05

## Summary

U-MC-02 adds distinct farmhand cabin input and output chest roles. The input chest is the crop-management supply reservoir and is excluded from selectable output destinations. The output chest remains the automatic fallback/default destination and stays selectable by the player.

Existing farmhand cabins are backfilled idempotently on save load and day start. New input chests preserve Stardew's stable building-chest ID in `Chest.Name`; role labels are applied through Stardew's localized `displayNameFormat` token so lookups continue to work across saves and multiplayer sync.

## Application Changes

- Added `CabinChestService` to ensure the input chest exists, apply localized display-name tokens, and expose role helpers.
- Updated `HiringBuilding` with `InputChestId`, input/output display tiles, dual `BuildingData.Chests` declarations, and tile lookup helpers.
- Updated `HiringBuildingInteraction` so action-clicking the input display tile opens the input chest and action-clicking the output display tile opens the output chest.
- Updated `ChestResolver` so the farmhand cabin input chest is excluded from output-destination selection while the output chest remains selectable.
- Wired `CabinChestService` to low-frequency `SaveLoaded` and `DayStarted` events in `ModEntry`.
- Added i18n keys for farmhand cabin input/output chest role labels.

## Tests

- Added/updated focused integration tests for chest declarations, localization token formatting, and selectable-destination exclusion behavior.
- Direct live Stardew object-construction tests remain constrained by game assembly/runtime loading in the test project, so example coverage uses pure helpers and source-level assertions where needed.

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` passed with 0 warnings and 0 errors.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false --no-build` passed with 379 passed, 1 skipped, 0 failed.
- Duplicate-file and scope checks passed: no `_new`/`_modified` duplicate application files, no application code under `aidlc-docs/`, no project dependency changes, and no per-frame cabin chest service wiring.

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops; U-MC-02 adds local SMAPI chest routing/backfill only. |
| Property-Based Testing | Compliant | Functional Design identified properties; Q5 selected example-focused coverage for live Stardew APIs. No new PBT generator was required for this unit. |

## Review Notes

- The stable chest ID/display-name split is intentional: Stardew's `Building.GetBuildingChest(id)` matches against `Chest.Name`, so localized role text must not overwrite `Name`.
- Later Manage Crops units should draw crop supplies from `HiringBuilding.InputChestId` and may deposit task output to `HiringBuilding.OutputChestId` according to the selected destination/fallback rules.
