# U-MC-06 Tech Stack Decisions

**Unit**: U-MC-06 — Town Shopping · **Stage**: CONSTRUCTION — NFR Requirements

No new technology is introduced. U-MC-06 reuses the established stack and seams; the only
"new" capability is using **existing SMAPI 1.6 shop APIs** headlessly and adding **new route
data** to the existing navigation layer.

| Concern | Decision | Rationale |
|---|---|---|
| Language / runtime | C# / .NET 6, SMAPI mod | Project-wide standard; unchanged. |
| Pure decision logic | `Dayswork.Core` (no SMAPI deps) | Determinism + PBT (NFR-MC6-01/08); reuses U-MC-01 `StoreResolver`/`CropSupplyPlanner`. |
| Live shop stock + prices | **`Data/Shops` via `ShopBuilder`** (e.g. `ShopBuilder.GetShopDataForCustomer` / `Utility` shop-stock helpers), read **headlessly** — no `ShopMenu` opened | FR-MC-18: honours mod-added seeds, price mods, seasonal gating, Joja markup; exact "store stocks it" rule. Read once per store per shift (NFR-MC6-02). |
| Gold mutation | `Farmer.Money` deduction on grant only | Item/gold safety (NFR-MC6-03); deduct exactly for granted items. |
| Item grant | Add purchased items to the worker's carried inventory; leftovers → input chest | Reuses U-MC-05 carried-supply-return seam; overflow-to-mail on full chest. |
| Town navigation | **New route definitions** (Farm↔SeedShop, Farm↔JojaMart) on the existing `CrossLocationRouteNavigator` / `BuildingWorkNavigator` | FR-MC-17: analogous to existing building/SVE-shed routes; failure → skip (DEV-MC-06-02). |
| Store hours | Pure `StoreHoursPolicy` (no live calendar dependency beyond time/weekday) | Deterministic gating for deferral/fallback (NFR-MC6-01). |
| Pacing | Existing `WorkerActionAnimationMs` / `WorkerPacingProfile` | FR-MC-19: one line per beat; no new pacing knob. |
| Config | One new GMCM key `PreferredCropStore` (Pierre/Joja/Either, default Either) in `ModConfig` | DEV-MC-06-01 global preference; config.json, not the save (NFR-MC6-06). |
| HUD / i18n | `Game1.addHUDMessage` via `CropHudNotifier` (M-29); strings in `i18n/default.json` | NFR-MC6-07; lint-gated. |
| Persistence | **No schema change** (reads existing V3 `CropPlan`) | NFR-MC6-06. |
| Testing | xUnit + FsCheck (`FsCheck.Xunit`, already present) | NFR-MC6-08; PBT full mode for pure seams, examples for adapters. |

## Explicitly NOT introduced
- No HTTP/network client, payment SDK, auth, or external service (the "transaction" is local
  in-game gold) → Security Baseline remains N/A.
- No async/job framework — the trip runs on the existing synchronous tick/intent state machine.
- No new save schema, no new persistence store, no new UI framework (GMCM dropdown + HUD only).
- No new third-party dependency.
