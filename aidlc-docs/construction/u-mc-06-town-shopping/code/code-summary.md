# Code Summary — U-MC-06 Town Shopping

**Unit**: U-MC-06 — Town Shopping  
**Stage**: CONSTRUCTION — Code Generation  
**Status**: Complete; review required (in-game playtest)

## Summary

U-MC-06 turns the Manage Crops supply-planning seams into a visible live shopping path for
open-farm managed crop shifts. The farmhand now performs pre-shopping crop labor from the current
field/input-chest state, reads live Pierre/Joja stock and prices, builds a purchase manifest from
the remaining planned plantable tiles, clamps purchases to the player's wallet, walks to the store,
waits outside if early, buys at the counter, returns to the farm, deposits bought supplies into the
input chest, and re-plans the managed-crop planting loop from the updated supply.

The gold/item safety barrier is intentionally conservative: items are created before gold is
deducted, bought stacks are carried by the farmhand until the return leg, and sleep/route-failure
paths settle carried stacks into the input chest or overflow. Shop data, route, or item-bind failure
skips the trip with a HUD notice and leaves the farmhand working from supplies already on hand.

## Created Files

**Core (`Dayswork.Core`)**
- `Crops/PlannedPlantableTileCounter.cs` — pure planned-tile sizing for purchase quantities.
- `Crops/ShiftPurchaseManifest.cs` — priced manifest records grouped by store.
- `Crops/AffordablePurchasePlan.cs` — wallet-clamped purchase plan.
- `Crops/PurchaseAffordabilityCalculator.cs` — pure affordability clamp with seed/fertilizer parity.
- `Crops/PurchaseResult.cs` — per-line transaction outcomes.
- `Crops/ShiftSupplyAggregator.cs` — whole-shift manifest aggregation from crop plan, field, chest,
  global preference, and live stock.
- `Crops/StoreHoursPolicy.cs` — pure Pierre/Joja open-hours policy.

**Mod (`Dayswork`)**
- `Integration/ShopStockReader.cs` — guarded live `ShopBuilder`/`Data/Shops` stock/price reader.
- `Integration/ShopPurchaseService.cs` — exchange-atomic input-chest and carried-item purchase boundary
  with authoritative live unit-price lookup at the counter.
- `Orchestration/ShiftOrchestrator.ManagedCropShopping.cs` — visible store-trip state machine for
  route, wait, counter purchase, return, input-chest deposit, and post-shopping re-plan.

**Tests (`Dayswork.Tests`)**
- `ManageCrops/TownShoppingPropertyTests.cs` — FsCheck properties for wallet cap, seed/fertilizer
  parity, planned-tile determinism, and store-hours totality.
- `ManageCrops/TownShoppingTests.cs` — example coverage for price snapshots, manifest aggregation,
  affordability edge cases, and store hours.

## Modified Files

- `Dayswork.Core/Crops/ShopStockSnapshot.cs` — added unit prices and `UnitPriceOf`.
- `Dayswork.Core/Crops/StoreResolver.cs` — carries prices through merged store snapshots.
- `Dayswork.Core/Crops/CropShiftPlanner.cs` — accepts a global store-preference override.
- `Dayswork/Integration/ModConfig.cs` — added `PreferredCropStore` (`Either`/`Pierre`/`Joja`).
- `Dayswork/Integration/RuntimeConfigSnapshotMapper.cs` — normalizes invalid store preference to
  `Either`.
- `Dayswork/Integration/GMCMRegistrar.cs` and `IGenericModConfigMenuApi.cs` — added the GMCM text
  option for preferred crop store.
- `Dayswork/Integration/ShopStockReader.cs` — can include stock for stores that are closed now but
  still open later today, allowing pre-open trips to wait outside; it now prefers
  `ShopBuilder.GetShopStock(...)` so snapshots use effective player-facing prices before
  `Data/Shops` fallback.
- `Dayswork/Integration/ShopPurchaseService.cs` — re-reads the authoritative live unit price at the
  counter and refuses unpriced purchases instead of granting free supplies.
- `Dayswork/Integration/CropHudNotifier.cs` and `Dayswork/i18n/default.json` — shopping purchase,
  fallback, festival, insufficient-funds, unavailable, departure, wait, return, summary, and deposit
  notices.
