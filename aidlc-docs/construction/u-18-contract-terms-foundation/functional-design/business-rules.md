# U-18 — Contract Terms Foundation: Business Rules

**Unit**: U-18 — Contract Terms Foundation  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A, FD-Q9=A, FD-Q10=A

Enforceable rules for the new fixed-price contract-terms model. See [business-logic-model.md](business-logic-model.md) for flows and [domain-entities.md](domain-entities.md) for data shapes.

---

## No deviations introduced at U-18

U-18 does not introduce a new deviation from the approved pricing redesign. It is the unit that formalizes the already-approved direction:
- fixed contract price instead of hourly settlement
- typed scope modeling
- additive animal-building pricing
- fixed greenhouse packages
- separate worker-energy profile

---

## Scope normalization and relevance

**BR-SCOPE-01 — Raw scope selection is preserved separately from priced scope materialization.** `ContractScopeSelection` remains the source-of-truth input, but `WorkScopeSet` contains only scope families relevant to the enabled task set. *(FD-Q3=A)*

**BR-SCOPE-02 — Outdoor overlap never double-charges.** All outdoor rectangles are unioned into a normalized non-overlapping footprint before outdoor service banding or pricing occurs. *(FD-Q1=A)*

**BR-SCOPE-03 — Outdoor pricing uses the unioned tile count.** `TotalTileCount` is calculated after outdoor-zone union, not by summing raw rectangle areas. *(FD-Q1=A)*

**BR-SCOPE-04 — Animal-building scopes are created only when animal-care services are enabled.** Selecting barns/coops without any enabled animal task produces no animal-building priced scopes. *(FD-Q3=A)*

**BR-SCOPE-05 — Greenhouse scope is created only when greenhouse-relevant crop services are enabled.** Selecting the greenhouse without Water/Harvest/Collect-Fruit enabled produces no greenhouse priced scope. *(FD-Q3=A)*

**BR-SCOPE-06 — Partially unmatched task selections are allowed.** If at least one chargeable scope-task pair exists overall, selected services without matching scope simply produce no priced contribution and do not invalidate the entire contract. *(FD-Q3=A, FD-Q8=A)*

---

## Outdoor banding

**BR-BAND-01 — Outdoor threshold schema is shared across services.** All outdoor services use the same size-band thresholds. *(FD-Q2=A)*

**BR-BAND-02 — Outdoor prices remain service-specific.** Shared thresholds do not imply shared prices; each outdoor service reads its own configured price for a given band. *(FD-Q2=A)*

**BR-BAND-03 — One outdoor band record exists per relevant outdoor service.** Even when multiple outdoor services share the same unioned tile count and therefore often the same band label, they remain separate pricing records. *(FD-Q2=A)*

**BR-BAND-04 — Outdoor banding ignores daily actionable work.** Band classification must not inspect ready crops, current rock counts, weather, or any other morning runtime conditions. *(FR-PAY-03, FR-PAY-06, FR-PAY-10)*

---

## Animal-building pricing

**BR-ANIM-01 — Animal-building price key is building tier, not occupancy.** Pricing keys use vanilla building progression tiers such as `Coop`, `BigCoop`, `DeluxeCoop`, `Barn`, `BigBarn`, and `DeluxeBarn`. *(FD-Q4=A)*

**BR-ANIM-02 — Current animal count does not affect animal-building price.** Terms building must not inspect how many animals currently live in the building or where those animals stand. *(FD-Q4=A, FR-PAY-04)*

**BR-ANIM-03 — Animal services price additively.** Each selected animal service contributes its own per-building line against each selected animal-building scope. *(FD-Q5=A)*

**BR-ANIM-04 — Identical animal-building contributions aggregate in the breakdown only after calculation.** Two identical deluxe-coop `PetAnimals` contributions still exist conceptually as two priced building contributions, even though `PricingSnapshot` may collapse them into one line with quantity `2`. *(FD-Q5=A, FD-Q7=A)*

---

## Greenhouse pricing

**BR-GH-01 — Greenhouse pricing is fixed-package, not banded.** The greenhouse never participates in outdoor size-band pricing. *(FR-PAY-05, FD-Q6=A)*

**BR-GH-02 — Greenhouse services price separately.** Each selected greenhouse crop service contributes its own fixed greenhouse package line. *(FD-Q6=A)*

**BR-GH-03 — Greenhouse pricing ignores daily crop state.** The greenhouse package must not depend on whether the greenhouse currently has actionable crops or fruit on a given morning. *(FR-PAY-06, FD-Q6=A)*

---

## Price breakdown shape

**BR-PRICE-01 — `PricingSnapshot` aggregates by pricing key, not click instance.** The breakdown line-item shape is structural and normalized, not one line per selected rectangle or one line per selected building. *(FD-Q7=A)*

**BR-PRICE-02 — Outdoor line items aggregate by `(Service, Band)`.** *(FD-Q7=A)*

**BR-PRICE-03 — Animal line items aggregate by `(Service, BuildingTier)`.** The quantity field records how many selected buildings share that contribution key. *(FD-Q7=A)*

**BR-PRICE-04 — Greenhouse line items aggregate by `(Service)` because there is only one greenhouse package anchor.** *(FD-Q7=A)*

**BR-PRICE-05 — Line-item ordering is deterministic.** `PricingSnapshot.LineItems` must be emitted in canonical family/service/key order so the same input always yields the same serialized and visual order. *(FD-Q7=A, S-19)*

