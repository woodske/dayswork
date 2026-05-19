# NFR Design Patterns — U-09 Minimum Hiring Flow

Six patterns address the applicable NFRs from [nfr-requirements.md](../nfr-requirements/nfr-requirements.md).

---

## Pattern 1 — Cached-Computation Draw Pattern
**Addresses**: NFR-PERF-01 (frame budget), T9-07 (rate per toggle), T9-06 (hours estimate once)

### Problem
`IClickableMenu.draw(SpriteBatch b)` is called every frame at 60fps. Calling `IRateCalculator` or `IHoursEstimator` inside `draw()` would execute expensive logic up to 60 times per second.

### Solution
Compute once, cache in a field, read in draw.

**TaskSelectionMenu**:
```csharp
private int _currentRate;      // updated on each task toggle
private int _currentDeposit;   // updated on each task toggle (for future: here unused in TaskSelectionMenu)

// Called from the toggle handler:
private void OnTaskToggled(TaskKind task)
{
    if (_enabledTasks.Contains(task)) _enabledTasks.Remove(task);
    else _enabledTasks.Add(task);
    _currentRate = _rateCalc.ComputeHourlyRate(_enabledTasks, _config, isRainyDay: false);
}

// draw() only reads:
b.DrawString(Game1.smallFont, I18nHelper.Get("ui.task_selection.rate_label",
    new { rate = _currentRate }), ratePos, Color.Black);
```

**SummaryMenu** — all three cached values set in constructor (or `Initialize()`):
```csharp
public SummaryMenu(ContractDraft draft, IHoursEstimator hoursEst,
                   IDepositCalculator depositCalc, IConfigSnapshot config, ...)
{
    _estimatedHours = hoursEst.EstimateHours(
        draft.Zones.Count > 0 ? draft.Zones : WholeFarmZones(),
        draft.EnabledTasks, config,
        coord => Game1.getFarm().isTilePassable(new Location(coord.X, coord.Y), Rectangle.Empty));
    _currentRate = ...; // already computed by TaskSelectionMenu; passed in via draft or coordinator
    _deposit = depositCalc.ComputeDeposit(_estimatedHours, _currentRate);
}
```

**Rule**: `draw()` contains only `b.Draw(...)` and `b.DrawString(...)` calls. Zero calls to Core interfaces.

---

## Pattern 2 — Coordinator-Driven Screen Transition Pattern
**Addresses**: T9-02 (screen transition ownership), NFR-MAINT-03 (decoupling)

### Problem
The hiring flow spans two screens (U-09 thin: TaskSelection → Summary). Menus must not know about each other. The flow must be controlled centrally.

### Solution
`HiringFlowCoordinator` owns all menu construction and `Game1.activeClickableMenu` assignments. Menus receive `Action` callbacks at construction time.

```
Player clicks "Hire a Farmhand" button
    └── BulletinBoardPatch calls coordinator.OpenHiringFlow()
            └── coordinator creates TaskSelectionMenu(onAdvance: coordinator.ShowSummary,
                                                       onCancel: coordinator.CloseFlow)
                sets Game1.activeClickableMenu = taskMenu

Player toggles tasks, clicks "Next"
    └── taskMenu calls onAdvance(draft)
            └── coordinator creates SummaryMenu(draft,
                                                 onConfirm: coordinator.ConfirmContract,
                                                 onBack: coordinator.BackToTaskSelection)
                sets Game1.activeClickableMenu = summaryMenu

Player clicks "Confirm"
    └── summaryMenu calls onConfirm(draft)
            └── coordinator.ConfirmContract(draft):
                    1. Afford-check (Pattern 4)
                    2. Money -= deposit
                    3. Build Contract from draft
                    4. _contractStore.Add(contract)
                    5. Game1.activeClickableMenu = null (or close billboard)
```

**Key invariant**: `Game1.activeClickableMenu =` is set only inside `HiringFlowCoordinator`. Never set from within a menu class.

---

## Pattern 3 — Constructor-Injected Core Services Pattern
**Addresses**: NFR-MAINT-03 (SMAPI integration separation)

### Problem
Menus need `IRateCalculator`, `IHoursEstimator`, `IDepositCalculator`, and `IConfigSnapshot` to compute live values. If menus instantiate these directly (`new RateCalculator()`), they become tightly coupled to implementations and untestable in isolation.

### Solution
All Core interfaces are constructor parameters. `ModEntry.Entry()` is the sole composition root.

```
ModEntry.Entry()
├── IRateCalculator    rateCalc   = new RateCalculator()
├── IDepositCalculator depositCalc = new DepositCalculator()
├── IHoursEstimator    hoursEst   = new HoursEstimator()
├── IConfigSnapshot    config     = ConfigDefaults.Build()  // later replaced by GMCM
├── IContractStore     store      = new ContractStore()
├── ISaveDataSerializer serializer = new SaveDataSerializer()
└── HiringFlowCoordinator coordinator =
        new HiringFlowCoordinator(rateCalc, depositCalc, hoursEst, config, store)
```

`HiringFlowCoordinator` passes the relevant subset to each menu at construction time. No menu has a `new` call for a Core type.

