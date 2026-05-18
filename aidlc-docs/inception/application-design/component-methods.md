# Component Methods — Dayswork

Method signatures for each component's public interface. **No business logic** — those rules land in per-unit Functional Design during Construction. I/O types and one-line purposes only.

> C# convention: `interface` types start with `I`. Records (`record class`) used for immutable DTOs. `Result<T>` is a discriminated-union-ish wrapper (e.g., `OneOf<T, Error>`); the choice of library is a Construction decision but the *shape* is decided here.

---

## Core method signatures

### IRateCalculator (C-01)
```csharp
int ComputeHourlyRate(
    IReadOnlySet<TaskKind> enabledTasks,
    IConfigSnapshot config,
    bool isRainyDay);
```
Returns gold/hour. Strips Water Crops surcharge when `isRainyDay && enabledTasks contains WaterCrops`.

---

### IDepositCalculator (C-02)
```csharp
int ComputeDeposit(decimal estimatedHours, int hourlyRate);
```
Integer gold; rounds up to the nearest gold piece. Returns 0 if `estimatedHours == 0`.

---

### IRefundCalculator (C-03)
```csharp
int ComputeRefund(int deposit, decimal actualHoursWorked, int hourlyRate);
```
`refund = deposit - ceiling(actualHoursWorked * hourlyRate)`, clamped to `[0, deposit]`.

---

### IHoursEstimator (C-04)
```csharp
decimal EstimateHours(
    IReadOnlyList<Zone> zones,
    IReadOnlySet<TaskKind> enabledTasks,
    IConfigSnapshot config,
    Func<TileCoord, bool> passabilityOracle);
```

---

### IZoneGeometry (C-05)
```csharp
int CountReachableTiles(Zone zone, Func<TileCoord, bool> passabilityOracle);
Zone Union(Zone a, Zone b);
bool Contains(Zone zone, TileCoord tile);
IEnumerable<TileCoord> EnumerateTiles(Zone zone);
```

---

### ICapabilityEvaluator (C-06)
```csharp
CapabilityMatrix Evaluate(ToolSnapshot snapshot);
```
Where `CapabilityMatrix` is a record of booleans: `CanChopSmallStump`, `CanChopLargeLog`, `CanBreakSmallBoulder`, `CanBreakLargeBoulder`, `CanBreakMeteorite`, `CanWater`, `CanScythe`. Fruit-tree felling is *not* in this matrix — it's always false (FR-SKIP-03 is a hard rule, not a capability check).

---

### ITaskPriorityOrderer (C-07)
```csharp
IReadOnlyList<WorkItem> OrderForExecution(IEnumerable<WorkItem> items);
```
Where `WorkItem` is a `(TaskKind kind, TileCoord tile)` record.

---

### IShiftStateMachine (C-08)
```csharp
ShiftState CurrentState { get; }

(ShiftState newState, IReadOnlyList<ShiftIntent> intents)
    Step(ShiftEvent evt);
```
`ShiftEvent` discriminated union: `TickElapsed`, `ArrivedAtTile`, `TaskCompleted`, `StuckDetected`, `TeleportFailed`, `ClockReached8pm`, `DepositRunComplete`, `SleepFastForwardRequested`.
`ShiftIntent` discriminated union: `MoveToTile`, `PerformTaskOnTile`, `PlayEmote`, `TeleportToTile`, `TeleportHome`, `DepositAtChest`, `DepositInShippingBin`, `QueueMail`, `ApplyRefund`, `ExitFarm`.

---

### IStuckDetector (C-09)
```csharp
void RecordTick(bool madeProgressThisTick, int inGameMinutesElapsed);
bool ShouldFireStuck();
void Reset();
```

---

### IItemBuffer (C-10)
```csharp
void Add(BufferedItem item, DestinationKey destination);
IReadOnlyDictionary<DestinationKey, IReadOnlyList<BufferedItem>> TakeAllFor(DestinationKey destination);
ItemBufferSnapshot Snapshot();
void Hydrate(ItemBufferSnapshot snapshot);
```
`DestinationKey` is a discriminated union: `ChestRef chest`, `ShippingBin`, `Mail`.

---

