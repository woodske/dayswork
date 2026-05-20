# U-13 — Worker Features: NFR Requirements Plan

**Unit**: U-13 — Worker Features: Priority + Stuck + Tool Swap + Invulnerability
**Phase**: CONSTRUCTION — NFR Requirements

---

## Plan Checklist

- [x] Analyze functional design artifacts
- [x] Pull applicable NFRs from requirements.md per unit scope
- [x] Determine PBT obligations (PBT extension — Partial mode)
- [x] Record tech-stack decisions
- [x] Generate `nfr-requirements.md`
- [x] Generate `tech-stack-decisions.md`
- [ ] Present completion message and await approval

---

## Assessment — no blocking user questions

All U-13 NFRs are determinable from the approved Functional Design + prior project decisions (consistent with how U-07 and U-10 NFR Requirements were handled):
- Performance budget, tick-throttle, and once-per-shift scan are inherited from U-10 and unchanged in principle.
- Safety/reliability follow directly from the stuck escalation + Farmer-not-serialized rules already fixed in business-rules.md.
- Tech stack adds **no new frameworks** — the Farmer rendering/movement uses existing Stardew APIs; testing stays on xUnit + FsCheck.
- The one genuinely open *engineering* choice — how to integrate the standalone Farmer's draw/update (manual render hook vs. registering it in `location.characters`) — is a **pattern decision for NFR Design**, not a product preference, so it is recorded as a deferred tech decision rather than a user question.

---

## Applicable NFRs (detail in nfr-requirements.md)
- **Performance**: NFR-PERF-01 (per-frame budget), NFR-PERF-02 (scan once per shift).
- **Safety**: NFR-SAFE-01 (no items lost on early-end), NFR-SAFE-02 (integer refund), NFR-SAFE-03 (no save corruption — Farmer never serialized), NFR-SAFE-04 (only collects self-caused drops).
- **Reliability**: clean stuck-escalation termination, teleport reachability validation, skip-and-continue, classifier never throws.
- **Maintainability**: NFR-MAINT-03 (StuckDetector + state machine pure Core), NFR-MAINT-02 (FsCheck), NFR-UX-02 (i18n — no new strings this unit).
- **PBT (Partial mode, blocking)**: PBT-03 (state-machine + StuckDetector invariants), PBT-08 (seed logging). PBT-02/07 N/A for this unit (no new round-trip serialization or shared generator obligations beyond U-10's).
