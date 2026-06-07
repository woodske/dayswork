# Functional Design Plan — U-MC-06 Town Shopping

**Unit**: U-MC-06 — Town Shopping
**Stage**: CONSTRUCTION — Functional Design
**Stories**: S-30
**Requirements**: FR-MC-12, FR-MC-13, FR-MC-14, FR-MC-15, FR-MC-16, FR-MC-17, FR-MC-18,
FR-MC-19, FR-MC-20, FR-MC-41
**NFRs**: NFR-MC-01 (determinism/PBT), NFR-MC-02 (perf), NFR-MC-03 (item/gold safety),
NFR-MC-05 (resilience), NFR-MC-07 (i18n), NFR-MC-08 (test rigor), NFR-MC-09 (tech stack)
**Design refs**: M-26 `ShopPurchaseService`, S-D shopping sub-service, orchestration steps
3–4 in [manage-crops-services.md](../../inception/application-design/manage-crops-services.md);
component methods in [manage-crops-component-methods.md](../../inception/application-design/manage-crops-component-methods.md).

## Unit Boundary (what is and isn't in U-MC-06)

U-MC-06 turns the **already-built pure shopping logic** (U-MC-01: `StoreResolver`,
`CropSupplyPlanner`, `ShopStockSnapshot`, `SupplyTarget`, `PurchaseLine`) into a real
**autonomous town shopping trip** during the managed-crop shift. U-MC-05 already calls
`CropShiftPlanner.Plan(...)` with `stockSnapshots: null` so no purchase lines are produced
and the worker plants only from the input chest; U-MC-06 supplies **live shop stock**,
applies a **global preferred-store setting**, and **executes** the resolved purchase lines
as a paced headless transaction, then plants from the now-stocked carry inventory.

**In scope**
- A **global** Manage Crops preferred-store config (Pierre / Joja / Either) applied to all
  crop groups (per user decision Q1; supersedes the per-`SeasonCropChoice.StorePreference`).
- **Up-front shift purchase manifest**: aggregate every managed zone's `SupplyTarget`s into
  one per-store, per-item manifest sized by **planned plantable viable tiles** (FR-MC-12),
  resolved against **live** store stock; wallet-funded with **max-affordable** shortfall
  handling that preserves seed/fertilizer atomic parity.
- **Live headless shop read + transaction** (M-26 `ShopPurchaseService`): read live
  `Data/Shops` stock **and prices** via `ShopBuilder` without opening `ShopMenu`; deduct
  gold from the wallet; grant items to the worker's carried inventory.
- **Store-hours-aware deferral**: do supply-independent field work 6–9 AM; defer departure
  until the resolved store opens; never idle at a closed store.
- **New cross-location town routes** on `CrossLocationRouteNavigator` (Farm↔SeedShop,
  Farm↔JojaMart); **multi-store** trip visits the preferred store first, then the other
  store only for items the preferred could not supply (per user decision Q1).
- **Paced transactions**: one purchase line per beat at `WorkerActionAnimationMs`, each
  beat emitting its own HUD notice; festival skip notice; preferred-closed fallback notice;
  insufficient-funds notice.
- **Leftovers** purchased-but-unused settle to the **input chest** at shift end (reusing the
  U-MC-05 carried-supply seam at `ShiftOrchestrator.ManagedCrops.cs`).
- **Resilience** (per user decision Q2): a failed town route or a failed headless shop bind
  **skips shopping with a HUD notice** and continues the shift from on-hand supply only;
  no gold spent, no items lost.
- Shopping costs **shift time only, not energy** (FR-MC-41).

**Out of scope (deferred, with seams left)**
- **Per-zone harvest routing to assigned `ChestRef`** and **greenhouse/SVE-shed
  season-agnostic support** → **U-MC-07**. U-MC-06 keeps the U-MC-05 output-chest fallback
  and operates on the open farm only.
- Membership/loyalty modelling beyond "Joja shoppable without membership; Pierre unlimited
  stock" (FR-MC-15) — no membership state is tracked.

## Decisions resolved with the user (this stage)

| # | Decision | Resolution |
|---|----------|------------|
| Q1 | **Store-preference model + multi-store trip policy.** | **User answer:** store preference is a **global Manage Crops setting** applied to all crop groups; the worker attempts to buy everything at the preferred store and buys anything missing at the other store. Captured as **DEV-MC-06-01**: new global config `PreferredCropStore` (Pierre/Joja/Either) overrides per-choice `StorePreference`; the existing `StoreResolver.ResolvePreferred` already buys per-item at preferred-else-other, and `ResolvePurchaseLines` groups by store — the trip visits the preferred store first, then the other store only if it owns ≥1 resolved line. |
| Q2 | **Navigation / transaction failure resilience.** | **User answer:** *Skip shopping, keep working.* Captured as **DEV-MC-06-02**: a failed town route or a failed live-shop bind aborts only the shop trip with a HUD notice; the shift continues planting from on-hand input-chest supply (partial-plant); no gold spent, no items lost. |

