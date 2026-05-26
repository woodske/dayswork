# U-24 — Config, Regression, and Documentation Cleanup: Business Logic Model

**Unit**: U-24 — Config, Regression, and Documentation Cleanup  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=C, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A

U-24 is the final redesign sweep. It does not add new farmhand gameplay. It consolidates the redesign-era player-facing configuration surface, tightens the i18n boundary, adds targeted regression verification for unchanged-but-risky behavior, refreshes build/test documentation to the new fixed-price/worker-energy model, and centralizes accepted deviations and remaining verification caveats.

See [domain-entities.md](domain-entities.md) for the cleanup data shapes, [business-rules.md](business-rules.md) for enforceable constraints, and [frontend-components.md](frontend-components.md) for the GMCM-facing structure.

---

## 1. Cleanup role in the retrofit sequence

Earlier retrofit units already changed the live behavior:
- `U-18` introduced fixed-price terms, typed scope, and worker-energy profiles
- `U-19` persisted scope plus terms snapshots
- `U-20` rewired the hire/edit preview flow
- `U-21` changed runtime stamina/pacing
- `U-22` made typed scope authoritative at runtime
- `U-23` rebuilt recurring billing/day-start behavior

U-24 is the final alignment pass:

```text
approved redesign behavior
  -> expose only redesign-era config concepts to players
  -> verify unchanged critical behaviors still hold
  -> rewrite build/test docs around the redesign model
  -> consolidate deviations and known caveats in one reviewable place
```

The unit's goal is coherence, not another gameplay branch.

---

## 2. Redesign-era config authority

### 2.1 Player-facing config concepts

After U-24, the player-facing configuration model is the redesign model only.

The authoritative configurable concepts are:
- outdoor work size thresholds
- outdoor per-service package prices by size band
- animal-building prices by service and building tier
- greenhouse fixed package prices by service
- worker daily energy capacity
- per-action energy costs
- worker pacing and recovery controls
  - walk speed
  - action animation duration
  - entrance hold
  - hard cap time
  - initial stuck threshold
  - post-teleport stuck threshold

The player-facing config model is no longer:
- hourly base rate
- per-task hourly surcharges
- deposit-oriented estimate tuning

### 2.2 Clean break saved-config behavior

U-24 adopts a redesign-only saved `config.json` shape.

That means:
- the saved file written after U-24 uses only redesign-era fields
- U-24 does not promise to preserve old hourly/deposit-era fields in the saved config
- U-24 does not promise a dual-format config bridge for one more cycle

This is a deliberate clean break. If an older hand-edited config file is incompatible, the accepted outcome is that the player may need to regenerate or hand-fix it.

### 2.3 Internal compatibility boundary

U-24 does not require every internal compatibility field to disappear from the codebase immediately.

Instead, the authoritative boundary is:
- player-facing config and GMCM are redesign-only
- any still-needed internal legacy compatibility values are derived or maintained behind the scenes and are not exposed as first-class player tuning concepts

This allows the final cleanup to simplify the user-facing model without forcing unrelated runtime/persistence seams to be rewritten again in the same unit.

---

## 3. GMCM registration flow after cleanup

### 3.1 Registration intent

GMCM remains optional. When present, it should expose the redesign-era tuning model completely enough that players do not need the config file for ordinary balancing.

The registration flow becomes:

```text
GameLaunched
  -> probe GMCM API
  -> if absent: no-op
  -> if present: register redesign-only sections and fields
  -> save/publish through existing ModConfigManager path
```

### 3.2 Section structure

The final GMCM surface should be organized around redesign-era mental models, not historical implementation history.

The intended sections are:
- pricing
  - outdoor thresholds
  - outdoor service band prices
  - animal-building prices
  - greenhouse package prices
- worker stamina
  - daily capacity
  - per-action costs
- worker behavior
  - walk speed
  - action animation duration
  - entrance hold
  - hard cap
  - stuck thresholds

### 3.3 Runtime adoption behavior

U-24 preserves the existing runtime-snapshot rule:
- active shifts keep the already-committed runtime config for that day
- future previews, recurring rebuilds, and future shift starts use the newly saved config

This keeps config edits consistent with the redesign's “current day is already committed” model.

