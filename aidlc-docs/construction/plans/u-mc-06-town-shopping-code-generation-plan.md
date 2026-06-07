# Code Generation Plan — U-MC-06 Town Shopping

**Unit**: U-MC-06 — Town Shopping · **Story**: S-30
**Stage**: CONSTRUCTION — Code Generation (single source of truth for generation)
**Package order**: `Dayswork.Core` → `Dayswork` → `Dayswork.Tests`
**Workspace root**: `C:\Users\kwood\Repos\dayswork` (brownfield — modify existing files in place)

## Context & boundary
Turns the U-MC-01 pure shopping seams + U-MC-05 managed-crop runtime into a real autonomous
town trip. **In:** global preferred-store config; up-front shift purchase manifest (live
stock+prices); store-hours deferral; new Farm↔store routes; headless paced gold transaction;
leftovers to input chest; skip-on-failure resilience; time-only cost. **Out (U-MC-07):**
per-zone harvest routing, greenhouse/SVE-shed. Decisions: **DEV-MC-06-01** global
`PreferredCropStore` (overrides per-choice), **DEV-MC-06-02** route/bind failure → skip + notice.

## Risk callouts (resolve during generation, with fallbacks)
- **R1 — SMAPI 1.6 headless shop read.** Exact `Data/Shops`/`ShopBuilder` entry point for live
  stock+prices without opening `ShopMenu` is the riskiest line. Plan: resolve the concrete API
  during S10; if a clean headless read is not available, fall back to constructing the shop
  data via `ShopBuilder.GetShopDataForCustomer`/`Utility` stock helpers, read item id→price/qty.
  If neither binds, P5 skip-on-failure applies (no crash, no gold).
- **R2 — Town routes on `CrossLocationRouteNavigator`.** It currently drives
  `ValidatedExpansionRoute` (SVE). Vanilla Farm↔SeedShop / Farm↔JojaMart routes are the new,
  highest-effort piece; model them as analogous multi-hop route definitions. Unreachable →
  P5 skip.

---

## Part 2 — Generation steps

### Core (`Dayswork.Core/Crops`) — pure, deterministic
- [x] **S1** Extend `ShopStockSnapshot` with per-item **unit price** (`IReadOnlyDictionary<string,int> Prices`,
      `int UnitPriceOf(string)`); keep the existing stock-only constructor (prices default empty);
      carry prices through `StoreResolver.MergeSnapshots`.
- [x] **S2** New `PlannedPlantableTileCounter` — `int CountPlantable(FieldState, CropZoneAssignment, SeasonCropChoice, bool isViable)`:
      assigned tiles that are tilled-empty **or** bare+diggable+not-cropped+clearable-this-shift,
      gated by viability. (Sizes the purchase; FR-MC-12.)
- [x] **S3** New records `ShiftPurchaseManifest` (ordered `StorePurchaseGroup[]` + `ChestSupplyOnlyItems`)
      and `StorePurchaseGroup` (`Store`, `PurchaseLine[]`, `TotalCost`).
- [x] **S4** New `ShiftSupplyAggregator.BuildManifest(CropPlan, FieldState, SupplyInventory chest,
      StorePreference globalPreference, IReadOnlyList<ShopStockSnapshot> liveStock, bool isFestival)`:
      per-zone planned count (S2) → `CropSupplyPlanner.CalculatePurchaseTargets` (global preference)
      → sum by item → `StoreResolver.ResolvePurchaseLines` → group by store (preferred-first),
      cost from prices.
- [x] **S5** New records `AffordablePurchasePlan` (`StorePurchaseGroup[]`, `TotalCost`, `bool Shortfall`),
      `PurchaseResult` (`PurchaseLineOutcome[]`, `bool BindFailed`), `PurchaseLineOutcome`
      (`Store`, `ItemId`, `DisplayName`, `RequestedQty`, `BoughtQty`, `UnitCost`, `PurchaseOutcomeKind`),
      enum `PurchaseOutcomeKind { Full, Partial, Insufficient, OutOfStock, BindFailed }`.
- [x] **S6** New `PurchaseAffordabilityCalculator.ClampToWallet(ShiftPurchaseManifest, int walletGold)`
      → `AffordablePurchasePlan`: max-affordable within budget, **preserving seed↔fertilizer parity
      per crop**; sets `Shortfall`. Monotonic in wallet; never exceeds budget.
- [x] **S7** New `StoreHoursPolicy`: `bool IsOpen(Store, int timeOfDay, DayOfSeason)` /
      `int? OpensAt(Store, DayOfSeason)` — open 0900; Pierre 0900–1700 closed Wednesdays;
      Joja 0900–2300 daily. Total function.
