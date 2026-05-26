# U-24 Functional Design Plan — Config, Regression, and Documentation Cleanup

**Unit**: U-24 — Config, Regression, and Documentation Cleanup  
**Phase**: CONSTRUCTION — Functional Design  
**Purpose**: Lock the final cleanup decisions for the redesign-era config surface, GMCM exposure, i18n/lint expectations, regression scope, and build/test documentation updates before implementation begins.

## Plan

- [x] Review U-24 unit boundaries, story assignments, and redesign carry-forward context
- [x] Capture the intended GMCM/config cleanup strategy for the redesign-era pricing and worker settings
- [x] Capture the intended i18n/lint enforcement boundary for the final sweep
- [x] Capture the intended regression-test depth for historically unchanged but high-risk behaviors
- [x] Capture the intended build/test documentation refresh approach
- [x] Wait for completed answers, validate for ambiguity, and only then generate the U-24 functional-design artifacts

Please answer the following questions by filling in the letter choice after each `[Answer]:` tag. If none of the listed options fit, choose the last option and describe your preference.

## Question 1
How should U-24 handle the player-facing GMCM surface now that the redesign is in place?

A) Fully replace the old hourly/deposit-era GMCM controls with redesign-era controls for outdoor thresholds/prices, animal-building prices, greenhouse prices, worker energy/action costs, and worker pacing/recovery. (Recommended)  
B) Keep some legacy hourly-style controls visible alongside the redesign controls for one more cleanup pass.  
C) Keep GMCM focused on a smaller subset of redesign settings and leave the more detailed price tables/action-cost maps as config-file-only knobs.  
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
How should the saved `config.json` shape behave once U-24 cleans up the redesign surface?

A) Continue loading missing/legacy values safely, but treat the redesign-era fields as the authoritative saved shape going forward when the config is next saved. (Recommended)  
B) Preserve both the old hourly-style fields and the redesign-era fields in the saved config for one more cycle.  
C) Break cleanly to a redesign-only config shape even if that means players must regenerate or hand-fix older config files.  
X) Other (please describe after [Answer]: tag below)

[Answer]: C

## Question 3
What level of regression coverage should U-24 add for historically unchanged but redesign-sensitive behavior?

A) Add targeted automated regression coverage for the highest-risk unchanged behaviors: output destinations/overflow, tool snapshot & skip rules, stuck recovery, invulnerability, multiplayer guard, and the i18n lint gate. (Recommended)  
B) Add a broader regression sweep that tries to cover nearly every historically unchanged story touched by the redesign.  
C) Keep U-24 mostly to config/docs cleanup and add only minimal regression updates beyond what earlier units already introduced.  
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4
How strict should the final i18n / hardcoded-string cleanup be?

A) Keep maintainer/debug/internal technical strings exempt, but require all player-visible UI, mail, GMCM, and player-facing log/help text to remain i18n-routed and enforced by the existing lint gate. (Recommended)  
B) Expand the lint gate to flag nearly all remaining English literals in `Dayswork/`, including most maintainer-facing logs.  
C) Rely mostly on manual review for the remaining i18n cleanup instead of tightening the lint/test boundary further.  
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5
How should U-24 refresh the build-and-test documentation for the redesign?

A) Rewrite the detailed build/test instruction files so they describe only the fixed-price/worker-energy model, and add a short explicit regression checklist for the redesign-sensitive behaviors. (Recommended)  
B) Keep the old detailed instruction files mostly intact and append a redesign addendum.  
C) Update only the summary-level build/test docs, leaving the detailed instruction files largely as-is.  
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 6
How should accepted deviations and remaining known limitations be handled in the final cleanup docs?

A) Consolidate the redesign-relevant accepted deviations and known verification caveats into the U-24 docs/regression notes so reviewers can see them in one place. (Recommended)  
B) Leave deviations and caveats only in `aidlc-state.md` / `audit.md`, and keep U-24 docs strictly focused on intended behavior.  
C) Put deviations and known limitations into a separate dedicated note rather than mixing them into the main U-24 docs.  
X) Other (please describe after [Answer]: tag below)

[Answer]: A
