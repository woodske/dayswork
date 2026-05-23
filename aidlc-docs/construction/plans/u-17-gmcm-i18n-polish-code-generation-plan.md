# U-17 — GMCM + i18n Polish: Code Generation Plan

**Unit**: U-17 — GMCM + i18n Polish  
**Stories**: S-13 (full GMCM exposure of configurable values), S-19 (lint completes the maintainability/testability promise), S-20 (hardcoded user-visible string lint gate)  
**Phase**: CONSTRUCTION — Code Generation (Part 1: Planning)

> This plan is the single source of truth for U-17 Code Generation. Generation (Part 2) executes these steps in order **after approval**.

---

## Unit Context

**Components owned (new files)**: M-17 `GMCMRegistrar` (Mod); mutable Mod config source + runtime snapshot mapper seam (Mod); i18n lint test surface under `Dayswork.Tests/Lint/`.

**Components extended**: `Dayswork/ModEntry.cs` (config loading + registrar wiring), `Dayswork/manifest.json` (GMCM optional dependency metadata), `Dayswork/i18n/default.json` (GMCM labels/tooltips and any lint-justified new keys), `Dayswork.Tests/Dayswork.Tests.csproj` (test-only lint dependency if required), and any existing config-facing consumers that currently take `ConfigDefaults.Build()` directly.

**Reused unchanged**: `Dayswork.Core.Config.IConfigSnapshot`, `ConfigSnapshot`, `ConfigDefaults`; pricing/scheduler/orchestrator logic; `I18nHelper`; existing FsCheck/xUnit infrastructure and generators.

**Dependencies satisfied**: U-03 config foundation; U-05 pricing; U-08 i18n helper; U-10/U-15 runtime snapshot semantics; U-02/U-19 test infrastructure obligations as captured in the story map.

**Design constraints baked in**:
- PAT-U17-01 optional dependency probe with clean no-op when GMCM is absent
- PAT-U17-02 mutable Mod config mapped into immutable runtime snapshot
- PAT-U17-03 centralized metadata table for GMCM fields
- PAT-U17-04 all GMCM UI strings through `I18nHelper`
- PAT-U17-05 deterministic hardcoded-string lint with explicit allowlist
- PAT-U17-06 one-time registration / zero tick cost
- PAT-U17-07 preserve "today uses R1, tomorrow uses R2" config-lock semantics

> **Onboarding note (NFR-ONBOARD-01):** the only new SMAPI/modding surface in this unit is Generic Mod Config Menu registration through `Helper.ModRegistry.GetApi(...)`, plus regular SMAPI config read/write flow. The implementation should keep that surface isolated in `Dayswork/Integration/`.

---

## Code Location

- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Mod project**: `Dayswork\`
- **Core project**: `Dayswork.Core\`
- **Tests**: `Dayswork.Tests\`
- **Docs**: `aidlc-docs\construction\u-17-gmcm-i18n-polish\code\`

---

## Steps

### A. Config runtime seam

**Step 1 — Create the mutable Mod config source**
[x] Create a mutable config model in `Dayswork/Integration/` (or adjacent config folder) that represents every player-tunable field required by S-13: base rate, per-task increments, average-speed constant, hard cap time, and both stuck thresholds. Seed it from `ConfigDefaults`-equivalent values and structure it for SMAPI `config.json` persistence. *S-13; PAT-U17-02.*

**Step 2 — Create the runtime snapshot mapper boundary**
[x] Add a dedicated mapping/validation seam that converts the mutable Mod config source into `IConfigSnapshot` / `ConfigSnapshot`, enforcing range checks and preserving the existing immutable runtime contract. This step should be the only path by which edited config reaches pricing/scheduler/orchestration code. *S-13; SAFE-U17-01/03; PAT-U17-02/PAT-U17-07.*

**Step 3 — Rewire `ModEntry` to load real config instead of `ConfigDefaults.Build()` directly**
[x] Modify `Dayswork/ModEntry.cs` so startup reads the mutable Mod config through SMAPI, materializes the validated runtime snapshot via the new mapper, and injects that snapshot into existing services. Preserve current-day config lock semantics by keeping in-flight consumers on the snapshot they were constructed or started with. *S-13; FR-PAY-08; PAT-U17-07.*

### B. GMCM integration

**Step 4 — Create `GMCMRegistrar` (M-17)**
[x] Create `Dayswork/Integration/GMCMRegistrar.cs` with a single optional-dependency registration entrypoint. It must probe GMCM at `GameLaunched`, no-op cleanly if absent, and register the Dayswork config screen if present. *S-13; PAT-U17-01.*

**Step 5 — Centralize GMCM field metadata**
[x] Inside `GMCMRegistrar`, define the registration surface from a single metadata table describing each config field: getter/setter, bounds/validation, display key root, and tooltip key root. Avoid one-off hand-written registration code for each option unless the GMCM API shape forces a tiny wrapper. *S-13; PAT-U17-03.*

**Step 6 — Wire the registrar into the composition root**
[x] Extend `Dayswork/ModEntry.cs` to construct the registrar and invoke it during `GameLoop.GameLaunched`, keeping all GMCM-specific work one-time only and out of the worker tick loop. *S-13; PAT-U17-06.*

**Step 7 — Update manifest metadata for optional GMCM support**
[x] Modify `Dayswork/manifest.json` to add the GMCM optional dependency metadata described in the unit-of-work docs while preserving the existing MFM required dependency. *S-13; COMPAT-U17-01.*

### C. i18n surface

**Step 8 — Extend `Dayswork/i18n/default.json` for the full GMCM surface**
[x] Add all GMCM-facing strings: menu title, section names if used, per-field labels, per-field tooltips, and any user-visible validation or explanatory text needed for "today uses R1, tomorrow uses R2" semantics. No new user-visible English literals should remain in code after this step. *S-13/S-20; PAT-U17-04.*

### D. Tests and maintainability gate

**Step 9 — Add config mapping tests**
[x] Create or extend tests under `Dayswork.Tests/Config/` to cover the mutable-config-to-runtime-snapshot seam: valid mapping, default equivalence, and representative invalid/clamped input behavior. Use example-based tests; add property-style coverage only if a new pure helper with meaningful invariants emerges during implementation. *S-13; SAFE-U17-03; PBT reassessment point.*

**Step 10 — Add the hardcoded user-visible string lint gate**
[x] Create `Dayswork.Tests/Lint/` and implement the U-17 i18n lint test that scans the `Dayswork` source surface for hardcoded user-visible strings outside approved `I18nHelper` callsites. Prefer a deterministic source-level approach; add a test-only Roslyn dependency to `Dayswork.Tests.csproj` only if simpler scanning is too noisy. *S-19/S-20; PAT-U17-05.*

**Step 11 — Encode and document the allowlist**
[x] Build the lint allowlist for known non-user-facing literal classes: manifest keys, console/debug command names, asset identifiers, internal IDs, and test-only literals. Keep the allowlist explicit and reviewable so failures remain actionable. *S-20; SAFE-U17-04; PAT-U17-05.*

### E. Verification and documentation

**Step 12 — `dotnet build`**
[x] Run `dotnet build Dayswork.sln` and fix compile issues across the Mod, Core, and test projects. The resulting mod should still auto-deploy as configured. *Regression gate.*

**Step 13 — `dotnet test`**
[x] Run `dotnet test Dayswork.sln` and confirm the new config tests and lint gate pass alongside existing regressions. If the lint gate exposes pre-existing hardcoded user-facing strings outside U-17 scope, fix them within this unit until S-20 truly holds. *S-19/S-20.*

**Step 14 — Create `aidlc-docs/construction/u-17-gmcm-i18n-polish/code/code-summary.md`**
[x] Document files created/modified, the GMCM registration surface, config snapshot behavior, lint allowlist scope, extension compliance, and a play-test checklist. Checklist should include: GMCM screen appears when installed, no crash when absent, each configurable value edits correctly, active-day config lock still holds, and lint test passes. *Docs + verification.*

**Step 15 — Update `aidlc-state.md` and `audit.md`**
[x] Mark U-17 Code Generation complete when implementation is finished, append audit entries, and update the current stage/next step accordingly. *Workflow bookkeeping.*

---

## Playtest Review Fixes

**Step 16 — GMCM proxy failure: `AddNumberOption` (reverted incorrectly)**
[x] Attempted fix: changed interface to old pre-1.6 names (`RegisterModConfig`, `RegisterLabel`, `RegisterClampedOption`), believing the installed GMCM 1.16.0 still used those names. This was incorrect — `RegisterClampedOption` only exists in `IGenericModConfigMenuApiWithObsoleteMethods` (not in `Framework.Api` that Pintail proxies against). Build marker: `build=U17-Step16`.

**Step 17 — GMCM proxy failure: `RegisterClampedOption` (correct fix)**
[x] Reflected the installed GMCM 1.16.0 DLL (`GenericModConfigMenu.dll`) to enumerate the actual public API surface. Confirmed `GenericModConfigMenu.IGenericModConfigMenuApi` exposes `Register`, `AddSectionTitle`, and `AddNumberOption(int)` / `AddNumberOption(float)` — NOT the old pre-1.6 names. Updated `Dayswork/Integration/IGenericModConfigMenuApi.cs` to use the correct method names and `Func<string>` types for name/tooltip (the original Step 15 failure was `string` vs `Func<string>`). Updated all three `GMCMRegistrar.cs` call sites (`Register`, `AddSectionTitle`, `AddNumberOption`). Verified: `dotnet build Dayswork.sln` 0 errors / 0 warnings, auto-deployed; `dotnet test Dayswork.sln` 211 passed / 1 expected skip. Build marker: `build=U17-Step17`.

**Step 18 — `TypeInitializationException` in `GMCMRegistrar..cctor`: static field init order**
[x] `RateOptions` (declared first) referenced `TaskKindOrder` (declared last) during the static constructor — C# initializes static fields top-to-bottom, so `TaskKindOrder` was `null` when `RateOptions` tried to `.Select()` over it, producing `ArgumentNullException: source`. Fixed by moving `TaskKindOrder` above `RateOptions` (and removing the null-forgiving `!` operator since it's now guaranteed non-null) and removing the now-duplicate declaration at the bottom of the class. Verified: `dotnet build Dayswork.sln` 0 errors / 0 warnings, auto-deployed; `dotnet test Dayswork.sln` 211 passed / 1 expected skip. Build marker: `build=U17-Step18`.

---

## Story Traceability

| Story | Steps |
|---|---|
| S-13 — full GMCM exposure of configurable values | 1–8, 12–14 |
| S-19 — maintainability/testability promise ratified by lint gate | 9–13 |
| S-20 — no hardcoded user-visible strings remain | 8, 10–13 |

## Scope summary

**15 steps** spanning one new production component (`GMCMRegistrar`), one mutable-config/runtime-snapshot seam, manifest/i18n extensions, config tests, and the new `Dayswork.Tests/Lint/` enforcement gate. This unit primarily modifies existing Mod wiring and content files while adding a narrow integration surface in `Dayswork/Integration/` and a deterministic test-time static-analysis layer in `Dayswork.Tests/`.
