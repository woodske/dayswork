# U-17 — Tech Stack Decisions

**Unit**: U-17 — GMCM + i18n Polish

---

## Decisions

### TS-U17-01 — Keep the existing split: immutable Core config, mutable Mod integration
- **Decision**: Preserve `Dayswork.Core.Config.IConfigSnapshot` / `ConfigSnapshot` / `ConfigDefaults` as the runtime config shape consumed by pricing, scheduling, and orchestration. Any mutable config-file or GMCM-facing type lives in the `Dayswork` mod project and maps into that snapshot.
- **Rationale**: This keeps SMAPI/GMCM concerns out of Core and preserves NFR-MAINT-03.

### TS-U17-02 — Use GMCM as an optional runtime integration
- **Decision**: Register config options only if `Helper.ModRegistry.GetApi(...)` returns the Generic Mod Config Menu API at `GameLaunched`. Add GMCM to `manifest.json` as an **optional** dependency, not a required one.
- **Rationale**: Matches FR-CFG-01, NFR-COMPAT-04, and the existing optional-dependency requirement in the Inception docs.

### TS-U17-03 — Route every GMCM label/tooltip through `I18nHelper`
- **Decision**: All GMCM section names, labels, tooltips, and any validation-facing user text come from `Dayswork/i18n/default.json` via `I18nHelper`.
- **Rationale**: Completes S-20 and preserves the single translation source of truth.

### TS-U17-04 — Add a test-only Roslyn/code-search lint dependency if needed
- **Decision**: Implement the hardcoded-string lint in `Dayswork.Tests/Lint/`, using a source-level static-analysis approach. If simple file scanning is too noisy, add `Microsoft.CodeAnalysis.CSharp` as a **test-project-only** package to parse syntax trees and inspect string literals precisely.
- **Rationale**: The lint gate must be deterministic and maintainable; test-only Roslyn keeps runtime dependencies unchanged while enabling a robust S-20 enforcement gate.

### TS-U17-05 — Keep PBT stack unchanged
- **Decision**: Continue using xUnit + FsCheck in `Dayswork.Tests`; no additional PBT framework is introduced for U-17.
- **Rationale**: Satisfies PBT-09 and aligns with Partial-mode enforcement already recorded in `aidlc-state.md`.

---

## Non-Decisions / Explicitly Unchanged

- No new runtime dependency beyond optional GMCM metadata/probing.
- No new Core project references or SMAPI/Stardew references in `Dayswork.Core`.
- No new Harmony patches are required for U-17.
- No change to the mod's target framework or SMAPI/Stardew compatibility baseline.
