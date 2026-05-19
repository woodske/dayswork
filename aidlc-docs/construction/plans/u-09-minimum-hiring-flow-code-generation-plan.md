# U-09 Code Generation Plan — Minimum Hiring Flow

## Unit
U-09 — Minimum Hiring Flow

## Stories Implemented
- **S-02** (full) — Task toggles + live hourly rate + gamepad navigation
- **S-05** (partial) — One-time contracts persist through save/load
- **S-06** (full) — Summary screen + confirm + insufficient-gold block

## Dependencies Satisfied
- U-03 Config Foundation: `IConfigSnapshot`, `ConfigDefaults`
- U-05 Pricing Core: `IRateCalculator`, `IDepositCalculator`, `IHoursEstimator`
- U-06 Persistence Core: `IContractStore`, `ISaveDataSerializer`, `Contract`, `DaysworkSaveDataV1`
- U-08 Bulletin Board Hook: `BulletinBoardPatch` (extended), `I18nHelper`, `ModEntry`

## Code Locations (all application code at workspace root — never aidlc-docs/)

### New files (5)
| Step | File | Purpose |
|---|---|---|
| 1 | `Dayswork/UI/ContractDraft.cs` | Mutable UI-only state accumulating player choices across screens |
| 2 | `Dayswork/UI/HiringFlowCoordinator.cs` | Owns all screen transitions; composition root for the hiring flow |
| 3 | `Dayswork/UI/TaskSelectionMenu.cs` | Screen 1 — 10 task toggles + live rate (M-04) |
| 4 | `Dayswork/UI/SummaryMenu.cs` | Screen 4 thin — estimated hours, rate, deposit, confirm (M-07) |
| 5 | `Dayswork/Integration/ContractPersistenceAdapter.cs` | Bridges SMAPI data API ↔ IContractStore (M-15) |

### Modified files (4)
| Step | File | Change |
|---|---|---|
| 6 | `Dayswork.Core/Persistence/ContractStore.cs` | Implement `ListActiveForDate` (currently throws `NotImplementedException`) |
| 7 | `Dayswork/Patches/BulletinBoardPatch.cs` | Replace placeholder log with `HiringFlowCoordinator.OpenHiringFlow()` |
| 8 | `Dayswork/ModEntry.cs` | Wire Core singletons + HiringFlowCoordinator + ContractPersistenceAdapter + events |
| 9 | `Dayswork/i18n/default.json` | Add 22 new UI keys |

### Build + docs (3)
| Step | Action |
|---|---|
| 10 | `dotnet build` — verify 0 errors, 0 warnings |
| 11 | Create code summary doc (`aidlc-docs/construction/U-09-minimum-hiring-flow/code/u-09-code-summary.md`) |
| 12 | Update `aidlc-state.md` + `audit.md` |

---

## Onboarding Notes (embedded for reference during generation)

### IClickableMenu anatomy (NFR-ONBOARD-01)
- `draw(SpriteBatch b)` — called every frame; renders the whole menu. Never compute values here — read cached fields only.
- `receiveLeftClick(int x, int y, bool playSound)` — called once per mouse click.
- `receiveGamePadButton(Buttons b)` — called once per controller button press. `Buttons.B` = back/cancel, `Buttons.A` = confirm/click.
- `populateClickableComponentList()` — called by SMAPI on menu open; fills `allClickableComponents` with every interactive element so D-pad navigation works.
- `setCurrentlySnappedComponentTo(int id)` — called by SMAPI when controller focus moves; set `currentlySnappedComponent` and call `snapCursorToCurrentSnappedComponent()`.
- Call `base.receiveGamePadButton(b)` after your own handling — the base class handles D-pad movement between components using `allClickableComponents` and neighbor IDs.
- `exitFunction` delegate fires when the menu is closed by any means (Escape, B button, close button). Set this to clean up if needed.

### ClickableComponent neighbor IDs
- `myID` — unique integer identifying this component.
- `upNeighborID`, `downNeighborID`, `leftNeighborID`, `rightNeighborID` — IDs of adjacent components for D-pad navigation. Use `SNAP_AUTOMATIC` (-1) or `SNAP_TO_DEFAULT` (-2) for automatic snapping when a specific neighbor isn't needed.

### Screen push pattern (T9-02)
- `Game1.activeClickableMenu = newMenu` replaces the currently open menu.
- Set the old menu's `exitFunction` if you need to react to the player closing with Escape/B — but in our flow the coordinator always sets the new menu explicitly via callbacks, so `exitFunction` is primarily used for "cancel entire flow" on the first screen.

