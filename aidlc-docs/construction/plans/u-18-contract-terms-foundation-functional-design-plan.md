# U-18 — Contract Terms Foundation: Functional Design Plan

**Unit**: U-18 — Contract Terms Foundation  
**Phase**: CONSTRUCTION — Functional Design  
**Status**: Answers reviewed, no clarification round needed, and functional-design artifacts generated. Pending user review.

---

## Plan Checklist

- [x] Load unit definition, refreshed requirements, refreshed stories, and refreshed application design
- [x] Collect answers to FD-Q1 through FD-Q10
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [x] Present completion message and await approval

---

## Context Loaded

- [unit-of-work.md](../../inception/application-design/unit-of-work.md) — U-18 definition
- [unit-of-work-story-map.md](../../inception/application-design/unit-of-work-story-map.md) — story ownership for S-02, S-03, S-06, S-19
- [requirements.md](../../inception/requirements/requirements.md) — pricing, scope, and energy requirements
- [stories.md](../../inception/user-stories/stories.md) — player-facing expectations for pricing preview and stamina preview
- [application-design.md](../../inception/application-design/application-design.md) — redesign summary
- [components.md](../../inception/application-design/components.md)
- [component-methods.md](../../inception/application-design/component-methods.md)
- [services.md](../../inception/application-design/services.md)

---

## What This Unit Must Define

U-18 is the new pure foundation that replaces the old hourly `rate/deposit/refund/hours` model from historical `U-05`.

This unit owns:
- `C-01 WorkScopeClassifier`
- `C-02 OutdoorServiceBandClassifier`
- `C-03 ContractPriceCalculator`
- `C-04 PriceBreakdownBuilder`
- `C-05 WorkerEnergyProfileBuilder`
- `C-06 ContractTermsBuilder`

This unit also introduces or locks the business shape of:
- `ContractScopeSelection`
- `WorkScopeSet`
- `OutdoorWorkScope`
- `AnimalBuildingScope`
- `GreenhouseWorkScope`
- `OutdoorServiceBand`
- `PricingSnapshot`
- `PricingLineItem`
- `ContractTermsSnapshot`
- `ContractPreview`
- `WorkerEnergyProfile`

---

## Already Decided And Not Re-Decided Here

- Hourly billing, deposit math, refund math, and estimated-hours preview are gone.
- Outdoor work is priced by broad size bands derived from configured scope, not by exact morning work found.
- Animal care is priced from selected barn/coop building scope, not from where animals happen to stand.
- Greenhouse crop work is a fixed package, not outdoor banding.
- Worker energy is separate from price, walking does not cost energy, and energy clamps at zero.
- If energy reaches zero mid-work-unit, the worker finishes that unit and then leaves after deposit behavior.
- One-time contracts keep the terms snapshot created at confirmation time.
- Recurring contracts rebuild terms from saved scope and current config on the next eligible day.

This plan focuses only on the remaining functional-design choices that still shape the pure contract-terms model.

---

## Design Questions

> Answer each question by writing after its `[Answer]:` tag. Pick the letter that best matches your preference. If none fit, choose `X` and describe your preference after the tag.

### FD-Q1 — How should overlapping outdoor zones be normalized for pricing?

Players can draw multiple outdoor rectangles, and those rectangles can overlap. We need a deterministic rule before any service banding happens.

A) **Union the outdoor footprint first (Recommended)** — merge all selected outdoor rectangles into one normalized non-overlapping outdoor footprint, then classify each relevant outdoor service once from that combined area. Overlap never double-charges.

B) **Classify each rectangle independently** — price each selected rectangle separately per relevant outdoor service, then sum the results. Overlap can increase price if the player intentionally drew multiple rectangles over the same space.

C) **Sum raw rectangle areas without geometric union** — the total outdoor area is just the sum of width × height for every rectangle, even if they overlap.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q2 — Should outdoor band thresholds be shared or service-specific?

Every outdoor service needs broad size bands such as small / medium / large, but we still need to decide whether the thresholds themselves vary by service.

A) **Shared thresholds, service-specific prices (Recommended)** — all outdoor services use the same size-band thresholds, but each service has its own configurable prices for those bands.

B) **Service-specific thresholds and prices** — Harvest Crops, Clear Rocks, Cut Trees, and so on can each define different tile thresholds and different prices.

C) **Shared thresholds and shared prices** — every outdoor service uses the same thresholds and the same band prices; the service only controls whether a line appears.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q3 — When does a selected scope become part of the priced work scope set?

The raw `ContractScopeSelection` can include outdoor zones, barns/coops, and the greenhouse even if the enabled task set does not actually use all of those scope types.

A) **Only materialize scope types relevant to the enabled tasks (Recommended)** — the raw selection is preserved, but `WorkScopeSet` and pricing only include scope types that matter for the currently enabled tasks.

B) **Materialize every selected scope type and keep irrelevant ones as zero-priced entries** — irrelevant scopes remain visible in the built terms and preview, but contribute no price.

