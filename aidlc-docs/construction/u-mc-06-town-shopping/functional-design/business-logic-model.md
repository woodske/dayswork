# Business Logic Model — U-MC-06 Town Shopping

**Unit**: U-MC-06 — Town Shopping · **Story**: S-30 · **Stage**: Functional Design

Technology-agnostic model of the autonomous seed/fertilizer shopping trip that runs inside
a managed-crop shift. Builds on the U-MC-01 pure shopping seams (`StoreResolver`,
`CropSupplyPlanner`, `ShopStockSnapshot`, `SupplyTarget`, `PurchaseLine`) and the U-MC-05
managed-crop runtime. Everything decision-shaped is pure Core; only the live shop read,
gold deduction, item grant, and physical walk are SMAPI-boundary adapters.

---

## 1. Where shopping sits in the shift

A managed-crop shift (U-MC-05) already runs as: build plan → supply-independent per-tile
work (harvest → clear debris → till → water-existing) → supply-dependent work (fertilize →
seed → water). U-MC-06 inserts a **shopping phase between** the supply-independent and
supply-dependent phases:

```
6:00 AM  Shift start
         │  Build the shift purchase manifest up front (§2) from the saved CropPlan,
         │  the live field state, and current input-chest stock.
         ▼
6:00–9:00 Supply-independent field work across all managed zones
         │  (harvest-first, clear debris, till viable bare tiles, water existing crops).
         │  This fills the pre-open window instead of idling at a closed store.
         ▼
~9:00 AM  Shopping decision (§3): festival? manifest empty? stores closed? route OK?
         │   ├─ skip with notice (festival / nothing to buy / fail) → straight to planting
         │   └─ otherwise: walk to preferred store (then the other), headless paced
         │      purchase (§4), walk back to the farm.
         ▼
         Supply-dependent field work: fertilize → seed → water the planned tiles, now
         funded by carried purchases + chest stock (U-MC-05 re-plan loop reuses the field).
         ▼
End of shift  Leftover purchased supplies settle to the INPUT chest (§5); harvest/output
              settle to the output chest (U-MC-05 fallback; per-zone routing is U-MC-07).
```

Shopping spends **shift time only** (the walk + paced transaction beats); it never spends
worker energy (FR-MC-41).

---

## 2. Building the shift purchase manifest (pure, up front)

At shift assessment, **before any tilling**, the worker computes how much it will need —
sized by **planned** plantable tiles, not by how many happen to be tilled when it shops
(FR-MC-12). This is the single most important correctness point of the unit: on a fresh,
untilled zone the execution-time `CanAcceptSeed` candidate set is empty, so the manifest
must instead count tiles the worker *intends to till and plant this shift*.

1. **Planned plantable count per zone** — for each `CropZoneAssignment` resolved to today's
   `SeasonCropChoice`, count the assigned tiles that are **plantable this shift**:
   - the zone's crop is **viable** (matures before season end with its fertilized growth
     time; greenhouse/shed bypass is U-MC-07, so open-farm only here), AND
   - the tile is **plannable**: either already tilled & empty, **or** bare + diggable +
     not already cropped + not blocked by un-clearable debris (debris the worker will clear
     this shift counts as plantable; permanently-blocking content does not).
   A new pure helper `PlannedPlantableTileCounter` derives this from `FieldState` + the
   zone + the viability result. Chest-supply-only crops are still counted (they may be
   planted from chest stock) but produce no purchase target (§ below).
2. **Per-zone supply targets** — feed each zone's planned count into the existing
   `CropSupplyPlanner.CalculatePurchaseTargets(crop, plannedCount, chestInventory,
   effectivePreference, liveStock)`. The **effective preference** is the global
   `PreferredCropStore` (DEV-MC-06-01), not the per-choice value.
3. **Aggregate into one shift manifest** — sum all zones' targets by item id, then resolve
   each to a store with `StoreResolver` against **live** stock. Group the resulting
   `PurchaseLine`s by store. The result is a `ShiftPurchaseManifest`: an ordered set of
   per-store line groups, plus the chest-supply-only / unavailable items that produced no
   line (used only to decide whether to notify).
4. **Affordability clamp** — a pure `PurchaseAffordabilityCalculator` takes the manifest +
   live unit prices + the player's wallet gold and returns an **affordable plan**: the
   maximum quantity buyable within budget, **preserving seed↔fertilizer parity per crop**
   so the worker never buys seed it cannot fertilize (atomic with §6.3). If gold covers
   everything, the plan equals the manifest; otherwise it is the largest atomic subset.

The manifest and affordable plan are fully deterministic given (CropPlan, FieldState,
chest stock, live stock, prices, wallet, date) — all FsCheck-covered.

---

## 3. The shopping decision (when the worker actually goes)

After the 6–9 AM supply-independent work, the worker evaluates, in order:

1. **Nothing to buy** — the affordable plan has no lines (already supplied from the chest,
   or only chest-supply-only / unavailable items). → **skip silently**, go straight to
   planting. Chest-supply-only crops never trigger a trip and emit no purchase notice
   (FR-MC-14).
2. **Festival day** — purchasing is skipped with a festival HUD notice; the rest of the
   shift still runs (FR-MC-16). (Store resolution already returns `Festival` closed-reason.)