### SMAPI Data API (T9-04)
- `helper.Data.WriteSaveData<T>(string key, T value)` — writes `value` as JSON alongside the current save file. Key is namespaced automatically to this mod.
- `helper.Data.ReadSaveData<T>(string key)` — returns `T?`; returns `null` if the key has never been written (e.g., first load).
- Both are safe to call in `GameLoop.Saving` / `GameLoop.SaveLoaded` events.

### Game1.player.Money
- Direct `int` field on `Farmer`. Stardew is single-threaded on the update loop. `Game1.player.Money -= amount` is safe, exact integer subtraction.

---

## Step Details

### Step 1 — Create `Dayswork/UI/ContractDraft.cs`  [x]
Mutable class holding the player's in-progress hiring choices. Lives in `Dayswork.UI` namespace. Not a Core type — never serialized. Properties:
- `HashSet<TaskKind> EnabledTasks` — tasks the player has toggled on
- `List<Zone> Zones` — empty in U-09 (whole-farm default applied by coordinator); populated in U-11
- `Dictionary<TaskKind, DestinationKey> Destinations` — empty in U-09 (shipping-bin default); populated in U-11
- `ContractSchedule Schedule` — defaults to `ContractSchedule.OneTime`; selectable in U-12

Using directives: `Dayswork.Core.Domain`

---

### Step 2 — Create `Dayswork/UI/HiringFlowCoordinator.cs`  [x]
The sole owner of screen transitions. Holds references to Core singletons (injected via constructor). Key members:

**Constructor**: `HiringFlowCoordinator(IRateCalculator rateCalc, IDepositCalculator depositCalc, IHoursEstimator hoursEst, IConfigSnapshot config, IContractStore contractStore)`

**Public methods**:
- `OpenHiringFlow()` — creates a fresh `ContractDraft`, creates `TaskSelectionMenu`, sets `Game1.activeClickableMenu`
- `OpenEditFlow(ContractId existing)` — (stub body: logs "Edit flow not yet implemented" and returns; full impl in U-12)

**Private helpers** (all set `Game1.activeClickableMenu`):
- `ShowSummary(ContractDraft draft)` — creates `SummaryMenu`
- `BackToTaskSelection(ContractDraft draft)` — recreates `TaskSelectionMenu` with existing draft (preserves toggle state)
- `ConfirmContract(ContractDraft draft, int deposit)` — afford-check → deduct → build Contract → store.Add → CloseFlow
- `CloseFlow()` — `Game1.activeClickableMenu = null`

**Whole-farm default zone**:
```csharp
private static readonly Zone WholeFarmZone =
    new("Farm", new TileCoord(0, 0), new TileCoord(79, 63));
```
Used when `draft.Zones` is empty (applies to entire U-09 thin slice; replaced by actual drawn zones in U-11).

**BuildContract helper**:
```csharp
private Contract BuildContract(ContractDraft draft, int deposit, int rate)
{
    var zones = draft.Zones.Count > 0
        ? (IReadOnlyList<Zone>)draft.Zones.AsReadOnly()
        : new[] { WholeFarmZone };
    var destinations = draft.Destinations.Count > 0
        ? (IReadOnlyDictionary<TaskKind, DestinationKey>)draft.Destinations
        : new Dictionary<TaskKind, DestinationKey>();
    return new Contract(
        Id: ContractId.New(),
        EnabledTasks: draft.EnabledTasks.ToHashSet(),
        Zones: zones,
        TaskDestinations: destinations,
        Schedule: draft.Schedule,
        Status: ContractStatus.Active,
        HireDate: new GameDate(Game1.Date.DayOfMonth, MapSeason(Game1.currentSeason), Game1.year),
        DepositAmount: deposit,
        HourlyRate: rate);
}
```
Season mapping: `string → Season` via `Game1.currentSeason` (a lowercase string like `"spring"`) converted via `Enum.Parse<Season>(Game1.currentSeason, ignoreCase: true)`.

Using directives: `Dayswork.Core.Config`, `Dayswork.Core.Domain`, `Dayswork.Core.Persistence`, `Dayswork.Core.Pricing`, `Dayswork.Integration`, `Microsoft.Xna.Framework`, `StardewValley`, `StardewValley.Menus`

---

### Step 3 — Create `Dayswork/UI/TaskSelectionMenu.cs`  [x]
**Extends** `IClickableMenu`. Screen 1 of the hiring flow.

**Layout** (centered on screen):
- Menu size: 700 wide × 700 tall
- Title: top-center
- 10 task toggle rows (each 56px tall, starting at y+80): toggle = `ClickableComponent` (ID 100–109)
- Rate label: below toggles
- "Next" button (ID 200): bottom-right; "Cancel" button (ID 201): bottom-left

