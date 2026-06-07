# U-MC-06 NFR Requirements

**Unit**: U-MC-06 — Town Shopping
**Stage**: CONSTRUCTION — NFR Requirements
**Status**: Review required

No question round was needed: the approved Functional Design, the feature-level NFR set
(NFR-MC-01..09), and the existing runtime architecture fix the quality bar; the two
genuinely-open behavioural decisions (DEV-MC-06-01 global store preference, DEV-MC-06-02
fail-skip) were resolved with the user at Functional Design. The recommended posture is below.
U-MC-06 is the first Manage Crops unit that **mutates player gold** and **integrates a live
1.6 game API**, so item/gold safety and live-integration resilience are blocking concerns.

## NFR-MC6-01 — Determinism (PBT full mode)
All shopping *decision* logic — planned-plantable counting, shift-manifest aggregation, store
resolution/grouping (preferred-then-other), store-hours, and max-affordable clamping with
seed/fertilizer atomic parity — is **pure and deterministic** in `Dayswork.Core`. Same inputs
(CropPlan, FieldState, chest stock, live stock+prices, wallet, date) always yield the same
manifest and affordable plan. The runtime adapter reads live state and applies the pure plan;
it contains no pricing/quantity/store decisions of its own. (NFR-MC-01)

## NFR-MC6-02 — Performance
The shopping phase must not stall the synchronous tick loop:
- The live shop snapshot (stock + prices) is read **at most once per store per shift** and
  cached for that shift's manifest + transaction; no per-tile or per-beat shop reads.
- Manifest aggregation and affordability are O(zones × items), computed once up front.
- Travel reuses the existing bounded route/navigation helpers (no new farm-wide pathfinding);
  purchase beats reuse the existing `WorkerActionAnimationMs` cadence and tick throttle. (NFR-MC-02)

## NFR-MC6-03 — Item & gold safety (BLOCKING for this unit)
- Gold leaves the wallet **only** in exchange for items actually granted to the worker's carry
  inventory; a failed/partial grant deducts gold for **only** what was granted, never more.
- Max-affordable never spends below zero and never exceeds wallet gold; seed/fertilizer parity
  is preserved so no gold is spent on seed that cannot be fertilized.
- No purchased item is ever destroyed: it is planted, returned to the **input chest** at shift
  end, or preserved via the existing overflow-to-mail path if the chest is full.
- A failed headless bind spends **no** gold (DEV-MC-06-02). (NFR-MC-03)

## NFR-MC6-04 — Vanilla / no-SVE invariance
With no managed crop plan (or no purchasable deficit), no trip occurs and behaviour is
unchanged. Town routes are added only for vanilla Farm↔SeedShop / Farm↔JojaMart; SVE absence
changes nothing. The new global config key defaults to a no-surprise value (`Either`). (NFR-MC-04)

## NFR-MC6-05 — Resilience (BLOCKING for this unit)
Every external failure degrades gracefully, never throwing or aborting the shift:
- **Town route unavailable/blocked** (unsupported/modded farm map, no path) → skip shopping +
  HUD notice; continue planting from on-hand supply.
- **Headless shop bind/read failure** → skip shopping + HUD notice; no gold spent.
- **Preferred store closed** → fallback store + notice; **all stores closed** → skip + notice.
- **Insufficient funds** → buy max affordable + notice; continue.
- **Festival** → skip + notice; rest of shift runs.
- **Sleep mid-trip** → U-MC-05 sleep-stop settles carry to input chest.
All of these are bounded, single-shift outcomes with no persistent corruption. (NFR-MC-05)

## NFR-MC6-06 — Backward-compatible persistence
U-MC-06 adds **no** save-schema change. It reads the existing V3 `CropPlan`. The only new
persisted state is the GMCM config key `PreferredCropStore` (config.json, not the save). The
retained per-`SeasonCropChoice.StorePreference` field is ignored at runtime but still
round-trips, so existing saves load unchanged. (NFR-MC-06)

## NFR-MC6-07 — i18n
All new player-facing text — the GMCM dropdown label/tooltip and the HUD notices (bought,
fallback store, festival skipped, insufficient funds, shopping unavailable) — is i18n-backed
and passes the hardcoded-string lint gate. (NFR-MC-07)

## NFR-MC6-08 — Test rigor
- FsCheck properties (full mode) for the new pure seams: affordable plan never exceeds wallet;
  seed/fertilizer parity holds under any shortfall; manifest grouping is preferred-then-other
  and stable; chest-supply-only items never appear in a purchase line; store-hours is a total
  function of (store, time, weekday); planned-plantable count is deterministic.
- xUnit examples for runtime wiring at the live-API boundary: shop read projection
  (stock+price+open), headless buy gold/item exchange, per-line outcome → notice mapping,
  end-of-shift leftover settle, route-failure skip.
- Manual SMAPI playtest closes the unit: walk-to-store + headless buy, Pierre-Wednesday
  fallback, insufficient funds, festival skip, route/bind failure skip, leftovers to input
  chest. (NFR-MC-08)

## NFR-MC6-09 — Tech stack
Reuse C#/.NET 6 + SMAPI 1.6 shop APIs (`Data/Shops` / `ShopBuilder`), the existing
cross-location navigation layer, `WorkerActionAnimationMs` pacing, HUD/i18n/config seams, and
xUnit + FsCheck. **No new runtime dependency.** (NFR-MC-09)

## NFR-MC6-10 — Usability / feedback
The trip is legible to the player: per-line paced HUD notices show the purchase resolving item
by item; fallback / insufficient-funds / festival / unavailable each emit a clear single
notice; chest-supply-only crops are silent (no misleading "unavailable" message). (NFR-MC-05/07, S-30)

## Extension Compliance
| Extension | Status |
|---|---|
| Security Baseline | N/A — disabled for Manage Crops. The "transaction" is a local in-game gold deduction against the save; no network, payment, PII, auth, or external surface. |
| Property-Based Testing | Compliant, full mode — all shopping decision logic is pure in `Dayswork.Core` and carries blocking FsCheck obligations (NFR-MC6-08); the live read/transaction/navigation are example/playtest-covered adapters. |
