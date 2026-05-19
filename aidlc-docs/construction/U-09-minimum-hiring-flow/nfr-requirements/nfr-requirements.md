# NFR Requirements — U-09 Minimum Hiring Flow

## Unit
U-09 — Minimum Hiring Flow (HiringFlowCoordinator, TaskSelectionMenu, SummaryMenu, ContractPersistenceAdapter)

**Depth**: Minimal — all NFRs derived from approved requirements.md; no clarifying questions needed.

---

## Applicable NFRs

### NFR-SAFE-03 — No Save Corruption
**Requirement**: The mod must not corrupt save files. All persisted data is namespaced via SMAPI's data API and tolerates being absent on first load.

**How it applies to U-09**:
- `ContractPersistenceAdapter.OnSaving` calls `Helper.Data.WriteSaveData("Dayswork.Contracts", serialized)` where `serialized` is a `DaysworkSaveDataV1` produced by `ISaveDataSerializer` (C-13, established in U-06).
- `ContractPersistenceAdapter.OnSaveLoaded` calls `Helper.Data.ReadSaveData<DaysworkSaveDataV1>("Dayswork.Contracts")` — null return (first load or missing segment) is treated as an empty contract list, no crash.
- SMAPI's data API writes to a separate `<save>.json` file in the mod's data folder, not directly to the vanilla save — corruption of the vanilla save is structurally impossible.
- The `ISaveDataSerializer.Deserialize(null)` path (established in U-06) explicitly returns an empty list, satisfying the "tolerates absent" requirement.

**Enforcement**: Code Generation step must call Deserialize on the ReadSaveData result, not access it directly. Null guard is in the Core serializer, not scattered across callers.

---

### NFR-SAFE-02 — No Gold Loss
**Requirement**: No gold is ever lost beyond the contractually-billed hourly rate × hours worked. Refunds are integer-clamped to avoid floating-point gold leakage.

**How it applies to U-09**:
- Deposit deduction in SummaryMenu confirm: `Game1.player.Money -= deposit` where `deposit` is the integer output of `IDepositCalculator.ComputeDeposit()` (already integer-safe per U-05 design).
- **Afford-check before deduction**: SummaryMenu must verify `Game1.player.Money >= deposit` before calling deduct. If insufficient, show the `ui.error.cant_afford` message and block confirmation (FR-HIRE-14). Never deduct then refund.
- `Game1.player.Money` is an `int` in SMAPI 4.x; the subtraction is exact integer arithmetic.

**Enforcement**: Code Generation must place the afford-check before the `Money -=` call. The check is inline in SummaryMenu's confirm handler — no separate validator class needed.

---

### NFR-PERF-01 — Frame Budget for draw()
**Requirement**: The worker's (and by extension, all per-frame hooks') update must not introduce visible frame drops. Stardew targets 60fps.

**How it applies to U-09**:
- `TaskSelectionMenu.draw(SpriteBatch b)` and `SummaryMenu.draw(SpriteBatch b)` run every frame while the menu is open.
- **Rule**: No computation inside `draw()`. Rate, deposit, and hours estimate are pre-computed when state changes (task toggle, menu open) and stored as fields. `draw()` only reads fields and renders sprites/text.
- Task toggle handler: `IRateCalculator.ComputeHourlyRate()` is called once per toggle, result cached in `_currentRate`. This is O(n) for n ≤ 10 tasks — negligible.
- SummaryMenu construction: `IHoursEstimator.EstimateHours()` is called once when the menu is opened (not in draw). Result cached as `_estimatedHours`. (See NFR-PERF-02.)

**Enforcement**: draw() methods must contain only `b.Draw(...)` and `b.DrawString(...)` calls reading from pre-computed fields. No `IRateCalculator` or `IHoursEstimator` calls in draw().

---

### NFR-PERF-02 — Single Tile-Scan Per Menu Open
**Requirement**: Tile scanning to build the task queue happens once at zone entry per shift, not per frame.