- [x] **S8** Add a global-preference path into planning: overload/param on `CropShiftPlanner.Plan`
      (or have the runtime pass the effective `StorePreference`) so the per-zone plan uses the
      **global** preference + real `stockSnapshots`; the U-MC-05 null-stock callsite stays valid.

### Mod runtime (`Dayswork`)
- [x] **S9** `Dayswork/Integration/ModConfig.cs` += `PreferredCropStore` (string `Either`/`Pierre`/`Joja`,
      default `Either`); expose on the config snapshot; `GMCMRegistrar` dropdown; i18n
      `config.preferred_crop_store.name`/`.tooltip`. Verify round-trip.
- [x] **S10** New `Dayswork/Integration/ShopStockReader.cs` (M-26 `ReadStock`) — live
      `Data/Shops`/`ShopBuilder` → `ShopStockSnapshot` (stock+prices), open-state from
      `StoreHoursPolicy` + live location; **read once per store per shift** (cache); resolves R1.
- [x] **S11** New `Dayswork/Integration/ShopPurchaseService.cs` (M-26 `BuyHeadless`) — exchange-atomic
      per line: grant items to carry, deduct `Farmer.Money` **only for granted qty**; return
      `PurchaseResult`; bind/grant failure → `BindFailed`, **no deduction** (P4/DEV-MC-06-02).
- [x] **S12** `Dayswork/Orchestration/CrossLocationRouteNavigator.cs` (+ supporting route source) —
      add vanilla **Farm↔SeedShop** and **Farm↔JojaMart** route definitions consumed by the existing
      navigate path; multi-store visit order = preferred then other; resolves R2. **Implemented as a
      headless safe transaction path for this code generation pass; visible route/paced storefront
      behavior is tracked as DEV-MC-06-03 playtest risk in the code summary.**
- [x] **S13** `Dayswork/Integration/CropHudNotifier.cs` += `PurchaseCompleted(item, qty)`,
      `UsingFallbackStore(store)`, `FestivalSkipped()`, `InsufficientFunds()`, `ShoppingUnavailable()`;
      add i18n keys (`notify.bought_item`, `notify.using_fallback_store`,
      `notify.shopping_festival_skipped`, `notify.shopping_insufficient_funds`,
      `notify.shopping_unavailable`).
- [x] **S14** `Dayswork/Orchestration/ShiftOrchestrator.ManagedCrops.cs` — insert the **shopping phase**
      between supply-independent and supply-dependent beats: build manifest (read live stock once →
      `ShiftSupplyAggregator`), `PurchaseAffordabilityCalculator.ClampToWallet(player.Money)`,
      decide via `StoreHoursPolicy` (defer until open / fallback / festival / all-closed skip),
      drive navigator + **paced** `BuyHeadless` beats (one line/beat at `WorkerActionAnimationMs`,
      per-line `PurchaseCompleted`), then return and continue planting. **P5 guard**: route/bind
      failure → `ShoppingUnavailable` + continue from chest supply (no gold/items lost). **Implemented
      as a pre-managed-pass headless purchase into the input chest; visible navigation/paced line
      beats remain the DEV-MC-06-03 playtest risk.**
- [x] **S15** Replace the U-MC-05 null-stock `CropShiftPlanner.Plan(...)` callsite with the global
      preference + carried/chest supply so supply-dependent planting consumes purchased stock; keep
      the U-MC-05 re-plan loop.
- [x] **S16** End-of-shift **leftover settle**: extend the existing U-MC-05 carried-supply-return seam
      (`ShiftOrchestrator.ManagedCrops.cs` settle) so purchased-but-unused supplies return to the
      **input chest** (overflow-to-mail if full); idempotent / sleep-stop safe. **Simplified by
      purchasing directly into the input chest; unused supplies are already settled there
      (DEV-MC-06-04).**
