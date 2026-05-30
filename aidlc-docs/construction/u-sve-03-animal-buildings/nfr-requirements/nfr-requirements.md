# NFR Requirements — U-SVE-03 SVE Animal Buildings

Quality bar for premium-building feed-capacity and tier resolution, mapped to the change-level `NFR-SVE-*`. Answers: Q1=A (on-demand trough scan, no caching), Q2=A (fail-safe to vanilla, never throw), Q3=A (reuse existing stack).

## Performance
- **NFRU3-01 (→ NFR-SVE-02) On-demand capacity, no caching.** Trough-tile counting runs only when feed work is created (a handful of times per shift), over a small building interior. Auto-feed buildings (all SVE premium + vanilla Deluxe) short-circuit on the auto-feed gate **before** any scan. No per-tile/per-frame cost; no cache/state introduced.
- **NFRU3-02 (→ NFR-SVE-02) O(1) tier lookup.** `MapPremiumBuildingTier` is a constant-time dictionary lookup keyed on `buildingType`; it adds nothing measurable to hiring enumeration.

## Reliability / Resilience
- **NFRU3-03 (→ NFR-SVE-04) Never throws into the shift.** Capacity derivation is total (negative/zero-safe clamp in `AnimalBuildingCapacityPolicy`); tier resolution returns the supplied vanilla tier when no premium mapping applies. Missing/odd building data degrades to a safe value, never an exception in the runtime path.

## Determinism & Correctness
- **NFRU3-04 (→ NFR-SVE-03) Deterministic pure logic.** `DeriveCapacity` and `MapPremiumBuildingTier` are pure, deterministic functions of their inputs.
- **NFRU3-05 (→ NFR-SVE-03) Grounded data.** The two premium `buildingType` strings, `MaxOccupants = 16`, the `AutoFeed = T` map property, and the Premium→Deluxe nearest-tier choice are all verified from SVE source; nothing assumed.

## Isolation / Vanilla invariance
- **NFRU3-06 (→ NFR-SVE-01) Vanilla unchanged.** With the null-object profile, `MapPremiumBuildingTier` returns `null` for every building (tier inference unchanged) and data-driven capacity equals the legacy ladder for vanilla buildings (parity asserted at code-gen). Byte-for-byte vanilla behavior.
- **NFRU3-07 (→ NFR-SVE-07) SVE ids isolated.** Premium identifiers live only in `SveExpansionProfile`; no SVE literals leak into `AnimalTaskHandler` or `LegacyScopeBootstrapper`.

## Testability
- **NFRU3-08 (→ NFR-SVE-05) Pure logic PBT-tested.** Capacity clamp/monotonicity/vanilla-parity and tier-map totality/pass-through are covered by xUnit + FsCheck without SMAPI. The only SMAPI-touching parts (trough scan over a live map, live building enumeration) are validated by manual SVE playtest.

## Security
- **N/A** — no network, PII, auth, or external-input surface.

## Extension Compliance

| Extension | Status | NFR-requirements compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. |
| Property-Based Testing | Enabled, full | Compliant — NFRU3-04/05/08 set the FsCheck obligations (capacity invariants, tier-map totality, vanilla pass-through) carried into NFR Design and Code Generation. |
