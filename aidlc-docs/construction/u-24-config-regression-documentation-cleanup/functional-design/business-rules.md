# U-24 — Config, Regression, and Documentation Cleanup: Business Rules

**Unit**: U-24 — Config, Regression, and Documentation Cleanup  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=C, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A

Enforceable rules for the final redesign cleanup. See [business-logic-model.md](business-logic-model.md) for flow-level behavior, [domain-entities.md](domain-entities.md) for the cleanup data shapes, and [frontend-components.md](frontend-components.md) for the GMCM-facing structure.

---

## Config authority and saved shape

**BR-CFG-01 — The redesign-era config model is the only player-facing authority.** U-24 must expose and document only the fixed-price, typed-scope, worker-energy, and worker-pacing settings that belong to the redesign. *(FD-Q1=A)*

**BR-CFG-02 — Hourly/deposit-era config concepts are removed from the player-facing surface.** Old hourly base-rate, task-surcharge, and deposit-estimate tuning must not remain in GMCM or the intended saved config model after U-24. *(FD-Q1=A, FD-Q2=C)*

**BR-CFG-03 — The saved `config.json` shape is redesign-only after U-24.** U-24 does not preserve a dual-format saved config surface for one more cycle. *(FD-Q2=C)*

**BR-CFG-04 — Clean-break config behavior is explicit, not accidental.** If older config files require regeneration or manual repair because the redesign-only shape is now authoritative, that is an accepted outcome of this unit. *(FD-Q2=C)*

**BR-CFG-05 — Internal compatibility seams may persist, but they are not player-facing knobs.** Any legacy values still needed behind the scenes must remain implementation details rather than exposed config concepts. *(unit boundary)*

**BR-CFG-06 — Active-day runtime lock semantics remain unchanged.** Saving config changes in GMCM affects future previews, recurring rebuilds, and future shifts, not an already-started shift. *(carry-forward from U-17/U-21/U-23)*

---

## GMCM structure and validation

**BR-GMCM-01 — GMCM remains optional.** If Generic Mod Config Menu is unavailable, the mod behaves normally without the registration surface. *(carry-forward)*

**BR-GMCM-02 — The redesign-era GMCM surface must be complete enough for ordinary balancing.** Players should be able to tune pricing, worker stamina, action costs, pacing, and recovery from GMCM without needing the config file for normal use. *(FD-Q1=A)*

**BR-GMCM-03 — GMCM grouping follows redesign mental models.** Controls should be grouped by pricing, worker stamina, and worker behavior/recovery rather than by historical implementation seams. *(FD-Q1=A)*

**BR-GMCM-04 — Threshold values must remain validly ordered.** Outdoor band thresholds must preserve ascending small/medium/large semantics. *(existing config invariant)*

**BR-GMCM-05 — Price and action-cost values must remain non-negative, and energy capacity must remain positive.** Validation rules continue to prevent invalid published runtime snapshots. *(existing config invariant)*

**BR-GMCM-06 — GMCM text must remain fully i18n-routed.** Section titles, labels, and tooltips must not be hardcoded in English in code. *(FD-Q4=A, S-20)*

---

## i18n and lint boundary

**BR-I18N-01 — All player-visible redesign-era strings remain i18n-routed.** This includes UI labels, review copy, mail text, GMCM text, and player-facing HUD/help/error messages. *(FD-Q4=A)*

**BR-I18N-02 — The existing hardcoded-string lint gate remains authoritative for the player-visible boundary.** U-24 should not replace that gate with manual review. *(FD-Q4=A)*

**BR-I18N-03 — Maintainer/debug/internal technical literals remain exempt.** U-24 does not widen the lint gate into a general “all English literals are forbidden” policy. *(FD-Q4=A)*

**BR-I18N-04 — Any remaining redesign-visible literal discovered during cleanup must be routed through `i18n/default.json`.** The correct fix is to i18n-route it, not to add a casual allowlist exemption. *(S-20)*

---

## Regression scope

**BR-REG-01 — U-24 adds targeted regression coverage, not a full historical reimplementation sweep.** The final unit focuses on unchanged high-risk behavior that the redesign could plausibly have regressed. *(FD-Q3=A)*

**BR-REG-02 — Output routing and overflow fallback remain mandatory regression targets.** U-24 must verify that redesign changes did not break destination assignment, deposit fallback, or next-morning overflow behavior. *(FD-Q3=A, S-04, S-11)*

**BR-REG-03 — Tool snapshot and skip rules remain mandatory regression targets.** U-24 must verify that the redesign did not break snapshot-at-spawn capability behavior or skip semantics for unsupported tasks/objects. *(FD-Q3=A, S-09)*

**BR-REG-04 — Stuck recovery and invulnerability remain mandatory regression targets.** U-24 must verify these behaviors still hold under the redesign runtime stack. *(FD-Q3=A, S-16, S-17)*

**BR-REG-05 — Multiplayer refusal remains a mandatory regression target.** U-24 must verify that the redesign did not accidentally expose the mod in multiplayer. *(FD-Q3=A, S-18)*

**BR-REG-06 — The i18n lint gate remains part of the regression surface.** Localization regressions are treated as regression failures, not only code-style issues. *(FD-Q3=A, FD-Q4=A)*

---

## Build-and-test documentation

**BR-DOC-01 — Detailed build/test docs are rewritten to the redesign model.** U-24 does not keep the hourly/deposit/refund model alive by leaving the old docs mostly intact. *(FD-Q5=A)*

**BR-DOC-02 — The rewritten docs must describe fixed-price, worker-energy, and recurring-rebuild behavior as the active system of record.** *(FD-Q5=A)*

**BR-DOC-03 — The rewritten docs include an explicit regression checklist for redesign-sensitive unchanged behavior.** *(FD-Q5=A, FD-Q3=A)*

**BR-DOC-04 — Summary-level docs and detailed docs must agree.** U-24 must not leave the detailed instruction files describing a different model than the summary file. *(documentation coherence)*

---

## Deviations and verification caveats

**BR-NOTES-01 — Redesign-relevant deviations and known caveats are consolidated into one reviewer-facing note.** U-24 creates a clean summary rather than forcing reviewers to mine `aidlc-state.md` and `audit.md`. *(FD-Q6=A)*

**BR-NOTES-02 — The consolidated note does not replace the audit trail.** `aidlc-state.md` and `audit.md` remain the authoritative historical record. *(unit boundary)*

**BR-NOTES-03 — Only currently relevant redesign deviations and caveats should be carried forward.** The note is a practical review aid, not a dump of every historical implementation detail. *(FD-Q6=A)*

---

## Property-based testing obligations

Property-Based Testing remains enabled in partial mode. U-24 should preserve deterministic seams and strengthen regression coverage where practical.

| Rule | Required U-24 expectation |
|---|---|
| PBT-03 invariant | Equivalent redesign-only config input produces equivalent normalized runtime snapshot output. |
| PBT-03 invariant | The targeted unchanged behaviors covered by pure seams still satisfy their existing deterministic invariants after the redesign cleanup. |
| PBT-07 generator quality | Config and regression generators should cover valid threshold ordering, varied price/action-cost maps, and the unaffected runtime behaviors chosen for targeted regression. |
| PBT-08 shrinking | Counterexamples should shrink to the smallest config or behavior case that demonstrates the regression or mapping failure. |
| PBT-09 framework | FsCheck remains the property-based testing framework where U-24 extends property coverage. |

Security Baseline is disabled project-wide, so its rules are N/A for this unit.
