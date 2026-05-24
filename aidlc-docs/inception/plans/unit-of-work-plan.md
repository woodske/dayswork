# Unit of Work Plan — Pricing Model Redesign

**Status**: Answers reviewed, no clarification round needed, and retrofit unit artifacts generated. Pending user review.

**Scope**: This plan decomposes the pricing overhaul into brownfield retrofit units. We are not replacing the historical greenfield unit history. We are defining the new units needed to rework pricing, worker energy, preview flow, recurring billing, and regression coverage on top of the already-built codebase.

**Still valid from earlier unit planning**:
- solo developer
- single deployable SMAPI mod
- `Dayswork`, `Dayswork.Core`, and `Dayswork.Tests` remain the codebase structure
- hybrid sequencing is still generally preferred unless the redesign answers change that

**Not re-asked here**:
- team-ownership boundaries, because this remains a solo-developer workflow
- greenfield directory-structure preferences, because this is a brownfield retrofit inside an existing solution
- deployable-service boundaries, because this remains one local mod, not a multi-service system

---

## Context Loaded
- [requirements.md](../requirements/requirements.md)
- [stories.md](../user-stories/stories.md)
- [application-design.md](../application-design/application-design.md)
- [components.md](../application-design/components.md)
- [component-methods.md](../application-design/component-methods.md)
- [services.md](../application-design/services.md)
- [component-dependency.md](../application-design/component-dependency.md)
- [execution-plan.md](execution-plan.md)
- existing unit artifacts in `aidlc-docs/inception/application-design/`

**Known high-impact historical units**:
- U-05 Pricing Core
- U-09 Minimum Hiring Flow
- U-10 Minimum Worker Shift
- U-12 Hiring UI Schedule
- U-15 Recurring Lifecycle + Calendar
- U-16 Animals & Buildings
- U-17 GMCM + i18n Polish

---

## Plan Checklist
- [x] Review refreshed requirements, stories, and application design for the pricing redesign
- [x] Identify which historical units are materially affected by the redesign
- [x] Prepare targeted unit-planning questions for the brownfield retrofit delta
- [x] Analyze your answers for ambiguity or contradictions and add follow-up questions if needed
- [x] Generate refreshed `aidlc-docs/inception/application-design/unit-of-work.md`
- [x] Generate refreshed `aidlc-docs/inception/application-design/unit-of-work-dependency.md`
- [x] Generate refreshed `aidlc-docs/inception/application-design/unit-of-work-story-map.md`
- [x] Validate retrofit unit boundaries and dependency order
- [x] Ensure every refreshed story is assigned to at least one retrofit unit
- [x] Ensure every refreshed pricing-redesign component is owned or explicitly extended by at least one retrofit unit

---

## Planning Questions

### Question U-R1 — How should the redesign units relate to the historical unit history?
The repo already has greenfield units U-01 through U-17 and associated Construction artifacts. We need to decide how the pricing-redesign units should coexist with that history.

A) **Append new retrofit units after the historical sequence (Recommended)** — preserve the old unit history as-is and create new redesign units that explicitly extend or replace parts of U-05/U-09/U-10/U-15/U-17
B) **Rewrite the old unit map in place** — update the existing unit IDs and artifacts as if the redesign had always been part of the original greenfield plan
C) **Hybrid** — keep the old units for audit history, but revise some of the historical unit definitions directly and add only a few new retrofit units
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

### Question U-R2 — What should be the primary slicing strategy for the retrofit units?
We have a few natural ways to group the redesign work.

A) **Architecture-first slices** — one unit for contract terms/pricing core, one for hiring preview/UI, one for runtime energy, one for recurring lifecycle, one for config/test/docs
B) **Player-journey slices** — one unit for “hire flow”, one for “day-of-work runtime”, one for “recurring life”, one for “admin/config cleanup”
C) **Hybrid (Recommended)** — start with foundational contract-terms and scope work, then vertical slices for hire preview, shift runtime, recurring lifecycle, and final config/regression cleanup
X) Other (please describe after [Answer]: tag below)

[Answer]: C

---

### Question U-R3 — How many retrofit units do you want for this redesign?
This controls checkpoint size and how much code churn each Construction loop will carry.

A) **4-5 coarse retrofit units** — fewer checkpoints, each unit rewrites a larger surface area
B) **6-8 medium retrofit units (Recommended)** — enough separation to isolate pricing core, UI preview, runtime energy, recurring lifecycle, and cleanup/regression without exploding overhead
C) **9+ fine-grained retrofit units** — tighter focus per loop, but more AI-DLC overhead and more review gates
X) Other (please describe after [Answer]: tag below)

[Answer]: B

---

### Question U-R4 — How should animal/greenhouse scope alignment be grouped?
Typed work scopes touch both pricing and runtime. We should decide whether that alignment lands early as part of the contract-terms foundation or later inside runtime-oriented units.

A) **Include animal/greenhouse scope modeling in the first foundation unit (Recommended)** — make typed scopes part of the earliest contract-terms/pricing unit so every later unit builds on the same scope model
B) **Split it** — outdoor scope modeling lands in the first unit, while animal/greenhouse alignment waits for later runtime-focused units
C) **Runtime-first** — keep the first unit focused on price structure only and defer full scope alignment until worker execution units
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

### Question U-R5 — Should regression/docs cleanup be a distinct final retrofit unit?
The redesign also requires build/test doc refresh, story-map cleanup, config/i18n polish, and regression coverage updates. We should choose whether that is its own unit or folded into feature units.

A) **Yes, keep a dedicated final cleanup/regression unit (Recommended)** — feature units focus on behavior changes, then a final unit consolidates config/i18n/test/doc updates and cross-cutting regression fixes
B) **No, fold cleanup into each feature unit** — each retrofit unit must leave its docs/tests/config surface fully updated before moving on
C) **Hybrid** — do most cleanup inline, but still keep a very small final verification/docs unit
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Artifact Goals After Approval

The answers are complete and the stage has refreshed these artifacts in `aidlc-docs/inception/application-design/`:

- [x] `unit-of-work.md` — retrofit unit list with purpose, owned/extended components, stories covered, and relationship to historical units
- [x] `unit-of-work-dependency.md` — dependency matrix and recommended execution order for the retrofit units
- [x] `unit-of-work-story-map.md` — mapping from refreshed stories to retrofit units and notes on which historical units are being superseded/extended