**How it applies to U-09**:
- `IHoursEstimator.EstimateHours()` requires a passability oracle over the farm map to count reachable tiles in the chosen zones.
- In U-09's thin slice, the "zone" defaults to the whole farm (no zone drawing yet). The call is made **once** when SummaryMenu is constructed (not per frame, not per task toggle).
- The passability oracle is `(TileCoord coord) => Game1.getFarm().isTilePassable(new Location(coord.X, coord.Y), Viewport.Empty)` — a per-tile call into Stardew. Scanning the full Standard Farm (~80×65 = 5 200 tiles) with this oracle takes < 5ms (negligible, one-time).
- Result `_estimatedHours` is stored on the SummaryMenu instance and used in all subsequent draw() calls.

**Enforcement**: `EstimateHours()` must not be called from `draw()` or from any UpdateTicked hook. Call site is SummaryMenu's constructor or `OnOpen` method only.

---

### NFR-UX-01 — Full Gamepad Navigation
**Requirement**: The hiring UI is fully navigable with mouse/keyboard and gamepad (FR-HIRE-03, Q24).

**How it applies to U-09**:
- Both `TaskSelectionMenu` and `SummaryMenu` must override `receiveGamePadButton(Buttons b)`.
- **Focus management**: Each menu must override `setCurrentlySnappedComponentTo(int id)` and `populateClickableComponentList()` so SMAPI can snap the cursor to the correct button on controller.
- **Standard SMAPI bindings**: `Buttons.B` → back/cancel (return to previous menu or close); `Buttons.A` → confirm/click current component; D-pad → move between clickable components.
- `HiringFlowCoordinator.OpenHiringFlow()` pushes `TaskSelectionMenu` onto `Game1.activeClickableMenu`; the menu's `exitFunction` (or a coordinator callback) is used to advance to SummaryMenu on confirm, close on cancel.
- Gamepad focus on menu open: first task toggle button in TaskSelectionMenu; confirm button in SummaryMenu.

**Enforcement**: Play-test with a controller before marking U-09 Definition of Done. All interactive elements must be reachable without mouse.

---

### NFR-UX-02 — i18n Routing
**Requirement**: All user-visible strings are routed through SMAPI's i18n system.

**How it applies to U-09**:
- All text rendered in `TaskSelectionMenu.draw()` and `SummaryMenu.draw()` must come from `I18nHelper.Get(key)`.
- New i18n keys added in this unit (added to `i18n/default.json`):
  - `ui.task_selection.title` — "Hire a Farmhand"
  - `ui.task_selection.water_crops` — "Water Crops"
  - `ui.task_selection.harvest_crops` — "Harvest Crops"
  - `ui.task_selection.collect_fruit` — "Collect Fruit"
  - `ui.task_selection.feed_animals` — "Feed Animals"
  - `ui.task_selection.pet_animals` — "Pet Animals"
  - `ui.task_selection.collect_animal_products` — "Collect Animal Products"
  - `ui.task_selection.cut_trees` — "Cut Trees"
  - `ui.task_selection.clear_rocks` — "Clear Rocks"
  - `ui.task_selection.clear_weeds` — "Clear Weeds"
  - `ui.task_selection.clear_grass` — "Clear Grass"
  - `ui.task_selection.rate_label` — "Hourly rate: {rate}g"
  - `ui.task_selection.confirm_btn` — "Next"
  - `ui.task_selection.cancel_btn` — "Cancel"
  - `ui.summary.title` — "Confirm Hiring"
  - `ui.summary.tasks_label` — "Tasks"
  - `ui.summary.hours_label` — "Est. hours: {hours}"
  - `ui.summary.rate_label` — "Rate: {rate}g/hr"
  - `ui.summary.deposit_label` — "Deposit: {deposit}g"
  - `ui.summary.refund_policy` — "Unused deposit refunded at shift end."
  - `ui.summary.confirm_btn` — "Confirm"
  - `ui.summary.back_btn` — "Back"
  - `ui.error.cant_afford` — "You can't afford this contract."
- The `{rate}`, `{hours}`, `{deposit}` tokens use SMAPI's built-in Translation substitution: `I18nHelper.Get("ui.summary.hours_label", new { hours = estimatedHours })`.

**Enforcement**: No hardcoded user-visible strings in any Mod-layer file. The U-16 i18n lint test will catch regressions.

---

