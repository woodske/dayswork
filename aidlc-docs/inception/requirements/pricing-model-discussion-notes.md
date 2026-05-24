# Pricing Model Discussion Notes

**Status**: Informal design discussion captured on 2026-05-24 and adopted as input to Requirements Analysis.

## Primary problem being solved

- The current deposit-plus-refund loop feels overly complicated.
- A large upfront deposit followed by next-morning refunding feels bad even when the math is technically correct.
- Recurring pricing should stay predictable rather than varying from day to day based on whatever happened to be ready that morning.

## Direction agreed so far

- Replace the current hourly/deposit/refund feel with a simpler contract model.
- Favor a fixed contract price with a worker energy bar limiting daily output.
- Do not reintroduce refunds through leftover-energy conversion.
- Keep recurring contracts stable and legible.

## Pricing shape discussed

- Outdoor crop and clearing work should use zone-based pricing.
- Animal care should use building-based pricing.
- Greenhouse work should use a fixed package price rather than tile-band pricing.
- The player should not manually pick small/medium/large job size if that can be exploited.
- Price should be based on contract scope set up front, not on the exact work state found each morning.

## Animal-work handling

- Animal work should be anchored to selected barns/coops, not to drawn zones.
- Selecting an animal building means servicing all animals assigned to that building.
- If animals are outside, the worker should still chase them anywhere on the farm.
- Billing should not vary because one animal happened to wander awkwardly.

## Energy model

- Worker energy should be roughly comparable to the farmer's daily energy budget.
- Energy should be spent per work action, generally mirroring vanilla farmer energy usage.
- Tool work should spend energy per tool use, including repeated swings on the same object.
- Non-tool labor such as petting, harvesting, and similar interactions should also spend energy.
- Walking should not spend energy.
- Energy should never go below zero.
- If energy reaches zero during an in-progress work unit, the worker finishes that unit, then deposits materials and leaves.
- A follow-up stage such as stump removal should count as a new work unit and should not start at zero energy.

## Worker feel / pacing

- The worker should be slowed down from the current implementation.
- Movement speed should be reduced.
- Task animations / action tempo should be slower and more readable.
- Slower pacing should make the contract feel more like paid labor rather than instant automation.

## Prioritization

- For this pricing rework, keep focus on pricing and energy rather than adding a broad prioritization system.
- The player may eventually get more control over priority ordering, but that is not the immediate target of this redesign.

## Open design questions

- Exact price structure for one-time vs recurring contracts.
- How to handle rain, festivals, and no-work days under the new no-refund model.
- Whether zone-based pricing should be banded, package-based, or otherwise simplified.
- Which GMCM knobs should remain exposed after the redesign.
