# U-17 — NFR Requirements

**Unit**: U-17 — GMCM + i18n Polish

U-17 completes the player-tunable configuration surface (S-13) and turns the existing i18n convention into an enforced maintainability gate (S-20) without changing the worker's runtime business rules. The current codebase already has immutable Core config records, `I18nHelper`, and a single `i18n/default.json`; U-17 adds the optional GMCM bridge plus an automated lint test over the `Dayswork` mod project.

---

## Safety & Data Integrity

### SAFE-U17-01 — Active-day pricing/config semantics stay stable (NFR-SAFE-02, FR-PAY-08)
Config edits made through GMCM must not retroactively change a day that is already in progress. Deposit/rate/refund math for an active shift or already-started recurring day continues to use the config snapshot captured when that day began; edited values apply starting the next eligible morning or the next newly confirmed contract.

### SAFE-U17-02 — Optional-GMCM no-op path is first-class (NFR-SAFE-03, NFR-COMPAT-04)
The mod must remain fully playable when Generic Mod Config Menu is not installed. U-17's registrar is an optional integration: absent API, unexpected nulls, or version mismatches degrade to "GMCM not available" with no crash and no loss of existing `config.json` / default behavior.

### SAFE-U17-03 — Config persistence falls back safely to defaults (NFR-SAFE-03)
The runtime config surface must tolerate missing or malformed persisted values by rebuilding a valid configuration from defaults plus any valid overrides, rather than leaving partially invalid values in the live worker pipeline.

### SAFE-U17-04 — Lint gate must not create false-positive release blockers
The hardcoded-string lint test only targets **user-visible** strings in the `Dayswork` mod assembly/source surface. Technical identifiers, debug command names, manifest keys, asset names, and test-only literals must be explicitly scoped out so the gate protects translation quality instead of becoming noisy or unreliable.

---

## Performance

### PERF-U17-01 — GMCM registration is one-time only (NFR-PERF-01)
Registering the mod's config options occurs once during `GameLaunched`. No GMCM-specific work is allowed in the per-frame worker loop, menu draw loop, or pathing/tick pipeline.

### PERF-U17-02 — Lint cost is test-time only
The i18n lint pass runs only under `Dayswork.Tests`; it has no runtime cost in the shipped mod and no effect on SMAPI load time beyond build/test execution.

---

## Usability

### UX-U17-01 — Full config surface in GMCM (FR-CFG-01, S-13)
Every player-tunable field that exists in the runtime config model must be exposed in GMCM with a sensible control type, validation range, and clear description. This includes base rate, per-task increments, the average-speed constant, the hard cap time, and both stuck-detection thresholds.

### UX-U17-02 — All new config UI text is localizable (NFR-UX-02, FR-CFG-02, S-20)
Every new GMCM title, section name, option label, tooltip, and validation-facing user message must be routed through `I18nHelper` and stored in `Dayswork/i18n/default.json`. U-17 may add many keys, but it must not add any new user-visible English literals in code.

### UX-U17-03 — Rate-change timing is explained consistently (FR-PAY-08, S-13)
The GMCM option text and any related tooltip copy must make it clear that edited rates/constants affect future work starts, not deposits or refunds already locked for the current day.

---

## Reliability

### REL-U17-01 — Optional dependency probe is deterministic
GMCM registration uses a single runtime probe path (`Helper.ModRegistry.GetApi(...)`) and either fully registers the config screen or cleanly skips registration. Partial registration or half-initialized callbacks are not acceptable.

### REL-U17-02 — Lint rule is deterministic and reviewable
The hardcoded-string lint test must produce stable results for the same source tree and make failures easy to interpret, so contributors can fix them without guessing whether the rule is flaky.

---

## Maintainability

### MAINT-U17-01 — Core config remains immutable and SMAPI-free (NFR-MAINT-03)
`Dayswork.Core.Config` stays the runtime source of truth for worker logic. Any mutable config DTO, GMCM callback glue, or config-file read/write logic belongs in the `Dayswork` mod layer and maps into the immutable snapshot shape before reaching Core consumers.

### MAINT-U17-02 — One metadata source for config fields
U-17 should avoid scattering duplicated field names, defaults, bounds, and tooltip text across multiple classes. The GMCM bridge should derive as much as practical from the existing config shape/defaults so future tuning changes only need one code-path update.

### MAINT-U17-03 — i18n lint becomes the enforceable S-20 gate (NFR-MAINT-05)
The repo's previous convention of "use `I18nHelper` everywhere" becomes an automated regression guard. The test should live under `Dayswork.Tests/Lint/` and fail the build when newly introduced user-facing string literals bypass i18n.

### MAINT-U17-04 — Existing PBT stack remains authoritative (NFR-MAINT-02)
U-17 introduces no new PBT framework or alternate test runner. FsCheck + xUnit remains the enforced property-based testing stack for this project; the new lint test is example-based/static-analysis coverage, not a replacement for prior PBT obligations.

---

## Compatibility

### COMPAT-U17-01 — GMCM is optional in both manifest and runtime behavior (NFR-COMPAT-04)
`manifest.json` must advertise GMCM as an optional dependency, and the code must tolerate its absence. The shipped mod cannot require GMCM to load or to execute existing hires/shifts.

### COMPAT-U17-02 — Current platform targets unchanged (NFR-COMPAT-01)
U-17 must preserve the existing Stardew 1.6.x / SMAPI 4.x / .NET 6 compatibility surface. The GMCM integration and lint dependencies must fit that stack cleanly.

---

## Property-Based Testing Obligations (PBT Extension — Partial mode)

### PBT-U17-01 — Framework selection remains satisfied (PBT-09)
FsCheck + xUnit is already configured in `Dayswork.Tests` and remains the only PBT framework for the project. U-17 adds no new language surface and no competing framework decision.

### PBT-U17-02 — No new blocking PBT property is introduced by the NFR stage
U-17's main additions are SMAPI/GMCM integration and a static-analysis lint test. Those are primarily example-tested / integration-tested rather than new pure-property surfaces. If Code Generation introduces a new pure config-mapping helper with identifiable invariants, that coverage will be assessed in the code-generation plan.

---

## Security

Security Baseline extension is **disabled** project-wide (NFR-SEC-01). U-17 adds no network, auth, PII, or external service surface, so all Security Baseline rules remain **N/A**.
