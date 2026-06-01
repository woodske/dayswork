# NFR Requirements Plan - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Change**: TODO-10 SVE Grandpa's Shed greenhouse routing
**Stage**: Construction / NFR Requirements
**Status**: Complete - review required

## Context

Functional Design is approved for `u-t10-shed-greenhouse-routing`. The design introduces explicit SVE route-hop data, route-shape discovery, shift-time route validation, a narrow `CrossLocationRouteNavigator`, selected shed-greenhouse crop work, shed/main-shed deposit eligibility, `ShiftOrchestrator`-owned failure policy, and item-safe route/deposit failure behavior.

NFR Requirements will define the quality bar for this runtime path before NFR Design and Code Generation.

## NFR Requirements Execution Plan

- [x] Record Functional Design approval from user response `continue`.
- [x] Load NFR Requirements rule details from `.aidlc-rule-details/construction/nfr-requirements.md`.
- [x] Load common content-validation and question-format rules.
- [x] Load TODO-10 Functional Design artifacts.
- [x] Load enabled extension configuration and Property-Based Testing rules.
- [x] Analyze NFR categories: scalability, performance, availability, security, tech stack, reliability, maintainability, and usability.
- [x] Validate this plan content before creation: markdown only, no Mermaid diagrams, no ASCII diagrams, and all questions use `[Answer]:` tags with `X) Other` as the last option.
- [x] Create this NFR Requirements plan and question gate.
- [x] Update `aidlc-docs/aidlc-state.md` and `aidlc-docs/audit.md` for the NFR Requirements question gate.
- [x] Collect answers from this file.
- [x] Validate every answer for completeness, invalid choices, ambiguity, and contradiction.
- [x] Create a clarification question file if any answer is ambiguous or contradictory.
- [x] Generate `aidlc-docs/construction/u-t10-shed-greenhouse-routing/nfr-requirements/nfr-requirements.md`.
- [x] Generate `aidlc-docs/construction/u-t10-shed-greenhouse-routing/nfr-requirements/tech-stack-decisions.md`.
- [x] Update `aidlc-docs/aidlc-state.md` and `aidlc-docs/audit.md` for NFR Requirements completion.
- [x] Present the standardized NFR Requirements completion message and wait for review approval.

## NFR Category Assessment

| Category | Initial assessment |
|---|---|
| Scalability | Low data volume; route tables are small and explicit. Quality bar should prevent broad graph scans or hot-loop recomputation. |
| Performance | Runtime route validation must stay lightweight during shifts and deposit trips; discovery occurs only when menus open. |
| Availability | Live SVE route state can change by save/map/quest/load state; validation should be fresh and fail-safe. |
| Security | Security Baseline is disabled for TODO-10; no network, auth, secrets, or PII surface. |
| Tech stack | Existing C#/.NET, SMAPI, xUnit, and FsCheck stack appears sufficient. |
| Reliability | Validation and route failure must be total, non-throwing, narrow, and item-safe. |
| Maintainability | SVE-specific strings/data stay centralized; future expansions should use profile/route data rather than runtime branches. |
| Usability | Existing menus remain; no player-facing route-error UI is introduced. Manual playtest still matters because SVE map state is live. |

## Questions

Please answer each question by filling in the letter choice after the `[Answer]:` tag. If none of the options match, choose `X) Other` and describe the preferred behavior after the tag.

### Question 1
What runtime performance envelope should route lookup and validation target?

A) Bounded synchronous validation: route lookup is table-based, validates only the requested route once per work/deposit attempt, performs no generic Content Patcher graph scan, and never runs repeated full-map route discovery in hot paths. Recommended.
B) Lazy per-hop validation: validate the next hop only as the worker reaches it, accepting possible mid-route failure in exchange for less upfront work.
C) Aggressive caching: cache validated routes for the day and reuse them across all shed-greenhouse work/deposit attempts unless manually cleared.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

### Question 2
How fresh should route validation be across game state changes?

