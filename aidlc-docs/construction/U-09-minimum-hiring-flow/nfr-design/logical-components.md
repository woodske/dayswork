# Logical Components — U-09 Minimum Hiring Flow

## Component Map

```
ModEntry (composition root — M-01, extended)
├── Singletons constructed in Entry():
│   ├── IRateCalculator          (C-01, from U-05)
│   ├── IDepositCalculator       (C-02, from U-05)
│   ├── IHoursEstimator          (C-04, from U-05)
│   ├── IConfigSnapshot          (C-14/C-15, from U-03)
│   ├── IContractStore           (C-12, from U-06)
│   ├── ISaveDataSerializer      (C-13, from U-06)
│   ├── HiringFlowCoordinator    (M-03, new this unit)
│   └── ContractPersistenceAdapter (M-15, new this unit)
│
├── Event registrations added in Entry():
│   ├── GameLoop.SaveLoaded  → ContractPersistenceAdapter.OnSaveLoaded
│   └── GameLoop.Saving      → ContractPersistenceAdapter.OnSaving
│
└── Wiring added to BulletinBoardPatch (M-02, from U-08):
    └── OnHireButtonClicked  → HiringFlowCoordinator.OpenHiringFlow()
```

---

## Screen Flow (U-09 thin slice — 2 of 4 screens)

```
BulletinBoardPatch.OnHireButtonClicked
    |
    v
HiringFlowCoordinator.OpenHiringFlow()
    |   creates ContractDraft (empty)
    |   creates TaskSelectionMenu(draft, onAdvance, onCancel, rateCalc, config)
    |   sets Game1.activeClickableMenu = TaskSelectionMenu
    v
TaskSelectionMenu (open)
    |   Player toggles tasks
    |   Each toggle: draft.EnabledTasks.Add/Remove(task)
    |                _currentRate = rateCalc.ComputeHourlyRate(enabledTasks, config, rainy=false)
    |                draw() reads _currentRate from field
    |
    |--- Player clicks "Cancel" or presses B
    |       onCancel() → coordinator.CloseFlow() → Game1.activeClickableMenu = null
    |
    +--- Player clicks "Next" or presses A on Next button
            onAdvance(draft) → coordinator.ShowSummary(draft)
                |   creates SummaryMenu(draft, onConfirm, onBack, hoursEst, depositCalc, config)
                |   SummaryMenu ctor: _estimatedHours = hoursEst.EstimateHours(...)
                |                    _deposit = depositCalc.ComputeDeposit(hours, rate)
                |   sets Game1.activeClickableMenu = SummaryMenu
                v
            SummaryMenu (open)
                |   draw() reads _estimatedHours, _currentRate, _deposit from fields
                |
                |--- Player clicks "Back" or presses B
                |       onBack() → coordinator.BackToTaskSelection(draft)
                |                    re-creates TaskSelectionMenu with same draft (preserves selections)
                |
                +--- Player clicks "Confirm" or presses A on Confirm button
                        onConfirm(draft, _deposit) → coordinator.ConfirmContract(draft, deposit)
                            |
                            |--- Afford-check: Game1.player.Money < deposit?
                            |       YES: HUDMessage(cant_afford, error_type); return
                            |
                            +--- NO: Game1.player.Money -= deposit
                                     contract = BuildContract(draft, deposit)
                                     _contractStore.Add(contract)
                                     coordinator.CloseFlow()
```

---

## ContractPersistenceAdapter Call Flow

```
GameLoop.SaveLoaded fires (player loads a save)
    ContractPersistenceAdapter.OnSaveLoaded(e)
        dto = helper.Data.ReadSaveData<DaysworkSaveDataV1>("Dayswork.Contracts")
              // null on first load
        json = dto == null ? null : JsonConvert.SerializeObject(dto)
        contracts = serializer.Deserialize(json)
              // ISaveDataSerializer.Deserialize(null) → empty list (U-06 contract)
        foreach contract in contracts: store.Add(contract)

GameLoop.Saving fires (game is about to save)
    ContractPersistenceAdapter.OnSaving(e)
        contracts = store.List()
        json = serializer.Serialize(contracts)
        dto = JsonConvert.DeserializeObject<DaysworkSaveDataV1>(json)
        helper.Data.WriteSaveData("Dayswork.Contracts", dto)
```

---

## N/A Logical Components

| Component | Rationale |
|---|---|
| Queue / message bus | No async work; all calls are synchronous on Stardew's update thread |
| Cache layer | No repeated expensive lookups; estimates are one-shot per menu open |
| Circuit breaker | No external service calls |
| Load balancer / replica | Single-player, single process |
| Zone draw overlay | U-11 concern |
| Schedule screen | U-12 concern |
| Mail dispatcher | U-14 concern |

---

## ModEntry Extension Summary (U-09 additions)

In addition to the U-08 additions (MultiplayerGuard check, Harmony.PatchAll), ModEntry.Entry() gains in U-09:

```csharp
// --- Core singletons (already created in U-03..U-07) ---
// IRateCalculator, IDepositCalculator, IHoursEstimator, IConfigSnapshot,
// IContractStore, ISaveDataSerializer already exist from prior units.

// --- New Mod singletons ---
var coordinator = new HiringFlowCoordinator(
    _rateCalc, _depositCalc, _hoursEst, _config, _store);

var persistenceAdapter = new ContractPersistenceAdapter(
    _store, _serializer, Helper.Data);

// --- Persistence events ---
Helper.Events.GameLoop.SaveLoaded += persistenceAdapter.OnSaveLoaded;
Helper.Events.GameLoop.Saving     += persistenceAdapter.OnSaving;

// --- Wire coordinator into bulletin board patch ---
// BulletinBoardPatch.Coordinator = coordinator;
// (static setter, or injected at Harmony patch registration time)
```

Note: `BulletinBoardPatch` currently (U-08) logs a placeholder on click. In U-09 it calls `coordinator.OpenHiringFlow()` instead. This is a one-line change to `BulletinBoardPatch.cs`.
