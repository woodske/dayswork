# Integration Test Instructions — Dayswork Fixed-Price Redesign

## Overview

Dayswork is still verified in the live game through SMAPI-driven manual playtesting. There is no separate service stack or automated end-to-end harness. The integration goal for the redesign is to confirm the fixed-price contract flow, typed scope execution, recurring lifecycle, and worker stamina behavior all match the approved rules in a real save.

## Environment Setup

1. Build and deploy the mod.
2. Install Mail Framework Mod.
3. Optionally install GMCM for settings verification.
4. Launch Stardew Valley through SMAPI.
5. Open a farm save that has:
   - outdoor crop zones
   - at least one barn or coop
   - a greenhouse
   - assignable chests and a reachable shipping bin

## Manual Scenarios

### Scenario 1 — Mod load and bulletin board entry

Verify:

- the mod loads without SMAPI errors
- the bulletin board shows the hire/manage entry in single-player
- the multiplayer refusal path still logs the friendly refusal message instead of opening the flow

### Scenario 2 — GMCM redesign surface

Verify:

- GMCM shows only `Pricing`, `Worker Stamina`, and `Worker Behavior`
- outdoor thresholds, fixed-price tables, stamina, and pacing values persist after reopening the menu
- no hourly/deposit-era labels are still visible

### Scenario 3 — One-time contract review and purchase

Verify:

- the flow is now `Tasks -> Work Scope -> Output Destinations -> Schedule -> Review Contract`
- review pricing reflects outdoor bands, animal buildings, and greenhouse packages correctly
- one-time contracts charge the fixed price immediately
- invalid preview states block confirmation only on the review page

### Scenario 4 — Recurring 6am lifecycle

Verify:

- recurring contracts rebuild terms at day start before an eligible charge
- festival days skip the worker and charge nothing
- rain and low-work days keep the same recurring price and stay quiet unless another notice reason applies
- edit-before-6am changes affect that same morning's rebuild path

### Scenario 5 — Worker stamina and pacing

Verify:

- the worker shows a visible stamina bar overhead
- movement and action pacing feel slower and readable
- stamina spends on labor beats rather than walking
- when stamina reaches zero, the worker finishes the current work unit, deposits output, and leaves
- the worker also stops at the hard-cap time only after finishing the current work unit

### Scenario 6 — Scope-driven execution

Verify:

- outdoor services only work inside selected outdoor scope
- selected barns/coops cover their animals even when those animals roam outside
- greenhouse work runs as its own crop scope
- output routing remains task-owned while overflow explanations still mention the right scope family

### Scenario 7 — Final regression surfaces

Verify:

- chest routing, shipping bin routing, and next-morning overflow mail still behave correctly
- tool snapshot and skip rules still respect the player's tool levels at shift start
- stuck recovery still resolves or aborts safely
- worker hit reactions still emote instead of taking damage or breaking the shift

## Logs

Primary log location:

```text
%AppData%\StardewValley\ErrorLogs\SMAPI-latest.txt
```

Look for `[Dayswork]` lines when validating recurring notices, runtime scope skips, deposit behavior, and stuck recovery.
