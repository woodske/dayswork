# NFR Requirements — U-SVE-01 Expansion-Compatibility Provider Foundation

Quality bar for the provider foundation. Each item maps to the approved change-level NFRs (`NFR-SVE-*`). Answers: Q1=A (fail-safe to Vanilla), Q2=A (cached/once performance envelope), Q3=A (reuse existing stack).

## Reliability / Resilience

- **NFRU-01 (→ NFR-SVE-01/04) Fail-safe detection.** Expansion detection, profile selection, and seam construction are guarded. On any unexpected error (registry anomaly, construction failure), the mod logs a warning and runs with the `VanillaExpansionProfile`. Compatibility logic must never crash or disable the mod.
- **NFRU-02 (→ NFR-SVE-04) Total seam operations.** Every seam operation is total: pure lookups never throw; on the Vanilla profile they return "no override." Bad/edge inputs (e.g., negative capacity inputs) clamp rather than throw.

## Performance

- **NFRU-03 (→ NFR-SVE-06) One-time detection.** Detection runs exactly once at `GameLaunched`; the active profile is cached for the session. No per-save, per-tick, or per-frame mod-registry queries.
- **NFRU-04 (→ NFR-SVE-06) Constant-time lookups.** Seam lookups used in runtime hot paths (entrance override, capacity, classification override, work-location membership) are O(1)/constant-time with no reflection per tile. No measurable per-shift regression versus the Worker Routing baseline.

## Determinism & Correctness

- **NFRU-05 (→ NFR-SVE-03) Deterministic pure logic.** Profile selection and capacity derivation are pure, deterministic functions of their inputs (same input → same output), enabling FsCheck coverage.
- **NFRU-06 (→ NFR-SVE-03) Grounded values.** SVE-specific values are verified from source before use; in this unit only verified mod IDs and farm-map IDs are encoded (override tables remain empty).

## Isolation & Maintainability

- **NFRU-07 (→ NFR-SVE-01/02) Seam is the only SVE-awareness point.** No vanilla/Core call site contains SVE branches; consumers depend only on `ExpansionCompatService`. With no expansion, behavior is identical to the prior release.
- **NFRU-08 (→ NFR-SVE-07) Centralized identifiers.** All SVE identifiers live in `SveExpansionProfile`; no scattered magic strings.
- **NFRU-09 (→ NFR-SVE-02) Extensible.** A new expansion is added by implementing a new `IExpansionProfile` + registering it with the selector; no edits to vanilla/Core call sites.

## Testability

- **NFRU-10 (→ NFR-SVE-05) Pure logic unit/PBT-tested without SMAPI.** `ExpansionProfileSelector`, `AnimalBuildingCapacityPolicy`, and profile lookups are tested in `Dayswork.Tests` with xUnit + FsCheck (full PBT), with no Stardew/SMAPI assemblies required. Properties: selection determinism/precedence, Vanilla default, Vanilla no-op, capacity clamp.

## Security

- **N/A** — no network, PII, auth, or secret-handling surface (Security Baseline disabled for this change).

## Extension Compliance

| Extension | Status | NFR-requirements compliance |
|---|---|---|
| Security Baseline | Disabled | N/A — no security surface. |
| Property-Based Testing | Enabled, full | Compliant — NFRU-05/10 set the FsCheck obligations carried into NFR Design and Code Generation. |