## Recommended Design Decisions (spec-pinned; no further question round)

| # | Decision | Recommended |
|---|----------|-------------|
| Q3 | **Quantity target.** Compute the shift purchase quantity up front from **planned plantable viable tiles** (bare diggable + already-tilled-empty tiles in viable zones), independent of how many are tilled when the worker shops (FR-MC-12). Requires a planning-time plantable-tile count distinct from U-MC-05's execution-time `CanAcceptSeed` candidates — a small pure Core helper feeding the existing `CropSupplyPlanner.CalculatePurchaseTargets(crop, viableTileCount, …)`. | **A** |
| Q4 | **Shift-level aggregation.** Aggregate all managed zones' `SupplyTarget`s into one shift manifest (sum by item, resolve to stores, dedupe) so the worker makes at most one trip covering all zones, rather than one trip per zone. | **A** |
| Q5 | **Live stock + prices.** Extend `ShopStockSnapshot` (or a sibling price carrier) with per-item **unit price** so the pure affordability calc is deterministic; M-26 `ReadStock` fills stock+price+open-state from live `Data/Shops`/`ShopBuilder`. Pierre = unlimited stock (large sentinel); Joja markup respected from live prices. | **A** |
| Q6 | **Max-affordable & atomicity.** A pure `PurchaseAffordabilityCalculator` clamps the manifest to wallet gold, buying the **maximum affordable** quantity while preserving seed↔fertilizer parity per crop so the worker never buys seed it cannot fertilize (atomic with §6.3). | **A** |
| Q7 | **Store hours.** A pure `StoreHoursPolicy`: open 9 AM; Pierre 9 AM–5 PM, closed Wednesdays; Joja 9 AM–11 PM daily. Drives departure deferral and the "store closed → fallback/skip" decisions deterministically. | **A** |
| Q8 | **Trip ordering in the shift.** 6–9 AM supply-independent field work → defer until store open → walk preferred (then other) → headless paced purchase → walk back → supply-dependent fertilize/seed/water → end-of-shift leftovers to input chest (orchestration steps from S-C/S-D). | **A** |
| Q9 | **Chest-supply-only crops.** Never trigger a trip and emit no purchase notice; naturally enforced because live shops do not stock them (no resolved line) — no special-casing required at runtime (FR-MC-14). | **A** |
| Q10 | **Energy/pricing.** Shopping spends shift **time only**, no energy; no separate gold charge for crop management beyond the seed/fertilizer purchase (FR-MC-41/42). | **A** |
| Q11 | **Test rigor (PBT full mode).** Keep manifest aggregation, store resolution, store-hours, and affordability in **pure Core** with FsCheck properties (max-affordable never exceeds wallet; atomic seed/fertilizer parity; preferred-then-other store grouping; chest-supply-only never purchased). M-26 live transaction + navigation are example/adapter-covered; manual playtest closes the unit. | **A** |

## Plan Checklist

- [x] Analyze unit context (unit-of-work U-MC-06, story S-30, FR-MC-12..20/41, S-D sub-service, M-26 methods).
- [x] Confirm the pure seam boundary: `StoreResolver`/`CropSupplyPlanner` already implement preferred-then-other per item and group by store.
- [x] Resolve the two consequential decisions **with the user** (Q1 global store, Q2 fail-skip).
- [x] Resolve spec-pinned decisions Q3–Q11 with recommended options.
- [x] Generate `business-logic-model.md` (manifest assessment, deferral, trip, headless paced purchase, settlement, failure paths).
- [x] Generate `domain-entities.md` (global config, manifest/affordability types, price-bearing stock snapshot, store-hours policy, purchase result, town routes, M-26).
- [x] Generate `business-rules.md` (BR-MC6-* incl. global store, up-front target, max-affordable atomicity, hours/festival, paced notices, leftovers, item/gold safety, fail-skip).
- [x] Generate `frontend-components.md` (GMCM global preferred-store dropdown + HUD notice set).
- [x] Present standardized 2-option completion message; wait for explicit approval.

## Extension Compliance

- **Security Baseline**: N/A (disabled for Manage Crops; no network/PII/auth surface — the
  "transaction" is an in-game gold deduction against the local save, not an external payment).
- **Property-Based Testing (full mode)**: compliant. All shopping decision logic (manifest
  aggregation, store resolution, store-hours, max-affordable/atomicity) stays pure in
  `Dayswork.Core` and carries explicit FsCheck obligations (Q11). The live headless
  transaction and town navigation are SMAPI-boundary adapters, example-covered per the
  established U-MC-02/U-MC-05 precedent.
