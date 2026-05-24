# Component Methods — Dayswork Pricing Model Redesign

Method signatures for each refreshed component interface. Detailed business rules still belong to Functional Design during Construction.

This refresh removes hourly billing, deposit, refund, and estimated-hours methods from the public application-design surface. The new signatures center on typed work scopes, contract terms snapshots, fixed pricing, and worker energy.

---

## Core Method Signatures

### IWorkScopeClassifier (C-01)
```csharp
WorkScopeSet Classify(
    ContractScopeSelection selection,
    IReadOnlySet<TaskKind> enabledTasks);
```
Builds the normalized set of outdoor, animal-building, and greenhouse work scopes.

---

### IOutdoorServiceBandClassifier (C-02)
```csharp
IReadOnlyList<OutdoorServiceBand> ClassifyBands(
    WorkScopeSet scopes,
    IReadOnlySet<TaskKind> enabledTasks,
    IConfigSnapshot config);
```
Assigns broad outdoor size bands per relevant outdoor service.

---

### IContractPriceCalculator (C-03)
```csharp
ContractPriceTotals Calculate(
    WorkScopeSet scopes,
    IReadOnlySet<TaskKind> enabledTasks,
    IReadOnlyList<OutdoorServiceBand> outdoorBands,
    IConfigSnapshot config);
```
Returns raw totals for the fixed-price model.

---

### IPriceBreakdownBuilder (C-04)
```csharp
PricingSnapshot BuildSnapshot(
    WorkScopeSet scopes,
    IReadOnlySet<TaskKind> enabledTasks,
    IReadOnlyList<OutdoorServiceBand> outdoorBands,
    ContractPriceTotals totals,
    IConfigSnapshot config);
```
Creates stable persisted/UI line items for contract pricing.

---

### IWorkerEnergyProfileBuilder (C-05)
```csharp
WorkerEnergyProfile BuildProfile(
    IReadOnlySet<TaskKind> enabledTasks,
    IConfigSnapshot config);
```
Creates the worker's daily energy capacity plus per-action cost table.

---

### IContractTermsBuilder (C-06)
```csharp
ContractTermsSnapshot BuildTerms(
    ContractScopeSelection selection,
    IReadOnlySet<TaskKind> enabledTasks,
    IConfigSnapshot config);

ContractTermsSnapshot RebuildTerms(
    Contract contract,
    IConfigSnapshot config);

ContractPreview BuildPreview(
    ContractScopeSelection selection,
    IReadOnlySet<TaskKind> enabledTasks,
    IConfigSnapshot config);
```
Pure facade over scope classification, banding, pricing, and energy profile building.

---

### IWorkerEnergyLedger (C-07)
```csharp
WorkerEnergyState StartShift(WorkerEnergyProfile profile);

WorkerEnergySpendResult ApplyActionCost(
    WorkerEnergyState current,
    WorkActionKind action);

bool CanStartNewWorkUnit(WorkerEnergyState current);
```
Tracks remaining energy, clamps at zero, and reports whether a new work unit may begin.

---

### IZoneGeometry (C-08)
```csharp
int CountReachableTiles(Zone zone, Func<TileCoord, bool> passabilityOracle);
Zone Union(Zone a, Zone b);
bool Contains(Zone zone, TileCoord tile);
IEnumerable<TileCoord> EnumerateTiles(Zone zone);
```

---

### ICapabilityEvaluator (C-09)
```csharp
CapabilityMatrix Evaluate(ToolSnapshot snapshot);
```

---

### ITaskPriorityOrderer (C-10)
```csharp
IReadOnlyList<WorkItem> OrderForExecution(IEnumerable<WorkItem> items);
```
Orders work items by the approved broad priority rules.

---

### IShiftStateMachine (C-11)
```csharp
ShiftState CurrentState { get; }

(ShiftState newState, IReadOnlyList<ShiftIntent> intents)
    Step(ShiftEvent evt);
```

**Notes**:
- `ShiftEvent` now includes energy-oriented events such as `EnergyDepletedAtWorkUnitBoundary`
- `ShiftIntent` no longer includes refund/billing intents
- Deposit and exit remain explicit intents

---

### IStuckDetector (C-12)
```csharp
void RecordTick(bool madeProgressThisTick, int inGameMinutesElapsed);
bool ShouldFireStuck();
void Reset();
```

---

### IItemBuffer (C-13)
```csharp
void Add(BufferedItem item, DestinationKey destination);
IReadOnlyList<BufferedItem> TakeAllFor(DestinationKey destination);
ItemBufferSnapshot Snapshot();
void Hydrate(ItemBufferSnapshot snapshot);
```

---

### IDepositPlanner (C-14)
```csharp
IReadOnlyList<DepositTrip> PlanTrips(
    ItemBufferSnapshot snapshot,
    IReadOnlyDictionary<TaskKind, DestinationKey> assignments,
    TileCoord workerCurrentTile,
    Func<TileCoord, TileCoord, int> distanceOracle);
```

---

### IContractStore (C-15)
```csharp
ContractId Add(Contract contract);
Contract Get(ContractId id);
void Update(ContractId id, Contract updated);
void ReplaceTermsSnapshot(ContractId id, ContractTermsSnapshot terms);
void Cancel(ContractId id);
void Pause(ContractId id);
void Resume(ContractId id);
IReadOnlyList<Contract> List();
IReadOnlyList<Contract> ListStartingToday(FarmDate today);
```

