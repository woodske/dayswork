# Business Rules — U-MC-06 Town Shopping

**Unit**: U-MC-06 — Town Shopping · **Story**: S-30 · **Stage**: Functional Design

Runtime/decision rules for the autonomous shopping trip. Each rule cites its requirement
and notes whether it is **pure** (FsCheck-covered) or an **adapter** (example/playtest).

---

## Store selection & preference

- **BR-MC6-01 — Global preferred store (DEV-MC-06-01).** A single global config
  `PreferredCropStore ∈ {Pierre, Joja, Either}` applies to **all** crop groups and overrides
  the per-`SeasonCropChoice.StorePreference`. The per-choice field is retained for
  save-compatibility but ignored at runtime. *(FR-MC-15; user Q1; pure)*
- **BR-MC6-02 — Preferred-then-other, per item.** Each needed item is bought at the preferred
  store if it stocks it (live), otherwise at the other store; `Either` uses the first open
  store stocking it (Pierre-first order). Resolved lines are **grouped by store** and the trip
  visits the **preferred store first, then the other** only if it owns ≥1 line. *(FR-MC-15/20;
  user Q1; pure — existing `StoreResolver.ResolvePreferred` + `ResolvePurchaseLines`)*
- **BR-MC6-03 — Availability gate (live stock).** Only items **live-stocked** at a store are
  purchasable; the "only if that store stocks it" rule is exact because stock is read from live
  `Data/Shops` via `ShopBuilder`. Pierre is treated as unlimited stock; JojaMart is shoppable
  **without membership** (no membership check). *(FR-MC-13/15/18; adapter read → pure resolve)*
- **BR-MC6-04 — Chest-supply-only crops never shop.** Crops not stocked at any store
  (ancient fruit, coffee, foraged, seed-maker output) produce no purchase line and trigger
  **no** store trip and **no** purchase notice; they are planted only from input-chest stock,
  partial-planting whatever the chest can supply. Enforced naturally because live shops do not
  stock them (no resolved line). *(FR-MC-14; pure)*

## Quantity, funding & affordability

- **BR-MC6-05 — Up-front planned-tile target.** Purchase quantity is computed up front from
  **planned plantable viable tiles** (already-tilled-empty OR bare/diggable/clearable-this-shift
  in viable zones), independent of how many are tilled when the worker actually shops. *(FR-MC-12;
  pure — `PlannedPlantableTileCounter`)*
- **BR-MC6-06 — Shift-level aggregation.** Targets from all managed zones are summed by item and
  resolved once into a single `ShiftPurchaseManifest`, so the worker makes at most one trip
  (covering both stores if needed) rather than one trip per zone. *(FR-MC-12/17; pure)*
- **BR-MC6-07 — Wallet funding, max-affordable.** Purchases are funded from the **player's
  wallet**. If gold is insufficient for the full manifest, buy the **maximum affordable**
  quantity and continue the shift. *(FR-MC-12; pure — `PurchaseAffordabilityCalculator`)*
- **BR-MC6-08 — Atomic seed/fertilizer parity under shortfall.** Max-affordable clamping never
  buys seed it cannot fertilize: per crop, the affordable plan keeps seed and required
  fertilizer in balance so every funded tile can be fully supplied (consistent with the §6.3
  atomic seed+fertilizer rule). *(FR-MC-12, FR-MC-24; pure)*

## Timing, hours & festivals

- **BR-MC6-09 — Pre-open field work, deferred departure.** The shift starts at 6 AM; stores
  open at 9 AM. The worker performs supply-independent field work in the 6–9 window and
  **defers departure** until the resolved store opens; it never walks to or idles at a closed
  store. *(FR-MC-15, spec §6.2; adapter over pure `StoreHoursPolicy`)*
