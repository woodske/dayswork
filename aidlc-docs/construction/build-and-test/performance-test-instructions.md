# Performance Test Instructions — Dayswork Fixed-Price Redesign

## Scope

Formal load or concurrency testing is still not applicable. U-24 performance verification is about making sure the redesign stays lightweight inside the game loop and the day-start recurring pass.

## Areas to Watch

### Live preview responsiveness

- task toggles should refresh preview immediately
- scope edits should recompute synchronously without visible menu lag
- review page scrolling should remain responsive for large contracts

### Runtime pacing

- worker stamina updates should stay in sync with visible labor beats
- slower pacing should feel intentional, not laggy
- the overhead stamina bar should update immediately when a work beat spends energy

### Recurring day-start pass

- 6am recurring rebuild/charge logic should not cause visible hitching when the day starts
- festival, cannot-afford, and needs-attention notice selection should stay synchronous

## Informal Verification

1. Turn on SMAPI trace logging if a slowdown is suspected.
2. Exercise a large review contract with many pricing lines and confirm the menu remains usable.
3. Run a recurring contract over multiple mornings and watch for hitching at day start.
4. Observe a long worker shift and confirm the stamina bar and action cadence stay visually aligned.

## Escalation Guidance

If a performance issue is observed:

1. capture the exact screen or phase where it happens
2. note whether the issue is preview-time, day-start, or live-runtime
3. gather SMAPI logs
4. profile the game process only if the lightweight evidence is not enough
