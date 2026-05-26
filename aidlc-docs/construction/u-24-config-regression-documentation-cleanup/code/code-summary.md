# U-24 Code Summary — Config, Regression, and Documentation Cleanup

## Scope

U-24 completes the redesign cleanup pass by removing hourly/deposit-era knobs from the player-facing config surface, rebuilding GMCM around fixed-price and worker-stamina concepts, strengthening deterministic cleanup coverage, and rewriting the build/test docs so they describe the shipped redesign instead of the legacy billing model.

## Application changes

### Redesign-only player config surface

- `Dayswork/Integration/ModConfig.cs`
- `Dayswork/Integration/RuntimeConfigSnapshotMapper.cs`
- `Dayswork.Core/Config/IConfigSnapshot.cs`
- `Dayswork.Core/Config/ConfigDefaults.cs`
- `Dayswork/UI/HiringFlowCoordinator.cs`

Key results:

- Removed legacy hourly/deposit tuning fields and task-rate helpers from the saved/player-facing `ModConfig` shape.
- Kept redesign-era saved knobs only: outdoor thresholds, fixed-price tables, worker stamina, and worker behavior settings.
- `RuntimeConfigSnapshotMapper` now treats redesign-era keys as the only authoritative saved input.
- Legacy hourly/deposit compatibility values are still produced for narrow transitional consumers, but only from internal defaults on the runtime snapshot.

### GMCM and i18n cleanup

- `Dayswork/Integration/GMCMRegistrar.cs`
- `Dayswork/i18n/default.json`

Key results:

- Replaced the old `Rates` screen with three redesign-era groups:
  - `Pricing`
  - `Worker Stamina`
  - `Worker Behavior`
- Added dynamic GMCM labels/tooltips for:
  - outdoor thresholds
  - outdoor service band prices
  - animal-building prices
  - greenhouse package prices
  - daily stamina capacity
  - per-action stamina costs
  - pacing and recovery controls
- Removed stale hourly/deposit GMCM copy from `default.json`.

### Narrow pure helpers for final regression seams

Created:

- `Dayswork.Core/Guards/BulletinBoardInteractionPolicy.cs`
- `Dayswork.Core/Shifts/HitReactionPolicy.cs`

Updated:

- `Dayswork/Patches/BulletinBoardPatch.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.cs`

Key results:

- Multiplayer bulletin-board refusal logic now has a pure decision seam with focused tests.
- Hit-reaction trigger logic now has a pure seam that protects the “fresh swing in range => emote only” behavior.

### Build marker refresh

- `Dayswork/ModEntry.cs`

Updated the startup build marker to `build=U24-Step19`.

## Tests

### Refreshed config tests

- `Dayswork.Tests/Config/RuntimeConfigSnapshotMapperTests.cs`
- `Dayswork.Tests/Config/ConfigDefaultsTests.cs`
- `Dayswork.Tests/Config/ConfigSnapshotFactoryTests.cs`
- `Dayswork.Tests/Config/ConfigValueResolverTests.cs`

Coverage added/refreshed:

- redesign-only normalization behavior
- narrow fallback and threshold-order repair warnings
- internal legacy compatibility defaults staying fenced behind the runtime snapshot
- additional resolver fallback coverage for outdoor thresholds, animal prices, and greenhouse prices

### Dedicated U-24 coverage

Created:

- `Dayswork.Tests/U24/U24PropertyGenerators.cs`
- `Dayswork.Tests/U24/ConfigCleanupPropertyTests.cs`
- `Dayswork.Tests/U24/BulletinBoardInteractionPolicyTests.cs`
- `Dayswork.Tests/U24/HitReactionPolicyTests.cs`

Coverage added:

- normalization idempotence
- snapshot determinism
- compatibility-default stability for internal hourly bridge values
- multiplayer bulletin-board action selection
- hit-reaction debounce/range behavior

### Targeted regression refreshes

- `Dayswork.Tests/Inventory/DepositPlannerTests.cs`
- `Dayswork.Tests/Capabilities/CapabilityEvaluatorTests.cs`
- `Dayswork.Tests/Shifts/StuckDetectorTests.cs`

Coverage added:

- preserved scope provenance on mail fallback routing
- capability independence from irrelevant tool upgrades
- progress clearing stuck state immediately after a stuck condition

### Lint boundary

- No production change was needed in `Dayswork.Tests/Lint/HardcodedUserFacingStringLintTests.cs`.
- The full suite still passed, so the existing i18n boundary remained aligned to the cleanup.

## Documentation rewrite

Rewrote:

- `aidlc-docs/construction/build-and-test/build-instructions.md`
- `aidlc-docs/construction/build-and-test/unit-test-instructions.md`
- `aidlc-docs/construction/build-and-test/integration-test-instructions.md`
- `aidlc-docs/construction/build-and-test/performance-test-instructions.md`
- `aidlc-docs/construction/build-and-test/build-and-test-summary.md`

Created:

- `aidlc-docs/construction/build-and-test/redesign-deviations-and-caveats.md`

Key results:

- All build/test guidance now describes the fixed-price and worker-stamina model.
- The reviewer-facing caveat note consolidates the accepted compatibility leftovers and live-game verification limits in one place.

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false`
  - Passed with `0` errors and `0` warnings
- `dotnet test Dayswork.sln`
  - Passed with `286` tests passing and `1` expected skip

## Accepted cleanup boundary

U-24 intentionally does **not** remove the persisted `DepositAmount` / `HourlyRate` bridge or the narrow legacy hourly estimation code paths yet. Those remain internal compatibility seams only, documented in the reviewer caveats note, and are no longer part of the player-facing config or GMCM model.
