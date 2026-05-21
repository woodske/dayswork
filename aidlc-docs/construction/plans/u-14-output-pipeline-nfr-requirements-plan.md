# U-14 — Output Pipeline: NFR Requirements Plan

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail
**Phase**: CONSTRUCTION — NFR Requirements

---

## Plan Checklist

- [x] Analyze functional design artifacts (business-logic-model, domain-entities, business-rules)
- [x] Pull applicable NFRs from requirements.md per unit scope
- [x] Determine PBT obligations (PBT extension — Partial mode)
- [x] Record tech-stack decisions
- [x] Generate `nfr-requirements.md`
- [x] Generate `tech-stack-decisions.md`
- [ ] Present completion message and await approval

---

## Assessment — no blocking user questions

Consistent with how U-07, U-10, and U-13 NFR Requirements were handled, all U-14 NFRs are determinable from the approved Functional Design + prior project decisions:

- **Safety** is the heart of U-14 and is already fixed by the FD: conservation (NFR-SAFE-01) follows directly from BR-SAFE-01 / BR-OUT-09 / BR-INT-01; integer refund (NFR-SAFE-02) is unchanged from prior units; save-integrity (NFR-SAFE-03) is *simplified* by FD-Q4=A (no custom Dayswork mail save data — mail rides the platform/MFM "deliver tomorrow" queue).
- **Performance** budgets are inherited from U-10/U-13: planning runs **once** at shift end (NFR-PERF-02), and chest resolution is one lookup per chest trip on arrival, never per frame (NFR-PERF-01). No new per-frame work.
- **i18n** (NFR-UX-02) applies — U-14 *does* add new user-visible strings (mail bodies + sender label) — but the requirement itself is unambiguous; the keys are listed in unit-of-work.md.
- **Tech stack adds no new frameworks**: the planner is plain Core C#; testing stays xUnit + FsCheck. The only external integration is MFM, already decided in V9 (FD context) — required dependency.
- The one open *engineering* question — how MFM behaves with a large multi-item attachment, and the exact `GetApi` acquisition/error handling — is a **pattern decision for NFR Design / Code Generation**, not a product preference, so it is recorded as a deferred tech decision rather than a user question.

No ambiguity requires user input before producing the NFR artifacts.

---

## Applicable NFRs (detail in nfr-requirements.md)
- **Safety**: NFR-SAFE-01 (conservation — primary), NFR-SAFE-02 (integer refund — inherited), NFR-SAFE-03 (no custom mail save data; tolerate MFM-managed persistence).
- **Performance**: NFR-PERF-01 (no per-frame planning), NFR-PERF-02 (plan once per shift).
- **Usability**: NFR-UX-02 (mail strings + sender label via i18n).
- **Reliability**: graceful chest-missing/full fallbacks, one-letter overflow guarantee, large-attachment consideration, MFM acquisition failure handling.
- **Maintainability**: NFR-MAINT-03 (DepositPlanner pure Core), NFR-MAINT-04 (no new Harmony patches), NFR-MAINT-05 (.NET conventions).
- **Compatibility**: NFR-COMPAT-04 (MFM required dependency in manifest).
- **PBT (Partial mode, blocking)**: PBT-03 (planner conservation + trip-count + no-empty-trip), PBT-07 (shared generator for planner inputs), PBT-08 (seed logging). PBT-02 **N/A** (FD-Q4=A introduces no new round-trip serialization).

---

## Artifact output
- `aidlc-docs/construction/u-14-output-pipeline/nfr-requirements/nfr-requirements.md`
- `aidlc-docs/construction/u-14-output-pipeline/nfr-requirements/tech-stack-decisions.md`
