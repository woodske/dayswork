# U-24 — Config, Regression, and Documentation Cleanup: Frontend Components

**Unit**: U-24 — Config, Regression, and Documentation Cleanup  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=C, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A

U-24 does not add a new in-world menu. Its only real frontend surface is the optional Generic Mod Config Menu integration, plus the wording boundary enforced by i18n.

---

## Affected player-facing surfaces

| Surface | U-24 responsibility |
|---|---|
| GMCM mod page | Replace the old hourly/deposit-era controls with redesign-era pricing, stamina, and behavior controls. |
| `config.json` mental model | Match the GMCM surface so players are not asked to think in two incompatible tuning systems. |
| Player-visible text surface | Keep UI/mail/GMCM/player-facing help text fully i18n-routed. |
| Build/test docs | Not a runtime UI, but reviewer-facing documentation must now match the redesign and include a regression checklist. |

---

## GMCM component structure

### 1. `DaysworkConfigPage`

Top-level optional GMCM page registered by `GMCMRegistrar`.

Responsibilities:
- register the page only when GMCM is installed
- expose only redesign-era settings
- route all labels/tooltips through i18n
- save through the existing `ModConfigManager` path

No U-24 redesign requires:
- a new in-game setup screen
- a custom config menu implementation outside GMCM
- duplicate controls in multiple places

### 2. `PricingSection`

Purpose:
- present the fixed-price contract tuning model directly

Fields:
- outdoor band thresholds
- outdoor service price controls by band
- animal-building service price controls by tier
- greenhouse service package prices

Interaction rules:
- controls should be grouped so the player understands the scope family they are changing
- legacy hourly/deposit wording must not appear

### 3. `WorkerStaminaSection`

Purpose:
- present stamina capacity and per-action costs as their own balancing surface

Fields:
- worker daily energy capacity
- action-cost controls for each work action kind

Interaction rules:
- labels should match the visible worker-stamina model the player now sees in-game
- the section should read as “how much work the worker can do,” not as hidden internals

### 4. `WorkerBehaviorSection`

Purpose:
- present readable labor feel and recovery tuning

Fields:
- walk speed
- action animation duration
- entrance hold
- hard cap time
- initial stuck threshold
- post-teleport stuck threshold

Interaction rules:
- pacing controls should be grouped together
- recovery controls should be grouped together within the same section or subsection

---

## GMCM text and validation behavior

### Text behavior

All GMCM-facing text must come from `i18n/default.json`:
- section titles
- section tooltips
- field labels
- field tooltips

No English fallback strings should be embedded directly into the registrar implementation for player-visible use.

### Validation behavior

The GMCM surface should prevent or normalize invalid values consistently with the mapper:
- ordered thresholds stay ordered
- prices and action costs stay non-negative
- energy capacity stays positive
- pacing values stay positive
- stuck thresholds stay positive

The UI and the saved config should describe the same model; the player should not have to guess whether a setting is “real” or just a compatibility leftover.

---

## Non-goals for this unit

U-24 does not require:
- a new in-world options menu
- per-save presets UI
- import/export UI for config profiles
- a new deviations viewer inside the game
- a new translation-management surface

Its frontend job is to make the optional config page honest, complete, and consistent with the redesign that now exists everywhere else.
