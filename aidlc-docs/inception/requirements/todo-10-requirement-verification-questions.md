# TODO-10 Requirement Verification Questions

Please answer each question by placing the letter choice after the matching `[Answer]:` tag. If none of the options match, choose `X` and describe the preferred answer after the tag.

## Source Context

- TODO-10 covers SVE's quest-unlocked `Custom_GrandpasShedGreenhouse`, not the standard Grandpa's Farm greenhouse named `Greenhouse`.
- The standard Grandpa's Farm greenhouse is already covered by the existing greenhouse support.
- Prior source review found the shed greenhouse is behind the shed, reached through a multi-location path rather than a single farm warp.
- Current Dayswork building navigation is single-door oriented: it resolves a farm-side approach tile, warps into one interior, finishes the batch, then exits back to the farm.
- Current scope data can store a `GreenhouseSelection(LocationName)`, but only one greenhouse-like location per contract.

## Question 1
Which Grandpa's Shed areas should TODO-10 service?

A) `Custom_GrandpasShedGreenhouse` only, as an indoor crop-work location for greenhouse services.
B) `Custom_GrandpasShedGreenhouse` plus the main shed interior for chest deposit support only.
C) The whole shed complex, including greenhouse, main shed, outside, and ruins.
X) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 2
How should players select the shed greenhouse for work?

A) Keep the current single-greenhouse scope model and expose the shed greenhouse as a selectable alternative greenhouse location when SVE makes it available.
B) Expand the scope model to support multiple greenhouse selections in one contract, so the standard greenhouse and shed greenhouse can both be selected together.
C) Automatically include the shed greenhouse whenever the standard greenhouse is selected and SVE is active.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3
Which farm-route coverage should this change include?

A) Support all SVE farm maps already in scope: Immersive Farm 2 Remastered, Grandpa's Farm, and Frontier Farm, with source-grounded route data for each.
B) Support Grandpa's Farm first and leave IF2R / Frontier for later playtest-driven follow-up.
C) Build generic route discovery for any Content Patcher farm or expansion location, including maps outside the existing SVE scope.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4
What navigation approach should be used?

A) Add an explicit SVE multi-hop route provider: each route is source-grounded, the worker walks to each hop's approach tile, and existing warp transitions move between locations.
B) Build a generic cross-location graph from live warps and tile actions, then search it at runtime for any requested location.
C) Directly warp the worker to the shed greenhouse when the route is known, without walking each hop.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5
How should the worker decide whether the shed greenhouse is currently available?

A) Attempt the route only when live locations and every configured hop validate at runtime; otherwise skip the batch with maintainer logging and no player-facing message.
B) Inspect SVE quest, event, or mail flags directly before scheduling the shed greenhouse.
C) Add a player-facing unavailable notice when a contract targets the shed greenhouse before it is reachable.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 6
Which tasks should run in the shed greenhouse?

A) Greenhouse crop services only: Water Crops and Harvest Crops, using the existing greenhouse pricing, stamina, batching, and output provenance.
B) Greenhouse crop services plus clearing tasks inside the shed greenhouse.
C) All eligible outdoor and greenhouse services across the whole shed complex.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 7
How should output destinations work for shed-greenhouse work?

A) Treat it like the existing greenhouse scope: preserve shipping/bin and farm chest behavior, and allow chests inside the selected shed greenhouse as deposit destinations if discovered.
B) Allow only shipping/bin and farm chests for shed-greenhouse output.
C) Add chest discovery and deposit support across every shed sub-location, even if only the greenhouse is serviced.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 8
What should happen if a configured route is blocked or cannot be resolved during a shift?

A) Skip the shed-greenhouse batch, continue the rest of the shift, preserve item safety, and log the reason for maintainers.
B) Fall back to directly warping the worker to the target greenhouse.
C) Stop the contract as needs-attention and send player-facing mail.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 9
What verification bar should this change meet?

A) Add pure route-model examples and FsCheck properties, update relevant integration tests, then require a manual SMAPI playtest with SVE for at least one completed shed-greenhouse route.
B) Add example-based automated tests and rely on manual SMAPI playtest for route behavior.
C) Manual SMAPI playtest only, because the route depends on SVE assets.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 10
Should security extension rules be enforced for this project?

A) Yes - enforce all SECURITY rules as blocking constraints.
B) No - skip all SECURITY rules for this local SMAPI mod change.
X) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 11
Should property-based testing (PBT) rules be enforced for this project?

A) Yes - enforce all PBT rules as blocking constraints.
B) Partial - enforce PBT rules only for pure functions and serialization round-trips.
C) No - skip all PBT rules.
X) Other (please describe after [Answer]: tag below)

[Answer]: B