- `Dayswork/ModEntry.cs` — exposes the live global store preference for shift runtime use.
- `Dayswork/Orchestration/ShiftOrchestrator.ManagedCrops.cs` — removes the up-front invisible
  purchase, runs pre-shopping crop actions first, and redirects stamina-boundary wrap-up through
  shopping when needed.
- `Dayswork/Orchestration/ShiftOrchestrator.Movement.cs` — dispatches movement arrivals/failures to
  the shopping state machine while a trip is active.
- `Dayswork/Orchestration/ShiftOrchestrator.cs` — resets the live stock cache per shift, suppresses
  stuck sampling while intentionally waiting/shopping, settles carried purchases on sleep, and tracks
  the active reachable end-of-shift exit tile.
- `Dayswork/Orchestration/ShiftOrchestrator.ManagedCropShopping.cs` — route graph now reads
  `GameLocation.warps`, map tile `Action`/`TouchAction` `Warp`/`LoadMap` transitions, and
  `LockedDoorWarp` store entrances. Store entrance resolution is constrained to the public `Town`
  exterior for Pierre/Joja, logs selected route hops/entrance/counter tiles, and reports ignored
  helper-map candidates when a public store route cannot be found.
- `Dayswork/Diagnostics/PlayerTileStepLogger.cs` — dev player tile-step logging now writes at
  info level after `dayswork_debug_player_tile on`, making preferred-exit playtest logs easier to
  capture.
- `Dayswork.Tests/Generators/ManageCropsGen.cs` — added domain generators for U-MC-06 PBT.
- `Dayswork.Tests/Config/RuntimeConfigSnapshotMapperTests.cs` — preferred-store normalization tests.

## Behavior Delivered

- Global `PreferredCropStore` applies to every managed crop group and overrides per-choice store
  preference at the planning seam (DEV-MC-06-01).
- Live stock snapshots include both quantity and price; failed shop reads produce safe empty/closed
  snapshots instead of crashes.
- Whole-shift manifests aggregate demand across zones after subtracting input-chest supply and the
  work already completed before the shopping phase.
- Wallet clamping never spends more than available gold and preserves seed/fertilizer parity under
  shortfall.
- Purchases are item-safe: gold is deducted only after item creation succeeds.
- The farmhand visibly routes through live Stardew warps to Pierre/Joja, waits one tile right of the
  entrance with a music-note emote if early, enters at opening time, walks to the counter, buys the
  affordable manifest, then walks back to the farmhand cabin input chest.
- Purchased supplies are deposited into the input chest before the post-shopping managed-crop
  re-plan, so the existing tile-by-tile loop consumes them normally.
- If stamina runs out before the shopping phase, the farmhand stops further labor, shops energy-free,
  deposits bought supplies, then wraps up through the normal deposit/exit path.
- Festival days skip shopping with a HUD notice; bind/read failures show a shopping-unavailable
  notice and continue from on-hand chest supply (DEV-MC-06-02).

## Review Fix Notes

- **DEV-MC-06-03 resolved in review fix.** The farmhand now makes a visible store trip after
  pre-shopping labor instead of running the transaction invisibly at batch start.
- **DEV-MC-06-04 resolved in review fix.** Bought items are carried during the return trip and
  deposited into the input chest; carried stacks are also settled safely if sleep/route failure
  interrupts the trip.
- **Playtest follow-up 2026-06-06 — route graph widened.** The first visible-route playtest aborted
  with `store_route_path_unavailable` after announcing shopping. The route graph now includes
  `Action Warp ...` tile transitions as well as `GameLocation.warps`, which covers Stardew map exits
  that are not represented in the `warps` list. If the route still cannot connect, the devlog now
  prints the discovered locations and route edges.
- **Playtest follow-up 2026-06-06 — public store exterior enforced.** The widened route graph exposed
  an invalid route target where the resolver selected `Sunroom` as the store exterior. Store routes
  now resolve Pierre/Joja through the public `Town` exterior only, log `store route selected` with the
  entrance/wait/arrival tiles, and list non-public shop-like candidates as ignored diagnostics.
- **Playtest follow-up 2026-06-06 — installed SVE `LockedDoorWarp` parsed.** The installed SVE
  `assets/Maps/Locations/Town.tmx` uses `LockedDoorWarp 6 29 SeedShop 900 2100` for Pierre's door
  and `LockedDoorWarp 13/14 29 JojaMart 1000 2200` for Joja. The shopping route parser now treats
  `LockedDoorWarp` as a navigable entrance action and prefers the rightmost duplicate door tile so
  the wait tile lands to the right of the doorway.
