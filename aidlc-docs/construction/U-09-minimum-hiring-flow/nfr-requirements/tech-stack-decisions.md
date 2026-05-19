# Tech Stack Decisions — U-09 Minimum Hiring Flow

## Unit
U-09 — Minimum Hiring Flow

---

## Decision T9-01 — IClickableMenu as UI Base Class

**Decision**: All hiring UI screens (`TaskSelectionMenu`, `SummaryMenu`) extend `StardewValley.Menus.IClickableMenu`.

**Rationale**:
- `IClickableMenu` is SMAPI/Stardew's standard base for all modal UI (shops, dialogue boxes, inventory).
- It provides the frame-drawing infrastructure (`drawTextureBox`), input routing (`receiveLeftClick`, `receiveRightClick`, `receiveKeyPress`, `receiveGamePadButton`), and the clickable-component snapping system for gamepad navigation.
- Alternatives (raw `SpriteBatch` overlay, custom `Game1.overlayMenu`) lack the snapping system and break conventional menu behavior.

**Impact**: Both menus must call `base.draw(b)` for the standard wooden frame, and must override `populateClickableComponentList()` for gamepad D-pad navigation to work.

---

## Decision T9-02 — Screen Transition: Coordinator-Owned Push

**Decision**: `HiringFlowCoordinator` owns all screen transitions. Menus signal completion via a `Action<ContractDraft>` callback passed at construction; they never directly push the next screen.

**Rationale**:
- Keeps menus decoupled — `TaskSelectionMenu` doesn't know `SummaryMenu` exists.
- Coordinator controls the linear flow: TaskSelection → Summary → [Confirm or Back].
- For U-09's thin slice (no Zone/Chest menu, no Schedule menu), the flow is: `TaskSelectionMenu` → on "Next" → `SummaryMenu` → on "Confirm" → deduct + save + close; on "Back" → return to `TaskSelectionMenu`.

**Transition mechanics**:
- `Game1.activeClickableMenu = new SummaryMenu(draft, coordinator.OnSummaryConfirm, coordinator.OnSummaryBack)` is set by the coordinator when TaskSelectionMenu calls its `onAdvance` delegate.
- `IClickableMenu.exitFunction` is set so the coordinator can react to the player pressing Escape/B to cancel the entire flow.

---

## Decision T9-03 — ContractDraft as Mutable UI-Local Value Object

**Decision**: `ContractDraft` is a mutable class in `Dayswork.UI` namespace, not a Core domain record.

**Rationale**:
- The draft accumulates player choices across screens (tasks, zone (stub), schedule (stub)).
- It does not persist and is never serialized; it exists only for the duration of the hiring flow.
- Making it a `record` would require creating new instances on every toggle — needlessly verbose for UI state.
- It is explicitly NOT a Core type: `Dayswork.Core` has no dependency on `ContractDraft`.

**Shape**:
```csharp
class ContractDraft
{
    public HashSet<TaskKind> EnabledTasks { get; } = new();
    // Stubs for U-09 (real values added in U-11 and U-12):
    public List<Zone> Zones { get; } = new();          // empty = whole-farm default
    public Dictionary<TaskKind, DestinationKey> Assignments { get; } = new(); // empty = shipping bin
    public ScheduleKind Schedule { get; set; } = ScheduleKind.OneTime;
}
```

---

## Decision T9-04 — SMAPI Data API for Contract Persistence

**Decision**: `ContractPersistenceAdapter` uses `IModHelper.Data.WriteSaveData` / `ReadSaveData` with key `"Dayswork.Contracts"`.

**Rationale**:
- SMAPI's data API writes to `<ModData>/<modId>/Dayswork.Contracts.json` alongside the save — never touches the vanilla save file.
- `ReadSaveData<DaysworkSaveDataV1>` returns `null` when the key is absent (first load, fresh save). The adapter passes the null to `ISaveDataSerializer.Deserialize(null)` which returns an empty list per U-06 contract.
- Type parameter `DaysworkSaveDataV1` is the versioned DTO established in U-06.

**Event wiring** (added to ModEntry in this unit):
```csharp
Helper.Events.GameLoop.SaveLoaded += _persistenceAdapter.OnSaveLoaded;
Helper.Events.GameLoop.Saving     += _persistenceAdapter.OnSaving;
```

---

## Decision T9-05 — Deposit Deduction: Inline Afford-Check Before Money Subtraction

**Decision**: SummaryMenu's confirm handler performs the afford-check and deduction inline, in two steps:

```csharp
// Step 1: Check
if (Game1.player.Money < deposit)
{
    // show cant-afford HUD message and return — do not proceed
    Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("ui.error.cant_afford"), HUDMessage.error_type));
    return;
}
// Step 2: Deduct (only reached if affordable)
Game1.player.Money -= deposit;
// Step 3: Build Contract and hand to coordinator
coordinator.OnContractConfirmed(BuildContract(draft, deposit));
```

**Rationale**:
- `HUDMessage` with `error_type` matches Stardew's built-in "not enough money" red message style — familiar to players.
- The deduction must be atomic with the contract creation: if `OnContractConfirmed` throws, gold has already been deducted. Acceptable for v1 (errors here would be bugs, caught in play-test); a full transactional approach is out of scope.
- No separate "payment service" class — FR-PAY-03 is a one-liner in the confirm handler.

---

## Decision T9-06 — Hours Estimate: Called Once at SummaryMenu Construction

**Decision**: `IHoursEstimator.EstimateHours()` is called once in `SummaryMenu`'s constructor (or an `Initialize()` method called at construction time), result stored in `_estimatedHours`.

**Rationale**:
- Calling it in `draw()` would execute it 60× per second — unnecessary for a static estimate.
- The estimate doesn't change while SummaryMenu is open (task selection is locked on the previous screen).
- For U-09's whole-farm default zone, the passability oracle is `(c) => Game1.getFarm().isTilePassable(new Location(c.X, c.Y), Rectangle.Empty)`.

---

## Decision T9-07 — Task Toggle Rate: Recompute IRateCalculator on Each Toggle

**Decision**: In `TaskSelectionMenu`, each task toggle click calls `IRateCalculator.ComputeHourlyRate(enabledTasks, config, isRainyDay)` immediately, updates `_currentRate`, and triggers a `draw()` to show the new rate. No debounce.

**Rationale**:
- `IRateCalculator` is O(n) for n ≤ 10 tasks — executes in < 0.1ms.
- Immediate feedback is the intended UX: FR-HIRE-04 says "the hourly rate updates live as tasks are enabled/disabled."
- Debouncing would add complexity with no observable benefit.

---

## Summary

| Decision | Choice | Rationale |
|---|---|---|
| T9-01 | `IClickableMenu` base class | SMAPI standard; gamepad snapping built-in |
| T9-02 | Coordinator-owned screen transitions | Decouples menus; coordinator controls flow |
| T9-03 | `ContractDraft` mutable class in UI namespace | UI-only state; not a Core domain type |
| T9-04 | SMAPI `WriteSaveData`/`ReadSaveData` | Safe; doesn't touch vanilla save file |
| T9-05 | Inline afford-check before `Money -=` | Simple; HUDMessage matches vanilla error style |
| T9-06 | `EstimateHours()` called once in SummaryMenu ctor | Avoids per-frame tile scan |
| T9-07 | Recompute rate on each toggle | O(n) fast; live feedback per FR-HIRE-04 |