---

### ISaveDataSerializer (C-16)
```csharp
string Serialize(IReadOnlyList<Contract> contracts);
IReadOnlyList<Contract> Deserialize(string? json);
```

**Note**: `Deserialize` silently drops legacy hourly/deposit/refund contract payloads under the pre-release cleanup policy chosen in AD-R6.

---

### IConfigSnapshot (C-17)
```csharp
record ConfigSnapshot(
    IReadOnlyDictionary<OutdoorPriceKey, int> OutdoorBandPrices,
    IReadOnlyDictionary<AnimalBuildingPriceKey, int> AnimalBuildingPrices,
    IReadOnlyDictionary<GreenhousePriceKey, int> GreenhousePackagePrices,
    int WorkerDailyEnergyCapacity,
    IReadOnlyDictionary<WorkActionKind, int> ActionEnergyCosts,
    float WorkerMoveSpeedMultiplier,
    int TaskBeatDurationMs,
    int EightPmCapInGameMinutes,
    int StuckInitialThresholdMinutes,
    int StuckPostTeleportThresholdMinutes
);
```

---

### ConfigDefaults (C-18)
```csharp
static IConfigSnapshot Build();
```

---

## Mod Method Signatures

### ModEntry (M-01)
```csharp
public override void Entry(IModHelper helper);
```

---

### BulletinBoardPatch (M-02)
```csharp
static void Postfix_BulletinBoardDraw(Billboard __instance, SpriteBatch b);
static void Postfix_BulletinBoardClick(Billboard __instance, int x, int y);
```

---

### HiringFlowCoordinator (M-03)
```csharp
void OpenHiringFlow();
void OpenEditFlow(ContractId existing);
void RefreshPreview(ContractDraft draft);
void ConfirmDraft(ContractDraft draft, ContractTermsSnapshot terms);
```

---

### TaskSelectionMenu (M-04), ZoneAndChestMenu (M-05), ScheduleMenu (M-06), SummaryMenu (M-07)
```csharp
override void draw(SpriteBatch b);
override void receiveLeftClick(int x, int y, bool playSound = true);
override void receiveGamePadButton(Buttons b);
```

Menus receive a current `ContractPreview` from the coordinator rather than computing price/energy details themselves.

---

### ZoneDrawOverlay (M-08)
```csharp
void Activate(Action<Zone> onComplete);
void Deactivate();
```

---

### FarmhandNpc (M-09)
```csharp
override void update(GameTime time, GameLocation location);
override int takeDamage(...);
void BeginShift(IShiftStateMachine machine, ShiftOrchestrator orchestrator);
void UpdateEnergyBar(WorkerEnergyState state);
void EndShift();
```

---

### ToolSwapAnimator (M-10)
```csharp
void OnTaskChanged(TaskKind previous, TaskKind next);
void Draw(SpriteBatch b, Vector2 worldPosition);
```

---

### PathFindControllerAdapter (M-11)
```csharp
void PathTo(TileCoord destination);
bool IsPathing { get; }
event Action OnArrived;
event Action OnNoPathFound;
```

---

### ShiftOrchestrator (M-12)
```csharp
void StartShift(Contract contract, ToolSnapshot toolSnapshot);
void StopForSleepAndSettle();
void OnUpdateTicked(UpdateTickedEventArgs e);
void OnTimeChanged(TimeChangedEventArgs e);
```

---

### RecurringContractScheduler (M-13)
```csharp
void OnDayStarted(DayStartedEventArgs e);
```

---

### CalendarHandlers (M-14)
```csharp
bool IsFestivalToday();
bool IsRainyToday();
void OnSavingHook(SavingEventArgs e);
```

---

### ContractPersistenceAdapter (M-15)
```csharp
void OnSaveLoaded(SaveLoadedEventArgs e);
void OnSaving(SavingEventArgs e);
```

---

### MailDispatcher (M-16)
```csharp
void QueueOverflowMail(ItemBufferSnapshot overflow);
void QueueFestivalNotice(Contract contract);
void QueueCannotAffordNotice(Contract contract);
```

---

### GMCMRegistrar (M-17)
```csharp
void RegisterIfAvailable();
```

---

### MultiplayerGuard (M-18)
```csharp
bool IsSinglePlayerSession();
```

---

### ToolLevelReader (M-19)
```csharp
ToolSnapshot ReadCurrent();
```

---

### ChestResolver (M-20)
```csharp
Chest? Resolve(ChestRef reference);
IReadOnlyList<ChestDescriptor> ListFarmChests();
IReadOnlyList<ChestDescriptor> ListBuildingChests();
```

---

### I18nHelper (M-21)
```csharp
string Get(string key, object? tokens = null);
```

---

## Important Notes About Public Surface Changes

- Any method returning or consuming hourly-rate, deposit, refund, or estimated-hours values is intentionally gone from the refreshed application-design surface.
- Contract confirmation now revolves around `ContractTermsSnapshot` and fixed total price.
- Daily runtime now revolves around `WorkerEnergyProfile` and `WorkerEnergyState`.
- Recurring repricing is an explicit scheduler concern, not an emergent side effect of estimated hours.
