# U-MC-06 NFR Design Patterns

**Unit**: U-MC-06 — Town Shopping
**Stage**: CONSTRUCTION — NFR Design
**Status**: Review required

All mandatory NFR Design categories (resilience, scalability, performance, security, logical
components) were evaluated. No additional question round was needed: the approved Functional
Design, NFR Requirements, and the two user decisions (DEV-MC-06-01 global store,
DEV-MC-06-02 fail-skip) fix the pattern set. Patterns extend the established
pure-Core / thin-adapter architecture; this unit adds the **first gold-mutating** and **first
live-API-reading** seams in Manage Crops, so a transaction-safety barrier is the new element.

## P1 — Pure shopping logic, thin runtime runner (determinism, performance)
The runtime adds **no** pricing/quantity/store decision logic. Pure Core
(`ShiftSupplyAggregator` → `ShiftPurchaseManifest`, `StoreResolver`, `StoreHoursPolicy`,
`PurchaseAffordabilityCalculator`, `PlannedPlantableTileCounter`) decides *what to buy, where,
how many, and whether affordable*. `ManagedCropShiftRunner`/`ShopPurchaseService` only read
live state, walk, deduct gold, grant items, and notify. Keeps all testable logic in Core
(NFR-MC6-01) and the hot path allocation-light (NFR-MC6-02).

## P2 — Read-once live shop snapshot (performance, determinism boundary)
`ShopStockReader` is the single live→pure boundary: it projects a store's live `Data/Shops`
stock + prices + open-state into an immutable `ShopStockSnapshot` **once per store per shift**.
The snapshot then feeds both the pure manifest/affordability calc and the headless transaction,
so price/stock are consistent within the shift and no shop read happens per tile or per beat
(NFR-MC6-02). A failed projection yields a closed/empty snapshot (feeds P5).

## P3 — Up-front planned-tile manifest (correctness)
`PlannedPlantableTileCounter` sizes the purchase by tiles the worker *plans* to till+plant
this shift, not by tiles already tilled when it shops (FR-MC-12). The manifest is built once at
assessment and aggregated across all zones into per-store groups (one trip, preferred-first).
This is the correctness keystone — without it a fresh untilled zone would buy zero seed.

## P4 — Gold-safety transaction barrier (item & gold safety — NEW, blocking)
`ShopPurchaseService.BuyHeadless` is an **exchange-atomic** step: for each line it grants items
to carry inventory and deducts gold **only for the quantity actually granted** — never deduct
first. Max-affordable clamping (pure, P1) guarantees the request never exceeds wallet and keeps
seed↔fertilizer parity, so no gold buys un-fertilizable seed. A bind/grant failure deducts
nothing (P5). This barrier is the single place gold leaves the wallet (NFR-MC6-03).

## P5 — Skip-on-failure resilience barrier (resilience — blocking; DEV-MC-06-02)
A non-throwing guard wraps the trip. If town-route resolution fails (unsupported/modded map,
blocked path) or the headless shop cannot bind/read, the runner **aborts only the shop trip**,
emits one `ShoppingUnavailable` HUD notice, and proceeds to plant from on-hand input-chest
supply. No gold spent, no items lost, rest of the contract unaffected (NFR-MC6-05). The same
barrier funnels festival / all-stores-closed / insufficient-funds into bounded notice+continue
outcomes.

## P6 — Store-hours deferral as a pure gate (resilience, performance)
`StoreHoursPolicy` (pure, total over store×time×weekday) drives departure: do
supply-independent field work 6–9 AM, defer the walk until the resolved store opens, never idle
at a closed store; Pierre-Wednesday → fallback + notice. Timing is a deterministic decision the
runner merely executes (NFR-MC6-01/05).

## P7 — Town routes on the existing navigator (reuse, maintainability)
New Farm↔SeedShop / Farm↔JojaMart route definitions plug into the existing
`CrossLocationRouteNavigator` consumed by the same `Navigate/Start` path used for building and
SVE-shed moves. Multi-store visit order (preferred → other) is data/ordering, not a new
scheduler. No new navigation subsystem (NFR-MC6-09).

## P8 — Paced beats + per-line notices reuse (consistency, usability)
Purchases execute one line per beat at the existing `WorkerActionAnimationMs` cadence — the
same throttle as task swings and the deposit loop — each beat emitting its own HUD notice via
`CropHudNotifier`. No new pacing knob; the trip reads like the rest of the worker's rhythm
(FR-MC-19, NFR-MC6-10).

## P9 — Global preference applied at the seam (DEV-MC-06-01)
The single global `PreferredCropStore` config is read once and passed uniformly as the
effective `StorePreference` into every `CalculatePurchaseTargets`/`Resolve` call. The existing
`StoreResolver.ResolvePreferred` already yields preferred-then-other per item; the per-choice
`StorePreference` is ignored at runtime (retained only for save round-trip). No serialization
change (NFR-MC6-06).

## P10 — Idempotent leftover settle (item safety)
Carried supplies unused at shift end settle back to the **input chest** through the same
idempotent end-of-shift settle U-MC-05 established; a full chest falls back to overflow-mail.
Re-entrancy/sleep-stop safe — settling twice cannot duplicate or lose items (NFR-MC6-03/05).

## Security
N/A — Security Baseline disabled for Manage Crops. The transaction is a local in-game
`Farmer.Money` deduction against the save; no network, payment processor, PII, auth, or
external surface exists to design against.

## Extension Compliance
| Extension | Status |
|---|---|
| Security Baseline | N/A (disabled). |
| Property-Based Testing | Compliant, full — P1/P3/P4(clamp)/P6/P9 carry the blocking pure properties (affordable ≤ wallet, seed/fertilizer parity, store grouping, store-hours totality, planned-tile determinism); P2/P4(grant)/P5/P7/P8/P10 are example/playtest-covered adapters. |
