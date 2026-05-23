# U-17 GMCM + i18n Polish — Code Summary

**Stage**: CONSTRUCTION — Code Generation  
**Status**: Complete, awaiting user review/approval  
**Verification**: `dotnet build Dayswork.sln` 0 errors / 0 warnings and auto-deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`; `dotnet test Dayswork.sln` 211 passed / 1 expected skip; final startup marker `build=U17-Step16`.

## Created Files

- `Dayswork.Core/Config/ConfigSnapshotFactory.cs` — validated Core config snapshot creation seam.
- `Dayswork/Integration/ModConfig.cs` — mutable SMAPI-persisted config DTO seeded from `ConfigDefaults`.
- `Dayswork/Integration/RuntimeConfigSnapshotMapper.cs` — normalization + immutable snapshot mapping boundary.
- `Dayswork/Integration/ModConfigManager.cs` — config load/reset/save/publish helper for the Mod layer.
- `Dayswork/Integration/IGenericModConfigMenuApi.cs` — minimal GMCM API contract used by Dayswork.
- `Dayswork/Integration/GMCMRegistrar.cs` — optional GMCM probe + one-time registration surface.
- `Dayswork/Properties/AssemblyInfo.cs` — `InternalsVisibleTo("Dayswork.Tests")` for mapper tests without widening runtime API.
- `Dayswork.Tests/Config/ConfigSnapshotFactoryTests.cs` — Core validation tests for snapshot creation rules.
- `Dayswork.Tests/Config/RuntimeConfigSnapshotMapperTests.cs` — mutable-config default, normalization, and clamping tests.
- `Dayswork.Tests/Lint/HardcodedUserFacingStringLintTests.cs` — deterministic source lint gate for user-facing string literals.

## Modified Files

- `Dayswork.Core/Config/ConfigDefaults.cs` — now routes snapshot creation through `ConfigSnapshotFactory`.
- `Dayswork/ModEntry.cs` — loads `ModConfigManager`, wires `GMCMRegistrar` at `GameLaunched`, and updates the build marker.
- `Dayswork/Orchestration/RecurringContractScheduler.cs` — captures the current config snapshot at day start and passes it into new shifts.
- `Dayswork/Orchestration/ShiftOrchestrator.cs` — accepts the runtime snapshot per shift so current-day config remains locked.
- `Dayswork/UI/HiringFlowCoordinator.cs` — reads the live config snapshot from `ModConfigManager` for hire/edit flows.
- `Dayswork/UI/SummaryMenu.cs` — uses i18n for the empty-task fallback label.
- `Dayswork/UI/ContractListMenu.cs` — uses i18n for the empty-task fallback label.
- `Dayswork/manifest.json` — declares GMCM as an optional dependency while preserving MFM as required.
- `Dayswork/i18n/default.json` — adds the GMCM labels/tooltips, timing explanation copy, and `ui.common.none`.
- `Dayswork.Tests/Dayswork.Tests.csproj` — now references `Dayswork` so the mapper seam can be exercised directly from tests.

## Implementation Notes

- GMCM remains fully optional. `GMCMRegistrar` probes `spacechase0.GenericModConfigMenu` once at `GameLaunched` and cleanly no-ops when the API is missing.
- The runtime worker pipeline still consumes immutable Core config only. U-17 adds a mutable Mod-layer DTO and maps it through `RuntimeConfigSnapshotMapper` + `ConfigSnapshotFactory` before pricing/scheduler/orchestration read it.
- Current-day pricing semantics are preserved. `RecurringContractScheduler` captures `_configManager.CurrentSnapshot` at day start, and `ShiftOrchestrator.StartShift(...)` now stores the per-shift snapshot so a GMCM edit mid-day does not retroactively change a running worker.
- GMCM field registration is metadata-driven. Rates and worker settings are defined through centralized option specs rather than one-off ad hoc registration calls.
- Playtest fix Step 16 corrected the GMCM bridge to the API actually exposed by installed GMCM `1.16.0`: `RegisterModConfig`, `RegisterLabel`, and `RegisterClampedOption` instead of the newer `AddNumberOption`-style surface. This resolves the runtime proxy failure `Tried to map a mod-provided API to interface 'Dayswork.Integration.IGenericModConfigMenuApi'`.
- The lint gate is intentionally source-level and deterministic. It scans `Dayswork/**/*.cs`, skips `bin/`, `obj/`, and the reflection-heavy Mail Framework adapter, strips interpolation holes before classification, and carries an explicit allowlist for technical/debug-only contexts such as logs, console commands, asset ids, reflection strings, and internal component names.
- U-17 also removes the remaining hardcoded `"—"` UI fallback and routes that text through `ui.common.none`.

## Lint Allowlist Scope

- Approved i18n callsites: `I18nHelper.Get(...)`, `_helper.Translation.Get(...)`.
- Approved non-user-facing contexts: debug/log formatting, debug console command metadata, sound ids, asset paths, SMAPI/mod ids, reflection/member names, location/task key plumbing, and internal clickable-component ids/names.
- File-level exclusion: `Dayswork/Integration/MailFramework/` because the adapter intentionally embeds reflection type/member names from another mod's API surface.

## Play-Test Checklist

- [ ] With GMCM installed, the Dayswork config screen appears and exposes all rates + worker timing fields.
- [ ] Without GMCM installed, the mod loads normally and existing hiring/shift flows still work.
- [ ] Editing base rate or per-task rates changes newly confirmed hire pricing and the next eligible recurring-day rate/deposit.
- [ ] Editing config during an already-running worker day does not change that active shift's already-locked pricing/refund behavior.
- [ ] Average speed, hard cap, and stuck timing edits persist through `config.json` and reload correctly.
- [ ] The empty-task/empty-contract UI fallback still renders correctly via `ui.common.none`.
- [ ] `dotnet test Dayswork.sln` continues to pass the lint gate after any additional U-17 text/config tweaks.

## Extension Compliance

- **Property-Based Testing**: Compliant for Partial mode. `PBT-09` remains satisfied by FsCheck + xUnit; U-17's new coverage is example-based because the new seams are integration/static-validation oriented. No new pure property surface required additional FsCheck properties.
- **Security Baseline**: N/A / disabled in `aidlc-state.md`.
