# U-16 — Animals & Buildings: NFR Requirements Plan

**Unit**: U-16 — Animals & Buildings
**Phase**: CONSTRUCTION — NFR Requirements
**Builds on**: approved Functional Design (FD-Q1=A…Q9=A; DEV-U16-01..04). See [functional-design/](../u-16-animals-buildings/functional-design/).

---

## Plan Checklist

- [x] NFR-Q1–Q3: Collect answers (Q1=A full vanilla animal-care gains, Q2=A lazy interior scan at entry, Q3=A reuse stuck detection)
- [x] Resolve any ambiguities — user clarification (does scanning once miss a moving animal?) answered: scan fixes the animal *set by identity*, positions/eligibility resolved live; encoded as REL-U16-02/03 and clarified in FD business-logic-model Flow 4 + domain-entities AnimalWorkItem
- [x] Generate `nfr-requirements.md`
- [x] Generate `tech-stack-decisions.md`
- [ ] Present completion message and await approval

---

## Context Summary

U-16's NFRs are **mostly inherited** from the worker/output/lifecycle units (U-10 shift, U-13 worker AI, U-14 output pipeline, U-15 recurring lifecycle). The unit adds quality requirements for the new surfaces: **cross-location traversal (door-warp)**, **animal task execution**, and **indoor scanning**. No new external dependency, no new save data, and no new state-machine phase are introduced (these constrain the NFR set).

**Inherited NFRs that must continue to hold** (no re-decision needed):
- **NFR-SAFE-01** no items lost — extends to building-skip, mid-building 8pm cap, multi-location deposit, and sleep-stop inside a building.
- **NFR-SAFE-02** integer-clamped refund; deposit-run (incl. deposit-time warps) unbilled.
- **NFR-SAFE-03** tolerate absent data — extends to missing silo/hopper/interior/animals/demolished building; no new persisted Dayswork data.
- **NFR-SAFE-04** only collect drops the worker caused — extends to "only animal-caused products/forage, never arbitrary placed/dropped items."
- **NFR-PERF-01/02** no per-frame cost spikes; scan once per location per shift.
- **NFR-MAINT-03** pure logic in Core, SMAPI/Stardew behind seams; **NFR-MAINT-04** no new Harmony patches.
- **NFR-UX-02 / S-20** new user-visible strings via `I18nHelper`.
- **NFR-COMPAT-02** all 7 vanilla farm types; relies on existing building types.

**New NFR concerns U-16 introduces** (assessed below; some need your input):
- **Worker is never serialized into a building.** The worker NPC must be removed from **every** location's character list (not just the farm) on shift end / clear / sleep-stop, so a save while it is inside a barn cannot persist a stray worker (NFR-SAFE-03). *(Stated as a requirement; not a question.)*
- **Warp cost is bounded.** One enter + one exit per building during work, plus one enter/exit per building-interior chest at deposit time; warps are not per-frame (NFR-PERF-01). *(Requirement; not a question.)*
- **Animal scan is bounded** by animal count, run once per location per shift (NFR-PERF-02). *(Requirement; scan *timing* is NFR-Q2.)*
- **Billing is wall-clock.** Work-phase travel (incl. in/out-of-building warps during work) is on the shift clock like outdoor walking; deposit-run warps occur after `ShiftEndTime` and are unbilled — consistent with FR-PAY-05. *(Requirement; not a question.)*

**Already decided / NOT in scope for questions:**
- Cross-location model, ordering, deposit model, scanner reuse, failure handling, pricing — all fixed in the approved FD.
- Security Baseline disabled project-wide (NFR-SEC-01) — N/A.
- No new NuGet/manifest dependency; MFM already required (U-14); GMCM optional (U-17).

---

## NFR Questions

> Option **A** is the recommendation. A letter is enough; add a sentence to steer detail.

### NFR-Q1 — Animal-care gameplay gains (does outsourcing care "count"?)

When the worker pets/feeds/collects, vanilla animal logic normally grants **friendship + mood**, which over time affect **product quality** (and mood affects whether a product is produced at all). The worker calling the same vanilla interactions would grant those gains; we can also mute them.

**A) Full vanilla gains (Recommended).** Worker feed/pet/collect grant the same friendship/mood as if the player did it; product quality improves over time exactly as normal. This is the point of hiring help — the player can keep animals happy without doing it by hand.

**B) Chore-only, no friendship/mood gain.** The worker marks the animal fed/petted (so it won't be re-done and won't *decay* from neglect that day) but grants **no** friendship/mood increase — building animal friendship stays the player's personal job. Products still collected, but quality progression is the player's responsibility.

**C) Other (describe after the tag).**

[Answer]: A

---

### NFR-Q2 — When to scan building interiors (performance vs. freshness)

The outdoor farm is scanned once at 6am today. For buildings, scanning can happen at the same 6am moment (pre-scan everything) or lazily when the worker actually enters each building. A shift can run for hours, so indoor state may change between 6am and arrival (e.g., the player harvests greenhouse crops first; an animal produces later).

**A) Lazy scan at batch entry (Recommended).** Each building's interior is scanned once, the moment the worker enters that batch. Still one scan per location per shift (NFR-PERF-02), but it reflects state at arrival and naturally handles a building that became invalid/demolished before the worker got there.

**B) Pre-scan all locations at 6am.** One uniform scan moment for the whole shift. Simpler timing, but a long shift may act on stale indoor state (or try to enter a building that changed after 6am).

**C) Other (describe after the tag).**

[Answer]: A

---

### NFR-Q3 — Moving / unreachable outdoor grazing animals (FD-Q5=B reliability)

FD-Q5=B has the worker handle animals **wherever they are**, including grazing outdoors. Outdoor animals wander, so the worker's approach tile can go stale and it could waste time chasing one animal.

**A) Reuse the existing stuck detection (Recommended).** If the worker can't reach a target animal within the normal stuck window, it gives up on that animal (skips it) and moves on — the same escalation that already protects outdoor tile work. Bounded, no new machinery, animal handled next morning.

**B) Per-animal attempt cap.** A simpler bounded retry just for animals (e.g., re-target a moving animal a fixed number of times, then skip), independent of the stuck system.

**C) No chase.** If a targeted animal is not at its scanned approach tile when the worker arrives, skip it immediately. Cheapest; may skip more animals on busy farms.

**D) Other (describe after the tag).**

[Answer]: A

---

## Artifact output (generated after answers are collected)

- `aidlc-docs/construction/u-16-animals-buildings/nfr-requirements/nfr-requirements.md`
- `aidlc-docs/construction/u-16-animals-buildings/nfr-requirements/tech-stack-decisions.md`