3. **Store hours** — `StoreHoursPolicy` decides whether each needed store is open now and
   when it opens. The worker **defers departure** until the preferred (or fallback) store
   opens at 9 AM, filling the wait with remaining field work; it never walks to a store
   that is closed. Pierre is closed Wednesdays → fall back to Joja with a fallback notice
   (FR-MC-20). If every needed store is closed for the whole remaining shift, skip with a
   notice.
4. **Route availability** — resolve the new town route(s) on `CrossLocationRouteNavigator`.
   If no usable route exists (unsupported/modded farm map, blocked path), **skip shopping
   with a HUD notice** and continue planting from on-hand supply (DEV-MC-06-02).

Only if all gates pass does the trip proceed.

---

## 4. The trip and the headless paced transaction

**Travel.** The worker physically walks to and enters the store using **new** cross-location
town routes (Farm↔SeedShop, Farm↔JojaMart) added to the existing navigation layer
(`CrossLocationRouteNavigator` / `BuildingWorkNavigator`). For a **multi-store** plan the
worker visits the **preferred store first**, completes its lines, then walks to the **other
store** only if it owns ≥1 line (DEV-MC-06-01 / Q1), then returns to the farm. Route legs
are chained nearest-first.

**Headless transaction.** At the counter the purchase is resolved **without opening the
visual `ShopMenu`** (M-26 `ShopPurchaseService.BuyHeadless`):
- read the store's **live** `Data/Shops` stock + prices via `ShopBuilder` (so mod-added
  seeds, price-modifying mods, seasonal gating, and Joja markup are all honoured, and the
  "only if this store stocks it" rule is exact);
- for each affordable line, **deduct gold** from the wallet and **grant** the items to the
  worker's carried inventory;
- return a `PurchaseResult` of per-line outcomes (bought quantity, unit cost, full/partial/
  insufficient) for paced notices.

**Pacing + notices.** Lines are executed as a **paced sequence**, one line per **beat** at
`config.WorkerActionAnimationMs` (the same cadence as task swings and the deposit loop).
Each completed beat emits its **own** HUD notice (e.g. "Bought 12× Parsnip Seeds")
(FR-MC-19). A preferred-closed fallback emits a fallback notice (FR-MC-20); a wallet
shortfall emits an insufficient-funds notice once.

---

## 5. Settlement and leftovers

- **Carried purchases** stay in the worker's carry inventory for the walk back and feed the
  supply-dependent planting phase (U-MC-05's re-plan loop already reads carry + chest stock
  and lays fertilizer→seed→water atomically).
- **Leftovers** — supplies purchased or carried but unused at shift end **return to the
  INPUT chest**, the persistent player-stocked reservoir the availability gate checks first
  (§6.10). This reuses the U-MC-05 carried-supply-return seam
  (`ShiftOrchestrator.ManagedCrops.cs` end-of-shift settle). If the input chest is full, the
  existing overflow-to-mail path preserves the items (no loss).
- **Harvest/output** continues to settle via the U-MC-05 output-chest fallback; per-zone
  routing is U-MC-07.

---

## 6. Failure and safety paths

| Situation | Behaviour |
|-----------|-----------|
| Festival day | No trip; festival HUD notice; shift continues (FR-MC-16). |
| Manifest empty / only chest-supply-only | No trip; no purchase notice; plant from chest (FR-MC-14). |
| Preferred store closed (e.g. Pierre Wed) | Use the other store; fallback HUD notice (FR-MC-20). |
| All needed stores closed for the rest of the shift | Skip with notice; plant from chest. |
| Wallet < manifest cost | Buy max affordable (atomic parity); insufficient-funds notice; continue (FR-MC-12). |
| Town route unavailable/blocked | **Skip shopping**, HUD notice, plant from chest; no gold spent (DEV-MC-06-02). |
| Headless shop bind fails | **Skip shopping**, HUD notice, plant from chest; **no gold deducted** for ungranted items (DEV-MC-06-02). |
| Inventory full when granting/returning | Items preserved via input-chest / overflow-mail; gold deducted **only** for items actually granted (item/gold safety). |
| Player sleeps mid-trip | U-MC-05 sleep-stop settles carry to input chest; no loss. |

**Item & gold safety invariant (NFR-MC-03):** gold leaves the wallet **only** in exchange
for items actually placed in the worker's inventory; no purchased item is ever destroyed —
it is planted, returned to the input chest, or mailed on overflow.

---

## 7. Determinism & test surface (NFR-MC-01/08, PBT full mode)

Pure, FsCheck-covered:
- `PlannedPlantableTileCounter` — planned count is a deterministic function of field+zone+viability.
- manifest aggregation + `StoreResolver` grouping — preferred-then-other per item; grouped by store.
- `StoreHoursPolicy` — open/closed is a total function of (store, time, day-of-week).
- `PurchaseAffordabilityCalculator` — affordable plan never exceeds wallet; preserves
  seed/fertilizer parity; monotonic in wallet; chest-supply-only never purchased.

Example/adapter-covered (SMAPI boundary): M-26 live read/transaction, town-route navigation,
paced beat emission, HUD notices, end-of-shift leftover settle. Manual SMAPI playtest closes
the unit (walk-to-store, headless buy, fallback day, insufficient funds, festival, route-fail).