**Component IDs** and neighbor graph:
- Toggle i: `myID = 100 + i`; `downNeighborID = i < 9 ? 101 + i : 200`; `upNeighborID = i > 0 ? 99 + i : SNAP_AUTOMATIC`
- Next: `upNeighborID = 109`; `leftNeighborID = 201`
- Cancel: `upNeighborID = 109`; `rightNeighborID = 200`

**Task → i18n key mapping**: static `Dictionary<TaskKind, string>` mapping each TaskKind to its `ui.task_selection.*` key.

**Core calls** (never in draw):
- `_currentRate = _rateCalc.Calculate(draft.EnabledTasks, config, isRaining: false)` — in constructor + on each toggle

**draw()** reads only `_currentRate`, `_draft.EnabledTasks`, and component bounds.
- `drawTextureBox` for the menu frame and each toggle row background (light green if enabled, light gray if disabled)
- `Utility.drawTextWithShadow` for labels and rate text
- `drawMouse(b)` last

**receiveLeftClick**: check toggle bounds → toggle task; check Next/Cancel bounds → invoke callback.

**receiveGamePadButton**: B → cancel; A → toggle/advance/cancel based on `currentlySnappedComponent.myID`; base call for D-pad.

Using directives: `Dayswork.Core.Config`, `Dayswork.Core.Domain`, `Dayswork.Core.Pricing`, `Dayswork.Integration`, `Microsoft.Xna.Framework`, `Microsoft.Xna.Framework.Graphics`, `StardewValley`, `StardewValley.Menus`

---

### Step 4 — Create `Dayswork/UI/SummaryMenu.cs`  [x]
**Extends** `IClickableMenu`. Screen 4 (thin) of the hiring flow.

**Layout** (centered on screen):
- Menu size: 700 wide × 500 tall
- Title: top-center
- Summary lines: tasks list (comma-separated), estimated hours, hourly rate, deposit, refund policy note
- "Confirm" button (ID 300): bottom-right; "Back" button (ID 301): bottom-left

**Constructor caches** (before draw() is ever called):
```
_rate     = coordinator passes rate (received from TaskSelectionMenu via draft state or explicit parameter)
_hours    = hoursEst.Estimate(effectiveZones, draft.EnabledTasks.Count, config)
_deposit  = depositCalc.Calculate(_hours, _rate) → extract int from DepositResult
```
Where `effectiveZones = draft.Zones.Count > 0 ? draft.Zones : new[] { WholeFarmZone }`.

**DepositResult handling**: `DepositResult` is a discriminated union. Extract the amount:
```csharp
_depositAmount = result switch {
    PositiveDeposit pd => pd.Amount,
    ZeroDeposit => 0,
    _ => 0
};
```

**draw()**: renders title, summary lines (from i18n keys with substitutions), Confirm/Back buttons, drawMouse.

**receiveLeftClick** + **receiveGamePadButton**: Confirm → `_onConfirm(_draft, _depositAmount, _rate)`; Back → `_onBack(_draft)`.

**Afford-check** lives in the coordinator's `ConfirmContract`, not here — SummaryMenu just invokes the callback.

Using directives same as TaskSelectionMenu + `Dayswork.Core.Pricing.DepositResult` etc.

---

### Step 5 — Create `Dayswork/Integration/ContractPersistenceAdapter.cs`  [x]

**Constructor**: `ContractPersistenceAdapter(IContractStore store, ISaveDataSerializer serializer, IDataHelper dataHelper, string modVersion)`

**OnSaveLoaded**:
1. `var dto = _dataHelper.ReadSaveData<DaysworkSaveDataV1>("Dayswork.Contracts")` 
2. `var json = dto == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(dto)`
3. `var contracts = _serializer.Deserialize(json)`
4. `_store.Hydrate(contracts)`

**OnSaving**:
1. `var contracts = _store.List()`
2. `var json = _serializer.Serialize(contracts, _modVersion)`
3. `var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<DaysworkSaveDataV1>(json)`
4. `_dataHelper.WriteSaveData("Dayswork.Contracts", dto)`

Using directives: `Dayswork.Core.Domain`, `Dayswork.Core.Persistence`, `Dayswork.Core.Persistence.Dto`, `Newtonsoft.Json`, `StardewModdingAPI`, `StardewModdingAPI.Events`

---

### Step 6 — Modify `Dayswork.Core/Persistence/ContractStore.cs` — Implement `ListActiveForDate`  [x]

Replace the `throw new NotImplementedException(...)` body with:

