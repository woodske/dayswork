# NFR Design Plan - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Change**: TODO-10 SVE Grandpa's Shed greenhouse routing
**Stage**: Construction / NFR Design
**Status**: Complete - review required

## Context

NFR Requirements are approved for `u-t10-shed-greenhouse-routing`. The approved quality bar fixes bounded table-based route validation, per-attempt live readiness, total non-throwing route failures, item-safe deposit handling, centralized SVE route data, existing-stack implementation, example tests, FsCheck properties, and one required live SVE playtest.

## NFR Design Execution Plan

- [x] Record NFR Requirements approval from user response `continue`.
- [x] Load NFR Design rule details from `.aidlc-rule-details/construction/nfr-design.md`.
- [x] Load common content-validation and question-format rules.
- [x] Load approved TODO-10 NFR Requirements and Tech Stack Decisions.
- [x] Load TODO-10 Functional Design and Application Design context.
- [x] Load enabled extension configuration and Property-Based Testing rules.
- [x] Evaluate NFR Design categories: resilience, scalability, performance, security, and logical components.
- [x] Determine whether a question round is needed.
- [x] Validate this plan content before creation: markdown only, no Mermaid diagrams, no ASCII diagrams, and no unanswered `[Answer]:` tags.
- [x] Create this NFR Design plan.
- [x] Generate `aidlc-docs/construction/u-t10-shed-greenhouse-routing/nfr-design/nfr-design-patterns.md`.
- [x] Generate `aidlc-docs/construction/u-t10-shed-greenhouse-routing/nfr-design/logical-components.md`.
- [x] Update `aidlc-docs/aidlc-state.md` and `aidlc-docs/audit.md` for NFR Design completion.
- [x] Present the standardized NFR Design completion message and wait for review approval.

## Category Evaluation

| Category | Applicability | NFR Design decision |
|---|---|---|
| Resilience Patterns | Applicable | Fixed by approved NFRs: route lookup and validation return typed success/failure values; route failures skip only the affected shed greenhouse work batch or mark the affected deposit trip undelivered/overflowed; warnings are one per failed route attempt. No open question. |
| Scalability Patterns | N/A | Single-player local SMAPI mod, small explicit route tables, and no external service or multi-user load. Bounded synchronous lookup is sufficient; no scaling mechanism or infrastructure component is needed. |
| Performance Patterns | Applicable | Fixed by approved NFRs: table lookup, one requested route validation per work/deposit attempt, no generic graph scan, no hot-path discovery, no async pipeline, and no day-long passability cache. No open question. |
| Security Patterns | N/A | Security Baseline is disabled for TODO-10 and the unit introduces no network, authentication, authorization, secrets, PII, or external process boundary. |
| Logical Components | Applicable | Fixed by approved Application Design and NFRs: profile-owned pure route data, `ExpansionCompatService` as live validation bridge, `CrossLocationRouteNavigator` as movement executor, `ShiftOrchestrator` as policy owner, draft-aware discovery/filtering, and test support through xUnit/FsCheck. No open question. |

## Question Round Decision

No additional NFR Design question round is needed. The approved Functional Design, NFR Requirements, and Tech Stack Decisions already resolve the relevant pattern choices:

- Failure strategy: typed total validation plus narrow skip/deposit-failure policy.
- Freshness strategy: per-attempt live validation without day/save caches.
- Performance strategy: bounded synchronous route lookup and validation.
- Tech stack: existing C#/.NET, SMAPI/Stardew APIs, current movement/navigation, xUnit, and FsCheck.
- Component ownership: route data in the profile seam, live validation in the compat adapter, movement in the cross-location navigator, and failure policy in `ShiftOrchestrator`.

## Extension Compliance at Plan Gate

| Extension | Status | NFR Design plan result |
|---|---|---|
| Security Baseline | Disabled | Skipped per TODO-10 configuration; no security pattern is applicable. |
| Property-Based Testing | Enabled - Partial | No blocking finding. NFR Design does not introduce new PBT framework decisions beyond the approved NFR Requirements; PBT-03/PBT-07/PBT-08 obligations are carried into Code Generation and Build/Test, PBT-09 remains satisfied by FsCheck, and PBT-02 remains N/A unless Code Generation introduces a reversible transform. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