**BR-PRICE-06 — Total price equals the sum of line totals.** Outdoor subtotal, animal subtotal, greenhouse subtotal, and grand total must all reconcile exactly with the emitted breakdown lines. *(S-19, PBT-03)*

---

## Preview validity and contract confirmation

**BR-VAL-01 — A contract is blocking-invalid only when it has zero chargeable scope-task pairs overall.** *(FD-Q8=A)*

**BR-VAL-02 — Invalid previews return structured reasons.** When `BR-VAL-01` fails, `ContractPreview` must surface one or more `ContractValidationIssue`s explaining why no chargeable pair exists. *(FD-Q8=A)*

**BR-VAL-03 — Invalid previews produce no proposed terms snapshot.** `ContractPreview.ProposedTerms` is null when the preview is invalid. *(FD-Q8=A)*

**BR-VAL-04 — Invalid drafts cannot produce confirmable terms.** `BuildTerms(...)` must not create a confirmable `ContractTermsSnapshot` when `BR-VAL-01` fails. The exact failure transport is implementation detail; the business rule is blocking. *(FD-Q8=A)*

**BR-VAL-05 — Valid previews may still omit some selected services from price contributions.** If the contract has at least one chargeable pair overall, unmatched services do not block confirmation by themselves. *(FD-Q3=A, FD-Q8=A)*

---

## Terms lifecycle and pricing stability

**BR-TERM-01 — One-time contract terms are built from scope + tasks + config at confirmation time.** No weather, festival, or discovered morning workload state participates. *(FR-PAY-01, FR-PAY-02, FR-PAY-06)*

**BR-TERM-02 — Recurring terms are rebuilt from saved raw scope + saved tasks + current config on the next eligible day.** *(FR-PAY-06, FR-PAY-12)*

**BR-TERM-03 — Contract terms are intentionally independent of daily actionable work.** Low-work days, empty-ready-crop days, and rainy days do not change the fixed terms for a saved recurring contract. *(FR-PAY-09, FR-PAY-10, FR-DAY-03)*

**BR-TERM-04 — U-18 does not consume weather or festival inputs.** Weather and calendar affect whether a later unit spawns/charges a worker, not how U-18 computes the pure terms snapshot. *(FR-DAY-01, FR-PAY-10)*

---

## Worker-energy profile rules

**BR-ENERGY-01 — Worker energy is modeled independently from price.** `WorkerEnergyProfile` is built alongside pricing, but no price formula reads energy capacity or per-action costs. *(pricing redesign decisions)*

**BR-ENERGY-02 — Energy costs are keyed by fine-grained labor actions.** The action-cost table uses action beats such as `WaterTile`, `HarvestCrop`, `FeedAnimal`, `AxeSwing`, and so on, not broad top-level task categories. *(FD-Q9=A)*

**BR-ENERGY-03 — The action-cost table is complete for all known work actions.** `WorkerEnergyProfile` stores the full configured map for the known `WorkActionKind` set, even when the current contract only uses a subset. *(FD-Q10=A)*

**BR-ENERGY-04 — One-time contract terms snapshot the full energy table plus daily capacity.** Later config changes do not alter a confirmed one-time contract's worker-energy profile. *(FD-Q10=A, FR-PAY-12 by contrast for recurring)*

**BR-ENERGY-05 — Recurring terms rebuild against the current full energy table.** Config changes to energy capacity or action costs apply starting the next eligible recurring day. *(FD-Q10=A, FR-PAY-12)*

---

## Separation-of-concerns rules

**BR-ARCH-01 — U-18 Core types contain no SMAPI/Stardew runtime objects.** The contract-terms foundation must remain pure and directly testable. *(S-19, NFR-MAINT-03)*

**BR-ARCH-02 — U-18 Core types contain no user-facing localized strings.** Preview validity and price breakdowns use structural keys/codes that the UI later localizes. *(S-20, NFR-UX-02)*

**BR-ARCH-03 — U-18 contains no deposit/refund/hourly constructs.** Those concepts are removed from the public and persisted pricing surface. *(FR-PAY-01)*

---

## Property-based testing obligations

Property-Based Testing extension is enabled in partial mode. U-18 owns several of the strongest pure-logic seams in the redesign, so it carries the main PBT burden.

| Rule | Required property / invariant |
|---|---|
| PBT-02 round-trip | `ContractPreview` / `ContractTermsSnapshot` derived from the same valid input remain structurally stable through DTO round-trip in later persistence tests. |
| PBT-03 invariant | Outdoor overlap never increases price after union; line-item totals always reconcile to subtotals and total; invalid previews never carry proposed terms. |
| PBT-07 generator quality | Generators must cover overlapping zones, empty scope families, mixed task sets, repeated building tiers, and greenhouse/no-greenhouse combinations. |
| PBT-08 shrinking | Counterexamples must shrink to minimal scope/task combinations such as one overlapping pair of zones or one task with no matching scope. |
| PBT-09 framework | FsCheck remains the framework used for these properties. |

Recommended concrete properties for U-18:
- same input -> same `PricingSnapshot` ordering and totals
- overlapping outdoor rectangles price the same as their geometric union
- removing irrelevant scope families does not change valid priced contributions
- adding a second identical animal building increases only the matching animal line quantity/total
- one-time terms always snapshot the full known action-cost table

Security Baseline extension is disabled project-wide, so its rules are N/A here.
