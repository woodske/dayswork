# U-24 — Config, Regression, and Documentation Cleanup: Code Generation Plan

**Unit**: U-24 — Config, Regression, and Documentation Cleanup  
**Phase**: CONSTRUCTION — Code Generation (Part 1 — Planning)  
**Builds on**: approved Functional Design, approved NFR Requirements, and approved NFR Design for the redesign-only player-facing config surface, deterministic config normalization/fallback, strict i18n/documentation coherence, and the final targeted regression sweep.

> **This plan is the single source of truth for U-24 Code Generation.** Part 2 executes these steps in order, checking each box on completion. No code, tests, or documentation work should happen outside these steps.

---

## Stories & Traceability

| Story / Requirement | Coverage in U-24 |
|---|---|
| **S-01** | Final regression-verifies the bulletin-board hire entry and multiplayer refusal behavior still hold after the redesign. |
| **S-04** | Final regression-verifies output destination routing and overflow behavior under the redesign-era runtime. |
| **S-09** | Final regression-verifies tool snapshot and skip rules remain intact after runtime/config changes. |
| **S-11** | Final regression-verifies overflow and unassigned-output mail behavior still matches the typed-scope/runtime model. |
| **S-13** | Replaces the old hourly/deposit GMCM surface with redesign-era pricing, stamina, and worker-behavior controls. |
| **S-16** | Final regression-verifies stuck recovery behavior still holds under the redesign runtime/config stack. |
| **S-17** | Final regression-verifies invulnerability / hit-reaction continuity remains intact after the runtime redesign. |
| **S-18** | Final regression-verifies multiplayer refusal remains friendly and intact. |
| **S-19** | Strengthens pure deterministic config/fallback and cleanup regression seams with focused example + FsCheck coverage. |
| **S-20** | Finishes routing redesign-visible strings through i18n and keeps the lint/documentation boundary authoritative. |

**Definition of Done** (from [unit-of-work.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/inception/application-design/unit-of-work.md)):
- GMCM exposes price, energy, pacing, and threshold knobs matching the redesign.
- All new strings are routed through i18n.
- Build/test docs describe the fixed-price and worker-energy model rather than hourly deposits/refunds.
- Regression coverage verifies historically unchanged behaviors still work after the redesign.

---

## Project Context & Execution Boundaries

- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Solution shape**: existing multi-project mod solution with `Dayswork.Core`, `Dayswork`, and `Dayswork.Tests`
- **Brownfield retrofit rule**: all target files are modified in place or created fresh in the existing structure; never create duplicate `*_new.cs` or `*_modified.cs` files
- **Layer mapping for this unit**:
  - **Player-facing config + runtime publication**: `Dayswork/Integration/`, `Dayswork/Core/Config/`, and any narrow consumers that still depend on old config concepts
  - **UI text / GMCM copy**: `Dayswork/i18n/default.json`
  - **Regression tests**: `Dayswork.Tests/` (existing config, lint, inventory, capability, shift, and any new focused `U24/` coverage)
  - **Documentation**: `aidlc-docs/construction/build-and-test/` plus `aidlc-docs/construction/u-24-config-regression-documentation-cleanup/code/`
  - **API / Infra / Deployment artifacts**: N/A in this unit

### Explicit retrofit constraints for U-24

1. **U-24 is the final cleanup/regression unit, not another pricing/runtime redesign unit.**  
   Do not reopen U-18 through U-23 behavior decisions unless a cleanup change requires a tightly scoped follow-through fix.

2. **The saved `config.json` shape must become redesign-only.**  
   Player-facing hourly/deposit-era config fields must not survive in `ModConfig` or GMCM after this unit.

3. **Internal compatibility is allowed only behind non-player-facing seams.**  
   If old hourly/deposit values are still needed temporarily for persistence or compatibility bridges, they must be derived or quarantined internally, not exposed as ordinary player tuning knobs.

4. **Deterministic normalization and narrow fallback are core requirements.**  
   `RuntimeConfigSnapshotMapper`, `ConfigValueResolver`, and related tests must stay the authoritative path for redesign-era config publication.

