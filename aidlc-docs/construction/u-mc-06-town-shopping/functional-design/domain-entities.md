# Domain Entities — U-MC-06 Town Shopping

**Unit**: U-MC-06 — Town Shopping · **Story**: S-30 · **Stage**: Functional Design

New and extended types for the autonomous shopping trip. **Pure Core** types live in
`Dayswork.Core.Crops`; **runtime/adapter** types live in `Dayswork` (SMAPI boundary).
Existing U-MC-01 types (`Store`, `StorePreference`, `SupplyTarget`, `PurchaseLine`,
`StoreResolution`, `StoreClosedReason`, `CropSupplyPlanner`, `StoreResolver`,
`ShopStockSnapshot`, `SupplyInventory`, `CropDescriptor`) are reused as-is unless noted.

---

## Pure Core (Dayswork.Core)

### Config value — global preferred store (DEV-MC-06-01)
- The contract-level / per-`SeasonCropChoice` `StorePreference` is **superseded at runtime**
  by a single **global** Manage Crops setting. No new enum is required — the runtime maps the
  global config string to the existing `StorePreference` values **Pierre | Joja | Either**
  (`InputChestOnly` remains a domain value but is not a user-selectable global option; it is
  unused at runtime because chest-supply-only crops are filtered by live stock, not by
  preference).
- The per-choice `SeasonCropChoice.StorePreference` field is **retained** for
  save-compatibility but is **ignored** when building the shift manifest; the global value is
  passed uniformly to every `CalculatePurchaseTargets` / `Resolve` call.

### `ShopStockSnapshot` — **extended with prices** (Q5)
- Add per-item **unit price** alongside the existing per-item quantity, so the affordability
  calculation is pure and deterministic (no live-API call inside Core):
  - `int UnitPriceOf(string itemId)` backed by an `IReadOnlyDictionary<string,int> Prices`.
- `QuantityOf` / `IsOpen` / `Store` unchanged. Pierre's "unlimited stock" is represented by a
  large sentinel quantity; Joja markup is reflected in its prices. Back-compat: the existing
  constructor stays valid (prices default empty); a new overload accepts prices.

### `ShiftPurchaseManifest` — *new pure aggregate*
- The whole-shift purchase intent across **all** managed zones, sized by planned plantable
  tiles (FR-MC-12).
- Fields: `IReadOnlyList<StorePurchaseGroup> Groups` (one per store, in visit order:
  preferred first, then the other), `IReadOnlyList<string> ChestSupplyOnlyItems`
  (items that produced no line — used to suppress purchase notices, FR-MC-14).
- `StorePurchaseGroup`: `Store Store`, `IReadOnlyList<PurchaseLine> Lines`, `int TotalCost`.

### `PlannedPlantableTileCounter` — *new pure helper*
- `int CountPlantable(FieldState field, CropZoneAssignment zone, SeasonCropChoice choice, bool isViable)`
  — counts assigned tiles plantable **this shift**: already-tilled-empty OR
  (bare + diggable + not cropped + clearable-this-shift). Distinct from U-MC-05's
  execution-time `CanAcceptSeed` candidate set; feeds `CalculatePurchaseTargets`.

### `ShiftSupplyAggregator` — *new pure service* (or methods folded onto `CropSupplyPlanner`)
- `ShiftPurchaseManifest BuildManifest(CropPlan plan, FieldState field, SupplyInventory chest,
  StorePreference globalPreference, IReadOnlyList<ShopStockSnapshot> liveStock, bool isFestivalDay)`
  — per-zone planned counts → per-zone targets → summed by item → `StoreResolver` grouping.

### `PurchaseAffordabilityCalculator` — *new pure service* (Q6)
- `AffordablePurchasePlan ClampToWallet(ShiftPurchaseManifest manifest, int walletGold)`
  — returns the maximum-affordable subset preserving **seed↔fertilizer parity per crop**
  (never buy seed without its matching fertilizer); `bool Shortfall` flags an
  insufficient-funds notice. Monotonic in `walletGold`; never exceeds budget.
- `AffordablePurchasePlan`: per-store affordable `PurchaseLine`s + `int TotalCost` + `Shortfall`.

### `StoreHoursPolicy` — *new pure policy* (Q7)
- `bool IsOpen(Store store, int timeOfDay, DayOfSeason day)` and
  `int? OpensAt(Store store, DayOfSeason day)`.