A) Revalidate live readiness for every shift work-route and expansion deposit-route attempt; discovery may use route-shape checks only. Do not trust cached passability across days, saves, or location reloads. Recommended.
B) Validate once when the hiring menu opens and rely on that result until the contract changes.
C) Validate once at day start and reuse the result for all shed-greenhouse route uses that day.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

### Question 3
What reliability and logging quality bar should NFR Requirements enforce?

A) All route/validation failures are total and non-throwing, skip only the affected shed greenhouse batch or deposit trip, preserve items, and emit one maintainer-facing warning per failed route attempt with route id, purpose, target, first failing hop if known, and reason. Recommended.
B) Route failures may bubble as exceptions during development builds but should be caught in release builds.
C) Route failures should be silent unless they cause item overflow, to reduce log noise.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

### Question 4
What automated test rigor should be required for this unit?

A) Example tests plus FsCheck properties with domain generators for route definitions, requests, validation failures, policy decisions, destination filtering, and item-safety mapping; shrinking/reproducibility must remain enabled. Recommended.
B) Example tests for route definitions and a minimal FsCheck property for deterministic route lookup only.
C) Example tests only; rely on manual SVE playtest for route behavior.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

### Question 5
What tech stack direction should NFR Requirements record?

A) Reuse the existing stack only: C#/.NET, SMAPI/Stardew APIs, current movement/navigation services, xUnit, and FsCheck. No new runtime dependency, route-graph package, or Content Patcher parser. Recommended.
B) Add a dedicated route-graph/pathfinding library for cross-location routes.
C) Add a Content Patcher map/action parser so routes can be discovered dynamically at runtime.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

### Question 6
What manual SVE verification breadth should be required before TODO-10 can close?

A) Require at least one full live SVE playtest on a supported farm map that selects the shed greenhouse, reaches it through the multi-hop route, performs crop work, deposits or exits item-safely, and verifies no player-facing route-error UI; cover the other supported farm maps through source-grounded route data and automated route tests unless playtest time is available. Recommended.
B) Require full live playtests on all three supported SVE farm maps before closing TODO-10.
C) Require automated verification only; document manual SVE playtest as optional.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

## Answer Validation

The user instructed `use recommended`, so the recommended `A` option was applied to all six questions.

| Question | Answer | Validation result |
|---|---|---|
| Q1 | A | Complete and valid. Bounded synchronous route validation aligns with the approved Functional Design and avoids hot-path graph discovery. |
| Q2 | A | Complete and valid. Per-attempt live readiness validation resolves freshness concerns across save, day, and location state changes. |
| Q3 | A | Complete and valid. Total non-throwing failures, narrow skip behavior, item preservation, and one warning match the Functional Design failure model. |
| Q4 | A | Complete and valid. Example tests plus FsCheck properties satisfy the enabled Property-Based Testing partial-mode obligations for invariants, generators, shrinking, and reproducibility. |
| Q5 | A | Complete and valid. Reusing the existing C#/.NET, SMAPI, navigation, xUnit, and FsCheck stack satisfies PBT-09 and avoids unsupported runtime dependencies. |
| Q6 | A | Complete and valid. One full live SVE playtest plus source-grounded data and automated tests balances runtime verification with bounded manual scope. |

No answer is ambiguous or contradictory. No clarification question file is required.

## Extension Compliance at Question Gate

| Extension | Status | NFR Requirements question-gate result |
|---|---|---|
| Security Baseline | Disabled | Skipped per TODO-10 configuration; no network, authentication, secrets, or PII surface is introduced. |
| Property-Based Testing | Enabled - Partial | No blocking finding at question gate. PBT-09 is directly applicable in NFR Requirements; FsCheck is already selected for the project. PBT-02, PBT-03, PBT-07, and PBT-08 are carried into Code Generation and Build/Test where applicable. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
- Questions use the required `[Answer]:` tag format.
- `X) Other` is the last option for every question.