5. **Stale docs are a regression surface.**  
   The build/test docs and reviewer-facing deviations note must become redesign-native; do not leave hourly/deposit guidance behind as an addendum.

6. **The regression sweep stays targeted.**  
   Focus on output routing/overflow, tool snapshot/skip, stuck recovery, invulnerability, multiplayer guard, i18n boundary, and cleanup-coherence invariants. Do not turn U-24 into a full replay of every historical unit.

---

# PART 1 — PLANNING (this document)

Steps 1–19 below. Approval of this plan authorizes Part 2 (execution).

---

# PART 2 — GENERATION STEPS

## Phase A — Redesign-only config surface and publication cleanup

- [x] **Step 1 — Reshape `ModConfig` to the redesign-only saved surface.** Remove the old hourly/deposit-era public config fields and helper accessors from [ModConfig.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Integration/ModConfig.cs), keep only the redesign-era persisted knobs, and preserve any still-needed compatibility data only behind non-player-facing code paths. *S-13, S-19*

- [x] **Step 2 — Update deterministic normalization and fallback for the clean-break config shape.** Refactor [RuntimeConfigSnapshotMapper.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Integration/RuntimeConfigSnapshotMapper.cs) so redesign-era keys remain the sole authoritative saved input, per-key fallback stays narrow and warning-backed, and removed legacy saved fields resolve only through internal defaults where still needed. *S-13, S-19*

- [x] **Step 3 — Adjust config snapshot/default seams only as needed to support the cleanup.** Update [IConfigSnapshot.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Config/IConfigSnapshot.cs), [ConfigSnapshot.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Config/ConfigSnapshot.cs), [ConfigSnapshotFactory.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Config/ConfigSnapshotFactory.cs), and [ConfigDefaults.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Config/ConfigDefaults.cs) so the redesign-authoritative fields stay coherent while any remaining compatibility values are clearly internalized. *S-13, S-19*

- [x] **Step 4 — Update config publication wiring and any stale hourly-config consumers.** Refactor [ModConfigManager.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Integration/ModConfigManager.cs), [ModEntry.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/ModEntry.cs), and any narrow consumers such as [HiringFlowCoordinator.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/UI/HiringFlowCoordinator.cs) if needed so the redesign-only config shape publishes cleanly without exposing removed hourly/deposit knobs back to players. *S-13, S-19*

## Phase B — GMCM and i18n cleanup

- [x] **Step 5 — Rebuild the GMCM surface around redesign-era sections.** Rewrite [GMCMRegistrar.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Integration/GMCMRegistrar.cs) so it exposes only the approved `pricing`, `worker stamina`, and `worker behavior` groups, including thresholds, outdoor/animal/greenhouse prices, daily stamina, per-action costs, and pacing/recovery settings. *S-13, S-20*

- [x] **Step 6 — Refresh redesign-era GMCM/player-facing i18n keys.** Update [default.json](/C:/Users/kwood/Repos/dayswork/Dayswork/i18n/default.json) to remove stale hourly/deposit GMCM strings, add the new redesign-era labels/tooltips, and keep any remaining U-24 player-visible cleanup text fully i18n-routed. *S-13, S-20*

## Phase C — Deterministic config + cleanup regression coverage

- [x] **Step 7 — Refresh config example tests for the redesign-only saved shape.** Update [RuntimeConfigSnapshotMapperTests.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Tests/Config/RuntimeConfigSnapshotMapperTests.cs), [ConfigDefaultsTests.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Tests/Config/ConfigDefaultsTests.cs), [ConfigSnapshotFactoryTests.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Tests/Config/ConfigSnapshotFactoryTests.cs), and any related config smoke tests so they assert the redesign-only shape, deterministic publication, and narrow fallback behavior. *S-13, S-19*

- [x] **Step 8 — Refresh `ConfigValueResolver` coverage for malformed/missing redesign-era input.** Expand [ConfigValueResolverTests.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Tests/Config/ConfigValueResolverTests.cs) and any neighboring helpers so per-key fallback stability and warning-worthy edge cases remain explicit. *S-13, S-19*