- **Playtest follow-up 2026-06-07 — exact counters and effective prices.** The store-counter scan no
  longer accepts message/dropbox tiles that merely contain `Shop`, `SeedShop`, or `Pierre`; Pierre
  matches the real `Buy General` action and Joja matches `OpenShop Joja`. Planning and counter
  transactions now prefer `ShopBuilder.GetShopStock(...)` prices, so seeds like Blueberry Seeds
  resolve to the same effective price the player pays instead of falling through to zero.
- **Playtest follow-up 2026-06-07 — Grandpa's Farm route diagnostics.** The local SVE Grandpa's Farm
  source shows normal map `Warp` exits plus patch-provided `TouchAction LoadMap ...` exits. The
  shopping route graph now parses `TouchAction LoadMap`, ranks reachable first-hop exits by walking
  distance from the farmhand, logs the selected route hops, and exposes info-level
  `dayswork_debug_player_tile on` logs so a preferred exit can be captured precisely during playtest.
- **Playtest follow-up 2026-06-07 — post-return farm exit fallback.** After the farmhand returns
  from Pierre's/Joja and deposits/replans, the configured cabin/exit tile can be passable but not
  reachable on Grandpa's Farm. `BeginExit` now resolves the actual end-of-shift navigation tile from
  the worker's current position: it uses the configured tile when reachable, otherwise chooses a
  reachable passable tile nearby and logs the fallback.
- The music-note emote uses `EmoteMusic = 16`; confirm the visual during playtest and adjust the
  constant if Stardew maps the music note differently.

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` — 0 warnings / 0 errors.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` — 473 passed / 1 expected skip / 0 failed.
- `dotnet build Dayswork.sln` — deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.

## Extension Compliance

- **Security Baseline**: skipped / N/A because disabled for Manage Crops.
- **Property-Based Testing (full mode)**: compliant. PBT-03, PBT-07, PBT-08, and PBT-10 are covered
  by the new domain generators and FsCheck properties alongside example tests. PBT-02 and PBT-04 are
  N/A for this unit's generated code because no new reversible transform or idempotent transform was
  introduced. PBT-05 and PBT-06 are N/A because the pure shopping logic has no separate oracle or
  stateful model; live adapter behavior is example/playtest-covered. The visible route state machine
  is SMAPI adapter behavior and requires the manual playtest below.

## Playtest Checklist

1. Close the game/SMAPI process, run `dotnet build Dayswork.sln`, and confirm the mod deploys before
   in-game verification.
2. Configure a managed crop group with no seeds in the input chest, set `Preferred crop store` to
   `Pierre`, keep enough gold, start a shift on an open non-Wednesday day, and confirm the farmhand
   does pre-shopping field labor, walks to Pierre's, buys supplies, returns, deposits them, and plants.
3. Start before 9:00 AM after a season transition with dead crops present; confirm the farmhand clears
   the dead crops, walks to the store, waits one tile right of the entrance with the waiting HUD notice
   and music-note emote, then enters when the store opens.
4. Repeat with `Preferred crop store = Joja`; confirm Joja-priced stock is used when available.
5. Put some seeds in the input chest before the shift; confirm the farmhand plants/uses that supply
   first, then buys only the remaining shortfall.
6. Use too little gold for all needed seed/fertilizer pairs; confirm the HUD insufficient-funds
   notice appears and planted tiles never receive unmatched seed/fertilizer.
7. Start on a festival day; confirm shopping is skipped and the farmhand plants only from input-chest
   supply.
8. Fill the input chest nearly full, then trigger purchases; confirm overflow is preserved and no
   bought stacks disappear.
9. Force exhaustion before shopping by lowering stamina; confirm the farmhand enters the shopping
   phase before returning to the cabin.
10. Confirm no regression for U-MC-05: existing input-chest-only planting still works when no purchase
    is needed.
11. On Grandpa's Farm, run `dayswork_debug_player_tile on`, walk the player to the preferred farm
    exit, then start the shopping shift and compare the player tile-step log with the
    `[managed-crops][shopping] route selected ... hops=[...]` line.
12. After the farmhand returns from shopping and deposits supplies, confirm there is no final
    `[Dayswork][exit] could not path to exit tile ...` warning. If the configured cabin/exit tile is
    isolated, expect an info log like `[Dayswork][exit] configured exit tile ... unreachable ...
    using reachable nearby tile ...`.