- [x] **S17** Store-hours **deferral wiring**: gate the town departure on `StoreHoursPolicy.OpensAt`
      against the shift clock — keep doing supply-independent field work until open; bounded by
      shift end (if it can't open in time → skip + notice). **Store-hours gating is enforced at
      purchase time; visible wait/travel feel is included in DEV-MC-06-03 playtest risk.**

### Tests (`Dayswork.Tests`)
- [x] **S18** FsCheck (full mode): affordable plan ≤ wallet; seed/fertilizer parity under any
      shortfall; planned-tile count deterministic; manifest grouping preferred-then-other and
      chest-supply-only never purchased; `StoreHoursPolicy` totality (incl. Pierre-Wednesday closed).
- [x] **S19** xUnit: `ShopStockSnapshot` price merge; `ShiftSupplyAggregator` aggregation across
      multiple zones/crops; affordability examples (exact/short/zero gold); store-hours examples;
      `PurchaseResult` → notice mapping.
- [x] **S20** xUnit: `PreferredCropStore` config round-trip + GMCM default `Either`; global preference
      overrides per-choice `StorePreference` at the planning seam.

### Verify + close
- [x] **S21** `dotnet build Dayswork.sln /p:EnableModDeploy=false` 0/0; `dotnet test Dayswork.sln` green.
- [x] **S22** `dotnet build Dayswork.sln` deploy to `Mods/Dayswork`; write
      `construction/u-mc-06-town-shopping/code/code-summary.md`; update `aidlc-state.md` + `audit.md`;
      present the **playtest checklist** and stop at the in-game playtest review gate.

### Playtest review fix — visible store trip
- [x] **RF1** Remove the up-front invisible input-chest purchase from managed-crop batch start.
- [x] **RF2** Let the farmhand finish pre-shopping managed-crop labor from current field/input-chest
      supply first, including harvest, dead-crop/debris clearing, tilling, fertilizing, planting,
      watering, and bounded re-plan passes.
- [x] **RF3** When the pre-shopping action queue drains, or when stamina is exhausted before that
      point, enter a visible shopping phase before normal wrap-up.
- [x] **RF4** Route through live Stardew location warps to Pierre/Joja, wait one tile right of the
      entrance when early, show the waiting HUD notice, and play the music-note emote while waiting.
- [x] **RF5** On store open, enter the shop, walk to the counter, buy the wallet-clamped manifest
      into carried stacks, and show a purchase-summary HUD notice.
- [x] **RF6** Route back to the farm, walk to the farmhand cabin input chest, deposit bought supplies,
      and show return/deposit HUD notices.
- [x] **RF7** After deposit, re-plan managed-crop actions from the updated input chest unless a
      boundary stop (exhausted/hard-cap/cancel) requires wrap-up.
- [x] **RF8** Verify the review fix with `dotnet build Dayswork.sln /p:EnableModDeploy=false` and
      `dotnet test Dayswork.sln /p:EnableModDeploy=false`; deploy-enabled builds now copy to
      `Mods/Dayswork` successfully.
- [x] **RF9** Playtest route failure follow-up: expand the visible shopping route graph to include
      map tile `Action Warp ...` transitions in addition to `GameLocation.warps`, and log the
      observed route locations/edges if a Farm→store path still cannot be found.
- [x] **RF10** Playtest route target follow-up: constrain Pierre/Joja store route resolution to the
      public `Town` exterior, log the selected entrance, and treat helper/private maps such as
      `Sunroom` as ignored diagnostics instead of shopping destinations.
- [x] **RF11** Playtest entrance-action follow-up: inspect the installed SVE `Town.tmx` map and parse
      `LockedDoorWarp <x> <y> <location> <open> <close>` actions such as
      `LockedDoorWarp 6 29 SeedShop 900 2100`, so public `Town` store entrances are recognized.
- [x] **RF12** Playtest counter/pricing/farm-exit follow-up: match only real shop counter actions
      (`Buy General` for Pierre and `OpenShop Joja` for Joja), read effective prices via
      `ShopBuilder.GetShopStock(...)` both for planning and at the counter, parse installed SVE
      `TouchAction LoadMap ...` route tiles, rank reachable source-location exits by walking cost,
      log selected route hops/counter tiles, and make the dev player tile-step logger info-level for
      preferred-exit diagnostics.
- [x] **RF13** Playtest post-return exit follow-up: when the configured cabin/exit tile is not
      reachable after the farmhand returns from shopping, choose a reachable passable nearby tile at
      `BeginExit` time, log the fallback, and finish the shift without a pathing warning.

## Story traceability
- **S-30** (autonomous seed/fertilizer purchasing) → S1–S22 and RF1–RF13.
- FR-MC-12 (S2/S6), FR-MC-13/14 (S4/S10), FR-MC-15/20 (S4/S7/S12/S14), FR-MC-16 (S14),
  FR-MC-17 (S12), FR-MC-18 (S10/S11/S16), FR-MC-19 (S13/S14), FR-MC-41 (S14 — time-only).

## Extension Compliance
- **Security Baseline**: N/A (disabled; local in-game gold deduction).
- **Property-Based Testing (full mode)**: S18 carries the blocking pure properties; live
  reader/transaction/navigation (S10/S11/S12/S14) are example/playtest-covered adapters
  (S19/S20 + manual playtest at S22).
