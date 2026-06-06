# NFR Requirements Plan - U-MC-03 Manage Crops Authoring UI

**Unit**: U-MC-03 - Manage Crops Authoring UI
**Stage**: CONSTRUCTION - NFR Requirements
**Status**: Complete (no question round needed)

## Plan Checklist

- [x] Load NFR Requirements rule details.
- [x] Read U-MC-03 Functional Design artifacts (domain-entities, business-logic-model, business-rules, frontend-components).
- [x] Evaluate all NFR categories (scalability, performance, availability/reliability, security, maintainability, usability) against the unit.
- [x] Determine whether a question round is needed.
- [x] Generate nfr-requirements.md.
- [x] Generate tech-stack-decisions.md.
- [x] Present NFR Requirements completion gate.

## Question-Round Decision

**No question round needed.** The approved Functional Design already fixed every
NFR-relevant choice for this UI authoring unit:

- **Testing/determinism (Q3=A):** catalog season-filter/supply-tag/sort and multi-season
  resolution are pure-Core and PBT-covered; the live crop/shop adapter is example-tested.
- **Usability (Q1=A single scrolling page, Q2=B scrollable pickers, Q7=A reused chest idiom,
  R-25 gamepad parity):** UX shape is pinned and consistent with existing menus.
- **i18n (R-24):** all new strings are i18n-backed and lint-gated.
- **Scope boundaries (Q4=A, Q5=A, Q6=A):** no schema change, no new infrastructure, no new
  runtime dependency in this unit.

These, plus the feature-level NFR-MC-01..09 already approved in requirements, fix the NFR
posture. This mirrors how U-MC-01 and U-MC-02 NFR Requirements proceeded without a new
question round.

## Extension Notes

- **Security Baseline:** disabled for Manage Crops → N/A.
- **Property-Based Testing (full mode):** enabled; PBT-09 (framework) remains satisfied by the
  existing FsCheck.Xunit. Properties are carried into the pure catalog/resolver seams (Q3=A).