- **BR-MC6-10 — Store hours.** Pierre: 9 AM–5 PM, **closed Wednesdays**. JojaMart: 9 AM–11 PM,
  daily. When the preferred store is closed (e.g. Pierre on Wednesday) the worker uses the
  other store and emits a **fallback** HUD notice. If every needed store is closed for the rest
  of the shift, shopping is skipped with a notice. *(FR-MC-15/20; pure hours, adapter decision)*
- **BR-MC6-11 — Festival skip.** On festival days purchasing is skipped with a festival HUD
  notice; the rest of the shift's tasks still run. *(FR-MC-16; pure resolve + adapter notice)*

## Travel & transaction

- **BR-MC6-12 — Physical travel via new town routes.** The worker physically walks to and
  enters the store using **new** cross-location town routes (Farm↔SeedShop, Farm↔JojaMart) on
  the existing navigation layer; a multi-store plan walks preferred → other → home. The trip is
  a deliberate time cost, never skipped when reachable. *(FR-MC-17; adapter)*
- **BR-MC6-13 — Headless transaction.** At the counter the purchase resolves **without opening
  `ShopMenu`**: read live stock+prices, deduct gold from the wallet, grant items to the
  worker's carried inventory. Mod-added seeds, price mods, seasonal gating, and Joja markup are
  all respected. *(FR-MC-18; adapter)*
- **BR-MC6-14 — Paced transactions, per-line notice.** Lines execute one per **beat** at
  `WorkerActionAnimationMs`; each completed beat emits its **own** HUD notice (e.g. "Bought 12×
  Parsnip Seeds"). An insufficient-funds shortfall emits one insufficient-funds notice.
  *(FR-MC-19; adapter)*

## Settlement, safety & cost

- **BR-MC6-15 — Leftovers to input chest.** Supplies purchased/carried but unused at shift end
  return to the **input chest** (the persistent supply reservoir the availability gate checks
  first). Reuses the U-MC-05 carried-supply-return seam; a full input chest falls back to
  overflow-to-mail (no loss). *(FR-MC-18, §6.10; adapter)*
- **BR-MC6-16 — Item & gold safety.** Gold leaves the wallet **only** in exchange for items
  actually placed in inventory; no purchased item is ever destroyed (planted, returned to the
  input chest, or mailed). *(NFR-MC-03; pure invariant + adapter enforcement)*
- **BR-MC6-17 — Time only, no energy; no extra charge.** The walk and transaction beats spend
  shift **time only**, never worker energy; there is no separate gold charge for crop
  management beyond the seed/fertilizer purchase. *(FR-MC-41/42; pure cost model)*

## Resilience (DEV-MC-06-02 / user Q2)

- **BR-MC6-18 — Skip-on-failure.** If the town route is unavailable/blocked (unsupported or
  modded farm map, no path) **or** the headless shop cannot bind to the live shop API, the
  worker **skips shopping with a HUD notice** and continues the shift planting only from
  on-hand input-chest supply (partial-plant). **No gold is spent** on a failed bind, and no
  items are lost. The rest of the contract's tasks are unaffected. *(NFR-MC-05; user Q2; adapter)*
- **BR-MC6-19 — Sleep mid-trip.** If the player sleeps while the worker is shopping or carrying
  supplies, U-MC-05 sleep-stop settles the carry to the input chest; no gold or items are lost.
  *(NFR-MC-03; adapter)*

## Determinism (PBT full mode)

- **BR-MC6-20 — Pure decision logic.** Manifest aggregation, store resolution/grouping,
  store-hours, planned-tile counting, and max-affordable/atomic clamping are **pure and
  deterministic** functions of their inputs and carry FsCheck obligations (NFR-MC-01/08): the
  affordable plan never exceeds wallet gold; seed/fertilizer parity holds under any shortfall;
  preferred-then-other grouping is stable; chest-supply-only items are never purchased; store
  hours are a total function of (store, time, weekday). The live read, transaction, navigation,
  pacing, and notices are SMAPI-boundary adapters closed by manual playtest.