### NFR-MAINT-03 — SMAPI Integration Separation
**Requirement**: Pure business-logic modules (rate calculation, deposit/refund math, tile-zone intersection, save-data DTO round-trips) are separated from SMAPI/game-engine integration so they can be unit-tested without launching Stardew.

**How it applies to U-09**:
- `TaskSelectionMenu` and `SummaryMenu` depend on Core interfaces (`IRateCalculator`, `IDepositCalculator`, `IHoursEstimator`) injected via constructor. No `new RateCalculator()` calls inside menus.
- `ContractPersistenceAdapter` depends on `IContractStore` (C-12) and `ISaveDataSerializer` (C-13) injected via constructor.
- `HiringFlowCoordinator` receives the menus' factory functions or the Core services from ModEntry via constructor injection.
- `Dayswork.Core` has zero references to `StardewValley.*` or `StardewModdingAPI.*` — verified by the project reference rules in [component-dependency.md](../../../inception/application-design/component-dependency.md).

**Enforcement**: Verified by `Dayswork.Core.csproj` project references (no SMAPI/SV references). Any Core violation would cause a build error.

---

### NFR-ONBOARD-01 — Just-In-Time Docs
**Requirement**: C# / SMAPI / Harmony concepts are explained just-in-time during Construction stages, embedded in Code Generation plans.

**How it applies to U-09**:
The Code Generation plan for U-09 must include brief explanations of:
1. **IClickableMenu anatomy**: What `draw(SpriteBatch b)`, `receiveLeftClick(int x, int y, bool playSound)`, `receiveGamePadButton(Buttons b)`, `populateClickableComponentList()`, and `setCurrentlySnappedComponentTo(int id)` do; why all must be overridden for gamepad support.
2. **ClickableComponent and ClickableTextureComponent**: The two types used for buttons and toggles in SMAPI menus; how `myRegion` and `myID` / `rightNeighborID` / `downNeighborID` wire up the controller D-pad navigation graph.
3. **SMAPI Data API**: `Helper.Data.WriteSaveData<T>(key, value)` writes to `<save_folder>/Mods/<modId>/<key>.json`; `ReadSaveData<T>(key)` returns null if absent; both are safe to call in `Saving` / `SaveLoaded` events.
4. **Game1.player.Money**: Direct `int` field; thread-safe (Stardew is single-threaded on the update loop); simple `+=` / `-=` for gold transfers.
5. **Menu push pattern**: `Game1.activeClickableMenu = newMenu` replaces the active menu; `exitFunction` delegate is called when the menu closes; the coordinator uses this to chain screens.

These are embedded in the Code Generation plan step comments, not as separate doc files.

---

## N/A NFRs

| NFR | Rationale |
|---|---|
| NFR-SAFE-01 | No items collected or moved in this unit |
| NFR-SAFE-04 | No item pickup by worker |
| NFR-PERF-03 | Zone overlay is U-11 |
| NFR-UX-03 | Zone-in-board drawing is U-11 |
| NFR-MAINT-01 | xUnit project established in U-02 |
| NFR-MAINT-04 | No new Harmony patches |
| NFR-MAINT-05 | `dotnet format` applies always; no design decisions |
| NFR-COMPAT-01 | Compatibility docs — README concern |
| NFR-COMPAT-02 | Farm-type support — runtime concern in U-10+ |
| NFR-COMPAT-03 | Multiplayer guard established in U-08 |
| NFR-COMPAT-04 | Required deps already wired (Harmony U-01; MFM U-14; GMCM U-16) |
| Security Baseline | Disabled project-wide (Q28) |

---

## PBT Extension Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-02 (round-trip) | N/A | No new serialized types; ContractDraft is UI-only and not persisted |
| PBT-03 (invariants) | N/A | No new domain invariants; pricing invariants covered in U-05 |
| PBT-07 (generator quality) | N/A | No new FsCheck generators needed |
| PBT-08 (shrinking/seed logging) | N/A | No PBT tests in this unit |
| PBT-09 (framework = FsCheck) | Already decided | No new framework decision |

**Note**: U-09 components (TaskSelectionMenu, SummaryMenu, ContractPersistenceAdapter) require a running Stardew instance to test — they are play-tested per the unit Definition of Done. The Core pricing and persistence contracts tested in U-05/U-06 cover the math invoked by these menus.
