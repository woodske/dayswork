# NFR Requirements Plan — U-17 GMCM + i18n Polish

## Depth
Minimal — U-17 is an integration/polish unit with no new business-logic model.
Functional Design is skipped; all NFRs are fully determined from approved requirements, unit-of-work docs, and the current codebase.
No clarifying questions needed.

## Stages

- [x] Step 1: Analyze unit context (unit-of-work.md, unit-of-work-story-map.md, requirements.md, components.md, current config/i18n code)
- [x] Step 2: Identify applicable NFRs for U-17 scope
  - NFR-COMPAT-04: GMCM remains an optional dependency probed at runtime and declared in `manifest.json` as optional metadata
  - NFR-SAFE-02 / FR-PAY-08 / FR-WORK-13 / FR-CFG-01: config edits must preserve "today uses R1, tomorrow uses R2" semantics for active recurring contracts and stuck-threshold tuning
  - NFR-SAFE-03: absent GMCM or malformed config data must degrade to defaults / no-op rather than breaking the mod
  - NFR-PERF-01: GMCM registration is a one-time `GameLaunched` cost with no per-frame overhead
  - NFR-UX-02 / FR-CFG-02: every new GMCM title/label/tooltip and validation message must route through `I18nHelper` / `i18n/default.json`
  - NFR-MAINT-03: immutable Core config snapshot remains the source of truth for runtime logic; Mod-layer adapters own SMAPI/GMCM integration
  - NFR-MAINT-05 / S-20: add an automated lint gate in `Dayswork.Tests` that flags user-visible hardcoded strings outside approved i18n callsites
  - PBT-09: existing FsCheck/xUnit framework remains the enforced PBT stack; U-17 adds no alternate framework
- [x] Step 3: Confirm no questions needed (scope is pinned by approved requirements and current implementation state)
- [x] Step 4: Generate `nfr-requirements.md`
- [x] Step 5: Generate `tech-stack-decisions.md`
