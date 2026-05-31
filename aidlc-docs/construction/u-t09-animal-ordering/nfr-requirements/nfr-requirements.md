# NFR Requirements — u-t09-animal-ordering

Scope: a deterministic re-ordering of existing animal work inside the shift runtime. No new external surface (no network/PII/auth → Security Baseline disabled, Q6=B). Property-Based Testing enabled, full mode (Q7=A).

## Requirements

- **NFRU-T09-01 — Determinism.** For a given `WorkScopeSet` + enabled-task set, `ShiftPlanBuilder.BuildBatchPlan` produces a fully deterministic batch sequence (building order, per-building pairing, single trailing forage). Required for example + property tests. *(NFR-T09-01)*
- **NFRU-T09-02 — Performance / no added scans.** The change adds **no** extra whole-farm scans versus today. The per-building grazing passes partition the same grazing-animal set (filtered by home key) instead of one combined pass; the single `FarmForage` pass performs the same whole-farm forage scan that the combined `OutdoorAnimals` batch did. No per-tick or per-shift regression. *(NFR-T09-02)*
- **NFRU-T09-03 — Reliability / no dropped work.** Re-ordering must not drop or double-apply animal work. The "every selected animal serviced exactly once" guarantee is preserved (idempotent pet/collect covers the legacy shared-key edge). *(FR-T09-04 / BR-T09-05)*
- **NFRU-T09-04 — Testability (PBT full mode).** Pure batch-plan ordering logic must be unit- and FsCheck-testable with the invariants P-T09-1..6. Runtime per-building scoping and forage isolation covered by targeted example tests where feasible.
- **NFRU-T09-05 — Backward compatibility.** No save-schema, config, GMCM, or i18n change. Existing and legacy contracts produce the new ordering with no migration. *(NFR-T09-04)*
- **NFRU-T09-06 — Maintainability.** Keep the ordering decision in the pure Core `ShiftPlanBuilder`; keep the runtime (`ShiftOrchestrator`) a thin adapter that fills skeletons and routes. No new subsystem.

## Out of scope (NFR)
- Scalability/availability/DR — N/A (single-player in-process mod logic).
- Security — N/A (no new surface; Security Baseline disabled for this change).
- Proximity-optimized building ordering — out of scope (FR-T09-07).