**Verification**: `Dayswork.Core.csproj` has no reference to `StardewValley` or `StardewModdingAPI`. Any Core dependency on Mod assemblies is a compile error.

---

## Pattern 4 — Inline Afford-Guard + HUDMessage Pattern
**Addresses**: NFR-SAFE-02 (no gold loss), FR-HIRE-14 (block confirm if can't afford), T9-05

### Problem
If the player clicks "Confirm" without enough gold, the mod must block the deduction and show a clear error. Deducting then refunding is unsafe.

### Solution
Single sequential guard in the confirm handler. No dedicated validator class.

```csharp
private void HandleConfirm(ContractDraft draft, int deposit)
{
    // Guard — must come before any deduction
    if (Game1.player.Money < deposit)
    {
        Game1.addHUDMessage(new HUDMessage(
            I18nHelper.Get("ui.error.cant_afford"),
            HUDMessage.error_type));   // red notification matching vanilla "not enough money" style
        return;                        // no further action
    }

    // Only reached when player can afford
    Game1.player.Money -= deposit;
    var contract = BuildContract(draft, deposit);
    _contractStore.Add(contract);
    CloseFlow();
}
```

**Why `HUDMessage.error_type`**: Matches Stardew's built-in "insufficient funds" visual (red pill, error icon) — immediately recognizable to players without needing a separate dialog popup.

---

## Pattern 5 — SMAPI Gamepad Snapping Pattern
**Addresses**: NFR-UX-01 (full gamepad navigation), FR-HIRE-03

### Problem
Controller players use D-pad to move between menu buttons. Without explicit neighbor IDs, SMAPI cannot route focus correctly.

### Solution
Each menu registers all interactive `ClickableComponent`s in `populateClickableComponentList()` and assigns neighbor IDs forming a navigation graph.

**TaskSelectionMenu layout** (10 task toggles in a vertical list + Next/Cancel buttons):
```csharp
// In the constructor, assign IDs:
// Toggle IDs: 100–109 (one per TaskKind, in priority order)
// "Next" button: ID 200
// "Cancel" button: ID 201

// Neighbor wiring (vertical list):
toggles[0].downNeighborID = 101;   // first → second
toggles[1].upNeighborID   = 100;   // second ← first
// ... etc. for all 10
toggles[9].downNeighborID = 200;   // last toggle → Next button
nextBtn.upNeighborID      = 109;
nextBtn.rightNeighborID   = 201;   // Next → Cancel
cancelBtn.leftNeighborID  = 200;
```

```csharp
public override void populateClickableComponentList()
{
    allClickableComponents ??= new List<ClickableComponent>();
    allClickableComponents.Clear();
    allClickableComponents.AddRange(_toggleComponents);
    allClickableComponents.Add(_nextButton);
    allClickableComponents.Add(_cancelButton);
}

public override void setCurrentlySnappedComponentTo(int id)
{
    currentlySnappedComponent = getComponentWithID(id);
    snapCursorToCurrentSnappedComponent();
}

public override void receiveGamePadButton(Buttons b)
{
    if (b == Buttons.B) { _onCancel(); return; }
    if (b == Buttons.A)
    {
        if (currentlySnappedComponent?.myID is int id)
        {
            if (id >= 100 && id <= 109) ToggleTask((TaskKind)(id - 100));
            else if (id == 200) _onAdvance(_draft);
            else if (id == 201) _onCancel();
        }
    }
    base.receiveGamePadButton(b);  // handles D-pad movement via allClickableComponents
}
```

**Default focus on open**: `setCurrentlySnappedComponentTo(100)` called in coordinator after setting `Game1.activeClickableMenu`.

---

## Pattern 6 — SMAPI Data API Read/Write Pattern
**Addresses**: NFR-SAFE-03 (no save corruption), T9-04

### Problem
Contracts must persist across game sessions without touching the vanilla save file or risking corruption.

### Solution
`ContractPersistenceAdapter` uses SMAPI's `IModHelper.Data` API, invoked only in `SaveLoaded` and `Saving` events.

```csharp
public void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
{
    var dto = _helper.Data.ReadSaveData<DaysworkSaveDataV1>("Dayswork.Contracts");
    // ISaveDataSerializer.Deserialize handles null → empty list (established in U-06)
    var contracts = _serializer.Deserialize(dto == null ? null : JsonConvert.SerializeObject(dto));
    foreach (var c in contracts) _store.Add(c);
}

public void OnSaving(object? sender, SavingEventArgs e)
{
    var contracts = _store.List();
    var json = _serializer.Serialize(contracts);
    var dto = JsonConvert.DeserializeObject<DaysworkSaveDataV1>(json);
    _helper.Data.WriteSaveData("Dayswork.Contracts", dto);
}
```

**Key properties**:
- `ReadSaveData` returns null on first load (no key yet) — handled by serializer.
- `WriteSaveData` overwrites the previous value atomically (SMAPI responsibility).
- No try/catch needed around these calls — SMAPI logs API errors and the game save proceeds regardless.
- The `"Dayswork.Contracts"` key is namespaced by SMAPI to this mod's data folder — cannot collide with vanilla or other mods.
