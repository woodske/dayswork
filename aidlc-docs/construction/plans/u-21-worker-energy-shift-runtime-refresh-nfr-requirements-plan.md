# U-21 — Worker Energy + Shift Runtime Refresh: NFR Requirements Plan

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh  
**Phase**: CONSTRUCTION — NFR Requirements  
**Builds on**: approved Functional Design for `U-21`. See [functional-design/](../u-21-worker-energy-shift-runtime-refresh/functional-design/).

---

## Plan Checklist

- [x] Analyze functional design for applicable NFRs
- [x] Create this NFR requirements plan
- [x] Collect answers to NFR-Q1 through NFR-Q5
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `nfr-requirements.md`
- [x] Generate `tech-stack-decisions.md`
- [x] Present completion message and await approval

---

## Context Summary

U-21 is a **runtime retrofit unit**. Unlike U-18 and U-19, its main risk is not pricing math or persistence shape; it is the quality of the live worker day after the pricing redesign becomes real in-world behavior.

Its NFR surface is therefore mostly about:
- keeping the per-tick shift loop cheap enough that the new stamina model and slower pacing do not cause visible frame drops
- making the overhead stamina bar and labor pacing feel immediate, readable, and deterministic rather than jittery
- preserving output-settlement safety across every stop reason: normal completion, zero stamina, 8pm, sleep-stop, and stuck abort
- keeping the new stamina/work-boundary decisions in pure Core seams so the runtime remains testable instead of collapsing into hard-to-verify orchestrator branches
- choosing an appropriate regression bar for a stateful unit that now owns energy depletion and stop-boundary semantics

**Inherited NFRs that already apply and do not need re-decision here**:
- `NFR-MAINT-02` Property-Based Testing extension remains enabled in partial mode with FsCheck
- `NFR-MAINT-03` pure business logic should stay separated from SMAPI/runtime dependencies where practical
- `NFR-SAFE-01` no collected items are lost
- `NFR-PERF-01` slower worker pacing must come from intentional gameplay timing, not lag
- `NFR-SEC-01` Security Baseline extension is disabled project-wide
- U-22 still owns the broader typed-scope runtime alignment work

**Important U-21-specific quality concerns**:
- The worker now spends stamina per labor beat, so the live update loop must remain lightweight while still updating the visible bar promptly.
- The empty-stamina experience must feel understandable: finish the current unit, then switch into deposit-and-exit with no strange extra work.
- Runtime stop behavior now has more paths that must preserve the same output-safety guarantees: zero stamina, 8pm, sleep, and stuck abort.
- This unit introduces the strongest new stateful Core seams since the earlier worker/runtime units, so test rigor and determinism matter more than usual.

**Pre-decided tech stack / no question needed**:
- no new runtime framework, job system, or async pipeline is being introduced
- SMAPI event-driven orchestration remains the runtime shell
- test stack stays `xUnit` + `FsCheck.Xunit`
- visible stamina remains an in-world overhead element rather than a separate HUD framework

---

## NFR Questions

> Option **A** is the recommendation. A letter is enough; add a sentence if you want to steer the detail.

### NFR-Q1 — Runtime performance target

U-21 adds per-beat stamina accounting, more deliberate pacing, and a live overhead bar. We should lock the expected runtime cost now.

**A) Keep the shift loop comfortably lightweight (Recommended).** The worker's per-frame update should stay well below visible frame-drop territory on typical hardware, and the new slower feel must come from explicit timing rather than expensive update work.

**B) Small extra runtime cost is acceptable.** A modest per-frame cost is fine if it simplifies the stamina/pacing implementation.

**C) Heavier runtime work is acceptable.** Correctness/readability matters more than per-frame cost for this retrofit.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q2 — Responsiveness target for stamina presentation and pacing

The worker now has a visible overhead stamina bar and slower action cadence. We should decide how immediate that feedback needs to feel.

**A) Immediate and deterministic visual feedback (Recommended).** The overhead bar should update on the same logical labor beat that spends stamina, and pacing should feel consistently readable rather than jittery or delayed.

**B) Small visual delay is acceptable.** Minor coalescing or a short delay in bar updates/action pacing is fine if it simplifies the implementation.

**C) Eventual visual consistency is enough.** As long as the worker generally looks right over time, exact beat-by-beat synchronization is not important.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q3 — Reliability target for stop-and-settle behavior

U-21 now has several stop reasons that all lead into deposit/output settlement. This question sets the quality bar for those paths.

**A) Full stop-path consistency is mandatory (Recommended).** Normal completion, zero stamina, 8pm, sleep-stop, and stuck abort should all preserve the same no-item-loss guarantees and predictable wrap-up behavior.

**B) Natural stop paths are the priority.** Normal completion and zero-stamina behavior must be solid, but edge paths like stuck abort or sleep-stop can be slightly rougher if needed.

**C) Minimal stop-path uniformity is acceptable.** As long as the mod avoids crashes, some edge stop behaviors may stay inconsistent during the retrofit.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q4 — Determinism strictness for pure runtime decisions

The new energy ledger and work-boundary logic create pure decision seams that later tests can exercise heavily. We should decide how strict determinism needs to be.

**A) Strict deterministic pure-runtime output (Recommended).** Equivalent inputs to the pure energy / work-boundary / stop-decision seams should produce the same remaining-energy results, stop reasons, and transition outcomes across runs.

**B) Behavioral determinism only.** The worker should generally stop and settle correctly, but exact transition/result structure may vary internally.

**C) Low determinism requirement.** As long as player-visible behavior is roughly correct, internal pure-seam outputs do not need to stay strictly stable.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q5 — Test-rigor expectation for the runtime retrofit

Because U-21 introduces new stateful Core seams, we should decide how strong the regression bar needs to be.

**A) Strong example + property coverage (Recommended).** U-21 should get focused example tests plus meaningful FsCheck coverage for stamina invariants, work-unit boundaries, stop-reason transitions, and no-refund wrap-up semantics where property testing is practical.

**B) Example tests first, lighter property coverage.** Keep only the minimum properties needed for extension compliance and lean mainly on conventional unit tests.

**C) Minimal direct coverage.** Rely mostly on later integration/playtest validation for this unit.

**X) Other (please describe after the tag).**

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/nfr-requirements/nfr-requirements.md`
- `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/nfr-requirements/tech-stack-decisions.md`