---

## 4. i18n and hardcoded-string cleanup

### 4.1 Enforcement target

U-24 keeps the lint gate strict for user-visible strings while preserving practical exemptions for non-user-facing technical literals.

After U-24, the enforced user-visible boundary remains:
- in-game UI labels and body text
- mail sender/body text
- GMCM labels/tooltips/section titles
- player-facing HUD/help/error text
- player-facing log/help text that is intentionally part of the experience

The exemptions remain:
- internal IDs
- technical keys
- debug/maintainer-only logs
- reflection strings / asset identifiers / console command names

### 4.2 Final cleanup intent

The final cleanup pass should therefore:
- route any remaining redesign-visible strings through `i18n/default.json`
- keep the lint test authoritative for the player-visible boundary
- avoid expanding the lint gate into a “no English literals anywhere” policy

This keeps S-20 practical and stable.

---

## 5. Targeted regression sweep

U-24 does not try to exhaustively replay every historical unit. It focuses on unchanged-but-risky behaviors that the redesign could have broken indirectly.

### 5.1 Regression focus areas

The targeted automated regression scope is:
- output destination routing and overflow fallback
- tool snapshot and skip behavior
- stuck recovery
- invulnerability / hit reaction continuity
- multiplayer guard
- i18n lint gate

### 5.2 Regression logic

The sweep is intentionally targeted because:
- the redesign already introduced strong direct coverage in `U-18` through `U-23`
- these behaviors are high-value but not the main feature being changed in U-24
- the final unit should close risk, not balloon into another broad implementation phase

So the U-24 regression workflow is:

```text
identify unchanged high-risk behavior
  -> add or refresh focused automated tests
  -> prove redesign changes did not regress it
  -> document the remaining manual verification caveats
```

---

## 6. Build-and-test documentation refresh

### 6.1 Rewrite strategy

U-24 fully rewrites the detailed build/test instruction files to the redesign-era model.

The post-U-24 docs should describe:
- fixed contract pricing instead of deposits/refunds
- recurring 6am rebuild/charge behavior
- worker stamina/action-cost behavior
- typed-scope runtime behavior for outdoor zones, animal buildings, and greenhouse
- targeted regression checks for unchanged critical behaviors

They should not describe the hourly/deposit/refund model as the active system of record.

### 6.2 Documentation outputs

The existing build/test doc set remains the target:
- `build-instructions.md`
- `unit-test-instructions.md`
- `integration-test-instructions.md`
- `performance-test-instructions.md`
- `build-and-test-summary.md`

But their content is refreshed to be redesign-native, not patched with an addendum.

### 6.3 Regression checklist addition

U-24 also adds a short explicit regression checklist covering the targeted unchanged behaviors so reviewers know what to sanity-check after the redesign:
- output delivery / overflow mail
- tool snapshot skips
- stuck recovery
- worker invulnerability
- multiplayer refusal
- i18n/lint expectations

---

## 7. Deviations and caveats consolidation

U-24 collects redesign-relevant accepted deviations and remaining verification caveats into one reviewable place.

This consolidated note should cover:
- accepted deviations that still matter to understanding the shipped redesign behavior
- known verification caveats still worth manual attention
- anything intentionally deferred that could otherwise be mistaken for a bug

This does not replace `aidlc-state.md` or `audit.md` as the audit trail. It creates a cleaner reviewer-facing summary of the currently relevant exceptions.

---

## 8. Testable properties and reviewable outcomes

Because Property-Based Testing remains partially enabled, U-24 should preserve deterministic/config-focused seams suitable for automated regression.

| Area | Property or expectation |
|---|---|
| redesign config mapping | equivalent redesign-only config input produces equivalent runtime snapshot output |
| lint boundary | player-visible literals remain caught while approved technical literals stay exempt |
| targeted regressions | unchanged destination/skip/recovery/invulnerability behaviors still satisfy their earlier invariants after the redesign |
| build/test docs | instruction files no longer describe the hourly/deposit/refund model as the active behavior |
| deviation register | accepted deviations/caveats are consolidated consistently from the redesign record |

U-24 therefore finishes the retrofit by making the codebase, the player-facing tuning surface, the tests, and the docs all tell the same story.