### IDepositPlanner (C-11)
```csharp
IReadOnlyList<DepositTrip> PlanTrips(
    ItemBufferSnapshot snapshot,
    IReadOnlyDictionary<TaskKind, DestinationKey> assignments,
    TileCoord workerCurrentTile,
    Func<TileCoord, TileCoord, int> distanceOracle);
```

---

### IContractStore (C-12)
```csharp
ContractId Add(Contract contract);
Contract Get(ContractId id);
void Update(ContractId id, Contract updated);
void Cancel(ContractId id);
void Pause(ContractId id);
void Resume(ContractId id);
IReadOnlyList<Contract> List();
IReadOnlyList<Contract> ListActiveForDate(int day, Season season, int year);
```

---

### ISaveDataSerializer (C-13)
```csharp
string Serialize(IReadOnlyList<Contract> contracts);
IReadOnlyList<Contract> Deserialize(string? json);  // null/empty → empty list
```

---

### IConfigSnapshot (C-14)
```csharp
record ConfigSnapshot(
    int BaseHourlyRate,
    IReadOnlyDictionary<TaskKind, int> TaskIncrements,
    decimal AverageSpeedTilesPerHour,
    int EightPmCapInGameMinutes,          // 1200 by default (8 * 60)
    int StuckInitialThresholdMinutes,     // 10 by default
    int StuckPostTeleportThresholdMinutes // 10 by default
);
```

---

### ConfigDefaults (C-15)
```csharp
static IConfigSnapshot Build();
```

---

## Mod method signatures

### ModEntry (M-01)
```csharp
public override void Entry(IModHelper helper);  // SMAPI required
```
Internal composition wires up all Mod- and Core-side singletons and registers SMAPI event handlers.

---

### BulletinBoardPatch (M-02)
```csharp
static void Postfix_BulletinBoardDraw(BulletinBoard __instance, SpriteBatch b);
static void Postfix_BulletinBoardClick(BulletinBoard __instance, int x, int y);
```
Harmony patch entry points. Postfixes only — no prefixes (NFR-MAINT-04).

---

### HiringFlowCoordinator (M-03)
```csharp
void OpenHiringFlow();
void OpenEditFlow(ContractId existing);
```
Both push a `TaskSelectionMenu` onto the menu stack with an in-progress `ContractDraft`.

---

### TaskSelectionMenu (M-04), ZoneAndChestMenu (M-05), ScheduleMenu (M-06), SummaryMenu (M-07)
```csharp
override void draw(SpriteBatch b);                 // IClickableMenu
override void receiveLeftClick(int x, int y, ...); // IClickableMenu
override void receiveGamePadButton(Buttons b);     // gamepad
// + each menu exposes a small `Coordinator` callback to advance/back
```

---

### ZoneDrawOverlay (M-08)
```csharp
void Activate(Action<Zone> onComplete);
void Deactivate();
// internally subscribes to Display.RenderedWorld + InputEvents
```

---

### FarmhandNpc (M-09)
```csharp
override void update(GameTime time, GameLocation location);  // StardewValley.NPC
override int takeDamage(...);  // overridden to return 0 + play OuchEmote
void BeginShift(IShiftStateMachine machine, IShiftOrchestrator orchestrator);
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
void StartShift(Contract contract, ToolSnapshot toolSnapshot, IConfigSnapshot config);
void OnSleepFastForwardRequested();   // called by CalendarHandlers
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
void OnSavingHook(SavingEventArgs e);  // triggers sleep fast-forward
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
void QueueOverflowMail(IReadOnlyList<BufferedItem> items, string reasonKey);
void QueueCannotAffordNotice(ContractId contractId);
void QueueToolMissingWarning(IReadOnlySet<TaskKind> skippedTasks);
```
Each method builds the SMAPI mail letter, looks up the body via `I18nHelper`, and queues for the next morning's mail.

---

### GMCMRegistrar (M-17)
```csharp
void RegisterIfAvailable();
```
Probes for the GMCM API; if present, wires up every `IConfigSnapshot` field.

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
IReadOnlyList<(Building building, IReadOnlyList<Chest> chests)> EnumerateBuildingChests(GameLocation farm);
```

---

### I18nHelper (M-21)
```csharp
string Get(string key);
string Get(string key, object templateArgs);
```
