# U-17 — Logical Components

**Unit**: U-17 — GMCM + i18n Polish

U-17 introduces one primary Mod component (`GMCMRegistrar`) and extends the composition/config/i18n/test seams around it. No Core gameplay component changes ownership in this stage; the design goal is to keep configuration editing in the Mod layer while preserving immutable runtime snapshots for the worker pipeline.

---

## Component Map

```text
ModEntry (existing, extended)
  |
  +- GameLaunched
      |
      +- GMCMRegistrar [NEW primary U-17 component]
      |    +- optional GMCM API probe
      |    +- field metadata table
      |    +- I18nHelper-backed labels/tooltips
      |    +- mutable config source read/write
      |    \- runtime snapshot mapper boundary
      |
      \- existing gameplay services remain unchanged
           +- HiringFlowCoordinator
           +- ShiftOrchestrator
           +- RecurringContractScheduler
           \- MailDispatcher

Dayswork.Core.Config (existing, reused unchanged)
  +- IConfigSnapshot
  +- ConfigSnapshot
  \- ConfigDefaults

Dayswork/i18n/default.json (extended)
  \- GMCM title / section / label / tooltip keys

Dayswork.Tests/Lint (new test area)
  \- hardcoded user-visible string lint gate
```

---

## LC-U17-01 — GMCMRegistrar

**Layer**: Mod / `Dayswork/Integration/`  
**Ownership**: Primary U-17 production component (`M-17` from unit-of-work docs)

**Responsibilities**:
1. Probe the optional GMCM API once at `GameLaunched`.
2. If present, register the Dayswork config screen.
3. Register each configurable field using centralized metadata rather than duplicated ad hoc callbacks.
4. Resolve all user-visible registration text through `I18nHelper`.
5. Write validated edits back into the mod's mutable config source.
6. Preserve the runtime rule that already-started work continues using the earlier snapshot.

**Not responsible for**:
- Recomputing active shifts.
- Owning pricing/scheduler/orchestrator logic.
- Replacing `IConfigSnapshot` as the worker-facing config contract.

---

## LC-U17-02 — Mutable Mod Config Source

**Layer**: Mod  
**Status**: Supporting seam, not a new Core concept

This is the editable configuration object loaded from `config.json` and surfaced through GMCM. It exists to absorb user edits and persistence concerns. Its design goal is compatibility with SMAPI/GMCM, not direct consumption by gameplay services.

**Responsibilities**:
- Hold editable values for rates, speed constant, hard cap, and stuck thresholds.
- Supply defaults equivalent to `ConfigDefaults`.
- Feed a validated mapping step into the immutable runtime snapshot shape.

---

## LC-U17-03 — Runtime Snapshot Mapper Boundary

**Layer**: Mod-to-Core seam

This mapping seam converts the mutable config source into `IConfigSnapshot` / `ConfigSnapshot`. It enforces PAT-U17-02 by making validation and clamping explicit before runtime logic consumes values.

**Responsibilities**:
- Build a complete immutable snapshot.
- Enforce field bounds/ranges.
- Preserve the existing snapshot-at-start semantics for shifts and recurring day starts.

---

## LC-U17-04 — I18n Catalog Extension

**Layer**: Content / `Dayswork/i18n/default.json`

U-17 extends the translation catalog with all GMCM-facing strings and any lint-test-documented allowlist notes that need to remain user visible elsewhere. This keeps the registrar and future config work fully localizable.

**Responsibilities**:
- Title/section keys for the config menu.
- Per-field label and tooltip keys.
- Any user-visible validation copy required by the config surface.

---

## LC-U17-05 — Hardcoded String Lint Test

**Layer**: Tests / `Dayswork.Tests/Lint/`

This component is the automated enforcement seam for S-20. It inspects the `Dayswork` source surface and flags likely user-visible literals that bypass `I18nHelper`.

**Responsibilities**:
- Scan the intended source scope deterministically.
- Exclude known non-user-facing literal classes through a documented allowlist.
- Produce clear failure output so contributors can fix or justify findings quickly.

**Not responsible for**:
- Runtime localization.
- Validating Core test data or technical constants unrelated to user-facing text.

---

## LC-U17-06 — ModEntry Extension

**Layer**: Mod / composition root

`ModEntry` remains the single wiring root. U-17 extends it only enough to:
- load/read the mod config source,
- construct `GMCMRegistrar`,
- call registration during `GameLaunched`,
- keep gameplay services consuming the validated runtime snapshot seam.

No new Harmony patching or per-tick event work is introduced here.

---

## Interaction Summary

```text
GameLaunched
  |
  +- ModEntry
      |
      +- probe GMCM API
      |    |
      |    +- unavailable -> no-op, mod continues normally
      |    \- available -> GMCMRegistrar.Register(...)
      |             |
      |             +- field metadata table
      |             +- I18nHelper labels/tooltips
      |             \- mutable config source setters
      |
      \- existing worker/hiring/scheduler services remain unchanged

Shift Start / Recurring Day Start
  |
  \- runtime snapshot mapper -> IConfigSnapshot consumed by pricing/orchestration
       (active-day values remain locked for in-flight work)

Test Run
  |
  \- Dayswork.Tests/Lint -> hardcoded-string gate over Dayswork source tree
```

---

## Extension Compliance Summary

| Rule | Status | Rationale |
|---|---|---|
| PBT-09 (blocking) | Compliant | FsCheck + xUnit remain the only PBT stack; no framework change |
| PBT-02 / PBT-03 / PBT-07 / PBT-08 | N/A at this stage | NFR Design defines integration/static-analysis seams, not new pure property obligations yet |
| Security Baseline | N/A | Extension disabled project-wide |