```csharp
public IReadOnlyList<Contract> ListActiveForDate(int day, Season season, int year)
{
    var target = new GameDate(day, season, year);
    return _contracts.Values
        .Where(c => c.Status == ContractStatus.Active && IsScheduledForDate(c, target))
        .ToList()
        .AsReadOnly();
}

private static bool IsScheduledForDate(Contract contract, GameDate date) =>
    contract.Schedule == ContractSchedule.Recurring || IsNextGameDay(contract.HireDate, date);

private static bool IsNextGameDay(GameDate hire, GameDate candidate)
{
    // Stardew seasons are 28 days; four seasons per year (Spring → Summer → Fall → Winter → Spring)
    var nextDay    = hire.Day + 1;
    var nextSeason = hire.Season;
    var nextYear   = hire.Year;
    if (nextDay > 28)
    {
        nextDay    = 1;
        nextSeason = (Season)(((int)hire.Season + 1) % 4);
        if (nextSeason == Season.Spring) nextYear++;  // wrapped from Winter
    }
    return candidate == new GameDate(nextDay, nextSeason, nextYear);
}
```

---

### Step 7 — Modify `Dayswork/Patches/BulletinBoardPatch.cs` — Wire Coordinator  [x]

In `ReceiveLeftClick_Postfix`, replace:
```csharp
ModEntry.ModMonitor.Log("[Dayswork] Hire-flow placeholder opened", LogLevel.Info);
```
with:
```csharp
ModEntry.Coordinator.OpenHiringFlow();
```

Add a corresponding `internal static HiringFlowCoordinator Coordinator { get; private set; } = null!;` property to `ModEntry` (set during Entry). `BulletinBoardPatch` accesses it the same way it accesses `ModMonitor`.

---

### Step 8 — Modify `Dayswork/ModEntry.cs` — Wire Singletons + Events  [x]

Add:
1. `internal static HiringFlowCoordinator Coordinator { get; private set; } = null!;` field
2. In `Entry()`, after existing setup, construct all Core singletons in dependency order:
   ```csharp
   var logWarning = (string msg) => this.Monitor.Log(msg, LogLevel.Warn);
   var config = ConfigDefaults.Build();
   var rateCalc      = new RateCalculator();
   var depositCalc   = new DepositCalculator();
   var hoursEst      = new HoursEstimator();
   var store         = new ContractStore(logWarning);
   var serializer    = new SaveDataSerializer(logWarning);
   Coordinator = new HiringFlowCoordinator(rateCalc, depositCalc, hoursEst, config, store);
   var persistAdapter = new ContractPersistenceAdapter(
       store, serializer, helper.Data, this.ModManifest.Version.ToString());
   helper.Events.GameLoop.SaveLoaded += persistAdapter.OnSaveLoaded;
   helper.Events.GameLoop.Saving     += persistAdapter.OnSaving;
   ```

Using directives to add: `Dayswork.Core.Config`, `Dayswork.Core.Persistence`, `Dayswork.Core.Pricing`, `Dayswork.UI`

---

### Step 9 — Modify `Dayswork/i18n/default.json` — Add 23 New Keys  [x]

Add all keys from NFR-UX-02 (nfr-requirements.md). Key list:
- `ui.task_selection.title`, `ui.task_selection.water_crops`, `ui.task_selection.harvest_crops`, `ui.task_selection.collect_fruit`, `ui.task_selection.feed_animals`, `ui.task_selection.pet_animals`, `ui.task_selection.collect_animal_products`, `ui.task_selection.cut_trees`, `ui.task_selection.clear_rocks`, `ui.task_selection.clear_weeds`, `ui.task_selection.clear_grass`, `ui.task_selection.rate_label`, `ui.task_selection.confirm_btn`, `ui.task_selection.cancel_btn`, `ui.summary.title`, `ui.summary.tasks_label`, `ui.summary.hours_label`, `ui.summary.rate_label`, `ui.summary.deposit_label`, `ui.summary.refund_policy`, `ui.summary.confirm_btn`, `ui.summary.back_btn`, `ui.error.cant_afford`

Note: that's 23 keys total (was listed as 22 in NFR Requirements — the `ui.summary.tasks_label` key was added; count updated here).

SMAPI token format: `{{token}}` (double braces) for Translation.Get substitutions.

---

### Step 10 — `dotnet build`  [x]
Run from workspace root. Target: 0 errors, 0 warnings.

---

### Step 11 — Code Summary Doc  [x]
Create `aidlc-docs/construction/U-09-minimum-hiring-flow/code/u-09-code-summary.md`

---

### Step 12 — Update `aidlc-state.md` + `audit.md`  [x]
Mark U-09 complete in aidlc-state.md. Append generation summary to audit.md.
