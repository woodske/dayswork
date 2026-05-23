# U-17 — NFR Design Patterns

**Unit**: U-17 — GMCM + i18n Polish

U-17 adds no new runtime gameplay loop. Its NFR design is about exposing the existing config surface safely, preserving the project's Core/Mod separation, and turning the existing i18n convention into an enforceable regression gate. The worker, pricing, persistence, scheduler, and deposit behaviors remain owned by earlier units; U-17 wraps those seams with optional configuration UI and static validation.

---

## Retained unchanged

- **Immutable Core config contract** from `Dayswork.Core.Config` remains the runtime source of truth for pricing, scheduling, and orchestration.
- **Config snapshot lock at shift/day start** remains authoritative for active work (`FR-PAY-08`, `SAFE-U17-01`).
- **I18nHelper + `Dayswork/i18n/default.json`** remain the only approved source for user-visible text.
- **FsCheck + xUnit** remain the project's only PBT/test framework stack.
- **No new Harmony patches** are introduced.

---

## PAT-U17-01 — Optional Dependency Probe and No-Op Registration
**Satisfies**: `SAFE-U17-02`, `REL-U17-01`, `COMPAT-U17-01`

`GMCMRegistrar` is activated only after a single optional-dependency probe at `GameLaunched`. If the API is absent, incompatible, or unavailable, the mod cleanly skips GMCM registration and continues with normal `config.json` / default behavior. No partial UI registration is allowed.

**Consequence**:
- `manifest.json` advertises GMCM as optional metadata.
- `ModEntry` wires the registrar only through a single runtime probe path.
- There is no runtime dependency from gameplay code back onto GMCM.

---

## PAT-U17-02 — Mutable Mod Config to Immutable Runtime Snapshot
**Satisfies**: `SAFE-U17-01`, `SAFE-U17-03`, `MAINT-U17-01`

The Mod layer may use a mutable config DTO or callback-backed editor surface for GMCM and `config.json`, but all worker logic continues to consume the immutable `IConfigSnapshot` shape. A dedicated mapping step converts mutable values into a validated runtime snapshot before they reach pricing/scheduler/orchestration code.

**Consequence**:
- Invalid or partial user-edited values are clamped/rejected before snapshot creation.
- `Dayswork.Core` stays free of SMAPI/GMCM references.
- Runtime consumers keep their current constructor contracts.

---

## PAT-U17-03 — Single Metadata Table for GMCM Fields
**Satisfies**: `UX-U17-01`, `MAINT-U17-02`

Define the GMCM registration surface from a single metadata description per field: key, value selector, setter, bounds/validation, and i18n key roots. This avoids duplicating field names, defaults, and text wiring across multiple registration callsites.

**Consequence**:
- Adding a new config field later becomes one metadata-row change plus i18n keys.
- Validators and labels stay aligned.
- The registrar remains a thin adapter rather than hand-written one-off code for each option.

---

## PAT-U17-04 — I18n-First Registration Surface
**Satisfies**: `UX-U17-02`, `UX-U17-03`, `MAINT-U17-03`

Every GMCM title, section label, field label, tooltip, and validation-facing message resolves through `I18nHelper`. No English UI literals are embedded in the registrar. The same rule applies to lint-test allowlist documentation so S-20 stays mechanically enforceable.

**Consequence**:
- `Dayswork/i18n/default.json` remains the single translation source of truth.
- U-17 code generation must add explicit keys for every new GMCM string surface.
- The lint test can treat direct string literals in UI/config code as suspicious by default.

---

## PAT-U17-05 — Deterministic Source-Lint Gate with Explicit Allowlist
**Satisfies**: `SAFE-U17-04`, `REL-U17-02`, `MAINT-U17-03`

The hardcoded-string lint is implemented as a source-level static-analysis test with a narrow, explicit allowlist for non-user-facing literals: manifest keys, console/debug command names, asset identifiers, internal IDs, and test-only data. The test fails only on likely user-visible literals outside approved i18n access points.

**Consequence**:
- The rule is stable and reviewable.
- Contributors can see why a literal failed and either i18n-route it or explicitly justify its allowlist class.
- The gate protects the `Dayswork` mod assembly/source tree without polluting runtime code.

---

## PAT-U17-06 — One-Time Registration / Zero Tick Cost
**Satisfies**: `PERF-U17-01`, `PERF-U17-02`

All GMCM integration occurs once at `GameLaunched`. The lint gate runs only in `Dayswork.Tests`. No config-UI bookkeeping is permitted in the per-frame worker loop, menu draw path, or daily scheduler logic beyond reading the already-materialized runtime config.

**Consequence**:
- U-17 introduces no measurable frame or tick overhead.
- Runtime code remains unaffected when GMCM is absent.
- Performance risk is isolated to startup/test time.

---

## PAT-U17-07 — Current-Day Config Lock Preservation
**Satisfies**: `SAFE-U17-01`, `UX-U17-03`

Edits made through GMCM update the persisted/live mod config source for future work only. Existing active shifts, started recurring days, and already-computed deposits/refunds continue using the snapshot captured when that work began. U-17 should not add any dynamic reread path that mutates an in-flight shift's config.

**Consequence**:
- "Today's deposit uses R1, tomorrow's uses R2" remains true.
- The GMCM layer is descriptive/editorial, not authoritative for in-flight worker state.

---

## Resilience Assessment

| Scenario | Handling | Pattern |
|---|---|---|
| GMCM not installed | Skip registration, keep normal mod behavior | PAT-U17-01 |
| GMCM API missing/incompatible | Clean no-op, no partial registration | PAT-U17-01 |
| Invalid edited config value | Reject/clamp before runtime snapshot use | PAT-U17-02 |
| Drift between labels/tooltips/validators | Central metadata table keeps definitions aligned | PAT-U17-03 |
| New user-facing literal added in code | Lint gate fails build/test until i18n-routed | PAT-U17-04 / PAT-U17-05 |
| False-positive lint noise | Explicit allowlist for non-user-facing literals | PAT-U17-05 |
| Frame/tick overhead from GMCM | Prohibited; registration is one-time only | PAT-U17-06 |
| Mid-day config edit affecting active worker | Prevented by snapshot lock semantics | PAT-U17-07 |

## Scalability Assessment
N/A — single-player SMAPI mod; GMCM registration and linting are small local concerns, not distributed scaling problems.

## Security Assessment
N/A — Security Baseline extension remains disabled and U-17 adds no network, auth, or external service surface.
