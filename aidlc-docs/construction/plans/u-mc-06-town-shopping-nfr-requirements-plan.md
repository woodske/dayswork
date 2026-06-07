# NFR Requirements Plan — U-MC-06 Town Shopping

**Unit**: U-MC-06 — Town Shopping · **Stage**: CONSTRUCTION — NFR Requirements

## Approach
No blocking question round. The approved Functional Design, the feature-level NFR set
(NFR-MC-01..09), and the existing runtime architecture already fix the quality bar; the two
genuinely-open behavioural decisions (global store preference DEV-MC-06-01; fail-skip
resilience DEV-MC-06-02) were resolved **with the user** during Functional Design. The
recommended NFR posture is recorded in `nfr-requirements.md` / `tech-stack-decisions.md`.
(Per the user's instruction, the approval gate is retained — this stage is **not**
auto-continued.)

## What is new in U-MC-06 vs. earlier MC units (drives the NFR focus)
- **Wallet/gold mutation** — first MC unit that spends player gold. Item & gold safety
  (NFR-MC-03) becomes a *blocking* concern, not inherited/unaffected.
- **Live shop API read** (`Data/Shops` via `ShopBuilder`, headless) — a new live-game
  integration with a cost/caching and resilience profile.
- **New cross-location town navigation** — multi-hop Farm↔store routes with a failure mode
  (skip-on-failure) that must never lose gold or items.
- **Store-hours timing** — deterministic open/closed gating drives deferral.

## Categories evaluated
- [x] Performance — live shop read cost + caching; paced beats reuse existing cadence.
- [x] Reliability / resilience — route/bind failure skip; per-line transaction atomicity.
- [x] Security — N/A (in-game local gold deduction; no network/PII/auth). Security Baseline disabled.
- [x] Maintainability / determinism (PBT full mode) — all decision logic pure in Core.
- [x] Item & gold safety — promoted to blocking for this unit.
- [x] Persistence compatibility — no schema change; one new config key only.
- [x] i18n — all new HUD/config strings i18n-routed.
- [x] Tech stack — reuse C#/.NET 6 + SMAPI 1.6 shop APIs + xUnit/FsCheck; no new dependency.

## Checklist
- [x] Analyze FD artifacts (business-logic-model, domain-entities, business-rules, frontend-components).
- [x] Confirm no genuine NFR ambiguity remains for user input (the two real decisions were FD-stage).
- [x] Generate `nfr-requirements.md` (NFR-MC6-01..10).
- [x] Generate `tech-stack-decisions.md`.
- [x] Present standardized 2-option completion message; wait for explicit approval.