C) **Treat irrelevant scope selections as invalid** — if the player selected a scope type that none of the enabled tasks use, the preview becomes invalid until the mismatch is fixed.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q4 — What is the animal-building price key?

Animal care is building-based, but we still need the business key that determines the configured price.

A) **Building tier / capacity key (Recommended)** — price from the building's vanilla progression tier such as `Coop`, `BigCoop`, `DeluxeCoop`, `Barn`, `BigBarn`, `DeluxeBarn`, independent of the current animal count.

B) **Generic barn-vs-coop key only** — all coop variants share one coop price key and all barn variants share one barn price key.

C) **Current occupancy / capacity key** — price from how many animals the building currently houses (or can house) when terms are built.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q5 — How should multiple animal services price against the same selected building?

If the player selects `Feed Animals`, `Pet Animals`, and `Collect Animal Products` for the same coop or barn, we need to decide whether those services stack or bundle.

A) **Additive per selected animal service (Recommended)** — each selected animal service contributes its own price line for each selected building price key.

B) **Single bundled animal-care price per building** — once a building is selected for any animal task, all chosen animal services are covered by one combined building charge.

C) **Hybrid bundle pricing** — animal services still depend on which services are selected, but some combinations collapse into discounted bundle prices.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q6 — How should greenhouse pricing compose with selected greenhouse crop services?

The greenhouse is a fixed package, but we still need to decide whether that package is one combined fee or separate fixed fees per selected service.

A) **Separate fixed package per selected greenhouse crop service (Recommended)** — greenhouse watering, greenhouse harvest, and greenhouse fruit collection each contribute their own fixed greenhouse package line when selected.

B) **One combined greenhouse package** — any selected greenhouse crop service turns on one greenhouse package charge that covers all selected greenhouse crop services together.

C) **Charge only if the greenhouse currently contains actionable work** — the greenhouse package appears only if there are relevant crops/fruit to work on that day.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q7 — What should a pricing breakdown line item represent?

`PricingSnapshot` is used by both preview UI and persisted terms, so its line-item shape needs to be stable and legible.

A) **Aggregate by pricing key after normalization (Recommended)** — one line per priced contribution such as `Harvest Crops — Outdoor Large`, `Pet Animals — Deluxe Coop x2`, or `Water Crops — Greenhouse Package`, with deterministic ordering.

B) **One line per physical scope instance** — each individual zone/building/greenhouse selection gets its own separate pricing line even if two lines share the same price key.

C) **Only broad family subtotals** — the breakdown has only aggregate lines such as `Outdoor Work`, `Animal Care`, and `Greenhouse`.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q8 — What should happen if a contract has no chargeable scope-task pair?

Examples: the player enables only animal tasks but selects no barns/coops; or enables only outdoor tasks with no outdoor zones; or selects the greenhouse but no greenhouse-relevant crop service.

A) **Return an invalid preview with reasons and block terms creation (Recommended)** — `ContractPreview` carries validation messages, and no confirmable `ContractTermsSnapshot` is produced until the contract includes at least one chargeable scope-task pair.

B) **Return zero-price / zero-energy terms and allow confirmation** — the player can confirm a free contract that simply results in no work.

C) **Silently drop the non-chargeable parts and build terms from whatever remains** — the builder auto-prunes the mismatch without surfacing an invalid state.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q9 — How fine-grained should energy action costs be in `WorkerEnergyProfile`?

The pricing redesign already chose vanilla-like energy behavior, but we still need the business granularity of `WorkActionKind`.

A) **Fine-grained labor actions (Recommended)** — use action kinds that map to the actual work beats the runtime will spend energy on, such as `WaterTile`, `HarvestCrop`, `HarvestFruit`, `PetAnimal`, `FeedAnimal`, `CollectAnimalProduct`, `AxeSwing`, `PickaxeSwing`, and `ScytheSwing`.

B) **Top-level task costs only** — one cost per selected task category such as `WaterCrops`, `HarvestCrops`, `ClearRocks`, `CutTrees`, and so on.

C) **Hybrid interaction costs** — a smaller set than A, but still more detailed than top-level tasks; for example `CropInteraction`, `AnimalInteraction`, `TreeStage`, `RockObject`, `GrassPatch`.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

### FD-Q10 — How much of the action-cost table should be snapshotted into one-time contract terms?

One-time contracts preserve the exact `ContractTermsSnapshot` confirmed at hire time. We need to decide how much of the energy profile becomes part of that snapshot.

A) **Snapshot full capacity plus the full action-cost table (Recommended)** — one-time terms preserve the worker's daily capacity and the entire configured action-cost map for all known work actions, even if some actions are not used by this contract.

B) **Snapshot capacity plus only the action costs relevant to the enabled task set** — one-time terms keep only the subset of action costs reachable from the selected tasks.

C) **Snapshot daily capacity only** — runtime reads the current action-cost map from config when the shift begins.

X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-18-contract-terms-foundation/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-18-contract-terms-foundation/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-18-contract-terms-foundation/functional-design/business-rules.md`