- [x] **Step 9 — Add dedicated U-24 property/example test support for cleanup invariants.** Create a focused `Dayswork.Tests/U24/` area (or equivalent) with generators and tests that cover deterministic normalization, narrow fallback stability, and other cleanup invariants suited to FsCheck. *S-19*

- [x] **Step 10 — Refresh targeted output-routing and overflow regressions.** Extend [DepositPlannerTests.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Tests/Inventory/DepositPlannerTests.cs), [OverflowCategorizerTests.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Tests/U22/OverflowCategorizerTests.cs), and any neighboring output/mail tests as needed so typed-scope routing and next-morning overflow behavior remain correct after the cleanup. *S-04, S-11, S-19*

- [x] **Step 11 — Refresh targeted tool snapshot and skip-rule regressions.** Extend [CapabilityEvaluatorTests.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Tests/Capabilities/CapabilityEvaluatorTests.cs) and any narrow scanner/runtime tests needed so snapshot-at-spawn capability rules and skip semantics remain protected. *S-09, S-19*

- [x] **Step 12 — Refresh targeted stuck recovery, invulnerability, and multiplayer-guard regressions.** Add or update focused tests for [StuckDetectorTests.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Tests/Shifts/StuckDetectorTests.cs), the worker hit-reaction/invulnerability runtime seam, and the multiplayer bulletin-board guard path ([BulletinBoardPatch.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Patches/BulletinBoardPatch.cs), [MultiplayerGuard.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Guards/MultiplayerGuard.cs)) so those historically unchanged behaviors remain verified after the redesign. *S-01, S-16, S-17, S-18, S-19*

- [x] **Step 13 — Keep the hardcoded-string lint gate aligned to the cleanup boundary.** Update [HardcodedUserFacingStringLintTests.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Tests/Lint/HardcodedUserFacingStringLintTests.cs) only as needed so the player-visible redesign surface remains enforced while approved technical/debug exemptions still pass. *S-20, S-19*

## Phase D — Build/test documentation and reviewer-facing cleanup note

- [x] **Step 14 — Rewrite the detailed build instructions to the redesign-native model.** Update [build-instructions.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/construction/build-and-test/build-instructions.md) so it describes the current fixed-price / worker-energy configuration and runtime model rather than the hourly/deposit system. *S-13, S-19*

- [x] **Step 15 — Rewrite the detailed test instruction files and add the explicit regression checklist.** Update [unit-test-instructions.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/construction/build-and-test/unit-test-instructions.md), [integration-test-instructions.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/construction/build-and-test/integration-test-instructions.md), and [performance-test-instructions.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/construction/build-and-test/performance-test-instructions.md) so they describe the redesign behavior and explicitly call out the final regression surfaces. *S-04, S-09, S-11, S-16, S-17, S-18, S-19, S-20*

- [x] **Step 16 — Rewrite the build/test summary and add the reviewer-facing redesign deviations note.** Update [build-and-test-summary.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/construction/build-and-test/build-and-test-summary.md) and create a bounded `redesign-deviations-and-caveats.md` note under `aidlc-docs/construction/build-and-test/` so the reviewer-facing documentation stays coherent and redesign-native. *S-19, S-20*

## Phase E — Verification and unit closeout

- [x] **Step 17 — Run verification for the completed U-24 cleanup.** Execute `dotnet build Dayswork.sln /p:EnableModDeploy=false` and `dotnet test Dayswork.sln`, then fix any U-24 regressions required to restore a green build/test state. *All U-24 stories*

- [x] **Step 18 — Perform a quick artifact coherence sweep.** Re-read the rewritten GMCM/i18n surface and the updated build/test docs to confirm the redesign-only config story, the regression checklist, and the reviewer-facing caveat note all match the shipped behavior before summarizing the unit. *S-13, S-19, S-20*

- [x] **Step 19 — Write the U-24 code summary and close the unit plan.** Document the modified/created files, key config/GMCM/test/doc cleanup changes, and verification results in `aidlc-docs/construction/u-24-config-regression-documentation-cleanup/code/code-summary.md`, then mark the plan complete. *All U-24 stories*