- Rules: open 9 AM (`0900`); **Pierre** 9 AM–5 PM (`0900`–`1700`), **closed Wednesdays**;
  **JojaMart** 9 AM–11 PM (`0900`–`2300`) daily. Total function; FsCheck-covered.

---

## Runtime / SMAPI adapters (Dayswork)

### `ShopStockReader` (M-26 `ReadStock`) — live read
- `ShopStockSnapshot ReadStock(Store store)` — resolves the store's live `Data/Shops` entry
  via `ShopBuilder`, projecting available stock quantities **and** unit prices into a pure
  `ShopStockSnapshot`, plus open-state from `StoreHoursPolicy` + live location. No `ShopMenu`
  is opened. Returns a closed/empty snapshot if the store data can't be resolved.

### `ShopPurchaseService` (M-26) — headless transaction
- `PurchaseResult BuyHeadless(Store store, IReadOnlyList<PurchaseLine> lines, Farmer wallet, IInventory carry)`
  — for each line: re-check live stock, deduct gold from `wallet`, grant items to `carry`;
  returns per-line outcomes. **Gold is deducted only for items actually granted** (item/gold
  safety). On a bind/grant failure it returns a failure outcome **without** deducting
  (DEV-MC-06-02).
- `PurchaseResult`: `IReadOnlyList<PurchaseLineOutcome> Outcomes`, `bool BindFailed`.
- `PurchaseLineOutcome`: `Store`, `string ItemId`, `string DisplayName`, `int RequestedQty`,
  `int BoughtQty`, `int UnitCost`, `PurchaseOutcomeKind Kind` (Full / Partial / Insufficient /
  OutOfStock / BindFailed).

### Town routes on `CrossLocationRouteNavigator` — *new route definitions*
- Vanilla **Farm↔SeedShop** and **Farm↔JojaMart** multi-hop route definitions consumed by the
  existing `Navigate(...)`/`Start(...)` path (analogous to the SVE-shed expansion routes).
  Multi-store visit order = preferred store first, then the other (DEV-MC-06-01). A route that
  cannot be built/validated yields the existing navigation-failure signal → shopping skip
  (DEV-MC-06-02).

### `ManagedCropShiftRunner` (M-27) — extended
- Drives the shopping phase: builds the manifest (Core), runs `StoreHoursPolicy` deferral,
  invokes the navigator + `ShopPurchaseService` per store, emits paced HUD notices, and
  settles leftovers to the input chest at shift end. Per-tile beat execution is unchanged
  from U-MC-05.

### `CropHudNotifier` (M-29) — extended notice set
- `PurchaseCompleted(itemName, qty)`, `UsingFallbackStore(store)`, `FestivalSkipped()`,
  `InsufficientFunds()`, and a **new** `ShoppingUnavailable()` (route/bind failure skip).
  All strings i18n-routed (NFR-MC-07).

---

## Configuration (Dayswork/Integration/ModConfig)
- **`PreferredCropStore`** (string: `Pierre` | `Joja` | `Either`, default **`Either`**) —
  the single global Manage Crops store preference (DEV-MC-06-01). Surfaced in GMCM
  (see [frontend-components.md](frontend-components.md)); reused `WorkerActionAnimationMs`
  paces purchase beats (no new pacing knob).

---

## Entity relationship summary

```
CropPlan ─┬─> PlannedPlantableTileCounter ─┐
FieldState┘                                 ├─> CropSupplyPlanner.CalculatePurchaseTargets
SupplyInventory(input chest)  ──────────────┘            │
PreferredCropStore (global config) ─────────────────────┤
ShopStockSnapshot[] (live stock+prices) ────────────────┤
                                                         ▼
                                          ShiftSupplyAggregator.BuildManifest
                                                         │
                                                         ▼
                                              ShiftPurchaseManifest ──> PurchaseAffordabilityCalculator(wallet)
                                                                                   │
                                                                                   ▼
                                                                        AffordablePurchasePlan
                                                                                   │
                              (per store, in visit order) ────────────────────────┤
                                                                                   ▼
       CrossLocationRouteNavigator(town routes) ──> ShopPurchaseService.BuyHeadless ──> PurchaseResult
                                                                                   │
                                                                                   ▼
                                                              CropHudNotifier (paced per-line notices)
                                                                                   │
                                                                                   ▼
                                              carried supply ──> U-MC-05 planting ──> leftovers ──> INPUT chest
```
