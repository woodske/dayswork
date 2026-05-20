# Code Generation Plan — U-12 Hiring UI: Schedule + Edit/Pause/Cancel

## Context

**Unit**: U-12 — Hiring UI: Schedule + Edit/Pause/Cancel  
**Stories**: S-05 (full — schedule UI + state persistence), S-12 (Pause/Cancel/Edit flows)  
**Workspace root**: `C:\Users\kwood\Repos\dayswork`

**Pre-conditions confirmed**:
- `ContractStore.Pause/Resume/Cancel` already fully implemented in U-06 (verified in code)
- `ContractStoreTests` already covers all xUnit state-transition behaviors
- `ContractGen` already generates contracts with Paused status (PBT-02 satisfied by existing SaveDataSerializerTests)
- `ContractDtoV1.Status` already serializes Paused state as a string — no DTO schema change needed
- `ShiftContext.ContractId` exists and is accessible
- `ContractDraft.Schedule` already defaults to `ContractSchedule.OneTime`
- `HiringFlowCoordinator.OpenEditFlow(ContractId)` exists as a stub — to be implemented here

**NFR design adaptations vs. plan**:
- `ContractOperationResult` enum: NOT needed — existing void-return + exception pattern is already complete and tested; UI layer guards with explicit checks instead
- `IsPaused` field on ContractDtoV1: NOT needed — `ContractDtoV1.Status` already carries the Paused state as a string
- PBT-03 obligation: existing xUnit tests cover state transitions; U-12 adds one FsCheck round-trip invariant to satisfy PBT-03 formally

---

## JIT Docs

### IClickableMenu patterns (SMAPI)
The following SMAPI/Stardew patterns apply to all menus in this unit:

**Constructor layout**: `IClickableMenu(int x, int y, int width, int height, bool showUpperRightCloseButton = false)`. Use `IClickableMenu.borderWidth` (16px) for standard padding. For a centered menu: `x = (Game1.uiViewport.Width - width) / 2`, `y = (Game1.uiViewport.Height - height) / 2`.

**draw() contract**: Called every frame. Do NOT call `ContractStore.List()` or format strings here. Reads only pre-computed fields.

**Gamepad wiring**:
```csharp
public override void populateClickableComponentList()
{
    allClickableComponents.Clear();
    allClickableComponents.Add(btnA);  // myID = 100
    allClickableComponents.Add(btnB);  // myID = 101
    // etc.
}
public override void snapToDefaultClickableComponent()
{
    currentlySnappedComponent = getComponentWithID(100);
    snapCursorToCurrentSnappedComponent();
}
public override void receiveGamePadButton(Buttons b)
{
    if (b == Buttons.B) { onBack(); return; }
    base.receiveGamePadButton(b);  // handles A → click on current snapped component
}
```

**D-pad chain**: Set `upNeighborID` / `downNeighborID` on each `ClickableComponent` to wire the D-pad navigation path.

**drawTextureBox**: `IClickableMenu.drawTextureBox(b, x, y, width, height, Color.White)` draws the standard Stardew panel background.

**Utility.drawTextWithShadow**: `Utility.drawTextWithShadow(b, text, font, position, color)` — use for all text rendering.

**SpriteText.drawStringHorizontallyCenteredAt**: `SpriteText.drawStringHorizontallyCenteredAt(b, text, x, y)` — for centered headers.

### ContractStore state rules (from existing implementation)
- `Pause(id)`: throws if already Paused or if Cancelled
- `Resume(id)`: throws if already Active or if Cancelled
- `Cancel(id)`: throws if already Cancelled — keeps contract in store with Status = Cancelled
- ContractListMenu shows only Active and Paused contracts (filter out Cancelled and Executed)
- Cancel guard: check `ModEntry.Orchestrator.ActiveContractId == contract.Id` BEFORE calling `_store.Cancel(id)`

### Edit flow design
- Edit opens the full 4-screen hiring flow with `ContractDraft` pre-filled from the existing contract
- No gold deduction on edit confirm — original deposit was already paid at hire time; recurring contracts' next-morning deposit will use the updated rate
- `ContractDraft.EditingId` (new `ContractId?` field) identifies this as an edit; `ConfirmContract` calls `Update` instead of `Add`
- The updated contract inherits the original `Id`, `HireDate`, and `Status` (Active or Paused)

---

## Steps

### Step 1: ScheduleMenu.cs (new — Screen 3 of hiring flow)
- [ ] Create `Dayswork/UI/ScheduleMenu.cs`
- Two large toggle buttons: **One-Time** (left) and **Recurring** (right), displayed side-by-side
- Selected option shows a highlighted frame (drawTextureBox with a colored tint)
- Fields pre-computed in constructor: `_draft.Schedule` → `_selectedOption`
- Clicking One-Time sets `_draft.Schedule = ContractSchedule.OneTime`; clicking Recurring sets Recurring; immediately re-renders
- "Next" button advances: calls `_onAdvance(draft)`; "Back" button: calls `_onBack(draft)`
- `receiveLeftClick`: check each button, update selection or call advance/back
- Gamepad wiring: D-pad left/right navigates between the two option buttons; `Buttons.B` = Back; `Buttons.A` = activates snapped component
- `populateClickableComponentList()`: adds option buttons + Next + Back
- `snapToDefaultClickableComponent()`: snaps to selected option button

Constructor signature:
```csharp
internal ScheduleMenu(
    ContractDraft draft,
    Action<ContractDraft> onAdvance,
    Action<ContractDraft> onBack)
```

- [x] Mark Step 1 complete in this plan [x]

---

### Step 2: ContractDraft + HiringFlowCoordinator (modify)

**ContractDraft.cs — add EditingId**:
- [ ] Add `public ContractId? EditingId { get; set; }` to `Dayswork/UI/ContractDraft.cs`

**HiringFlowCoordinator.cs — wire ScheduleMenu + implement edit flow**:
- [ ] Change `ShowZoneAndChest`'s `onAdvance` lambda from `d => ShowSummary(d)` to `d => ShowSchedule(d)`
- [ ] Add private method `ShowSchedule(ContractDraft draft)`:
  ```csharp
  private void ShowSchedule(ContractDraft draft)
  {
      Game1.activeClickableMenu = new ScheduleMenu(
          draft,
          onAdvance: d => ShowSummary(d),
          onBack:    d => ShowZoneAndChest(d));
  }
  ```
- [ ] Add `OpenManageFlow()` public method (called from BulletinBoardPatch):
  ```csharp
  public void OpenManageFlow()
  {
      Game1.activeClickableMenu = new ContractListMenu(_contractStore, _helper);
  }
  ```
- [ ] Implement `OpenEditFlow(ContractId existing)` (replace stub with real logic):
  - Load contract: `var contract = _contractStore.Get(existing);`
  - Build pre-filled draft:
    ```csharp
    var draft = new ContractDraft { EditingId = existing, Schedule = contract.Schedule };
    draft.EnabledTasks.UnionWith(contract.EnabledTasks);
    draft.Zones.AddRange(contract.Zones);
    foreach (var kvp in contract.TaskDestinations)
        draft.Destinations[kvp.Key] = kvp.Value;
    ```
  - Open: `ShowTaskSelection(draft);`
- [ ] Update `ConfirmContract` to detect edit vs. add:
  ```csharp
  private void ConfirmContract(ContractDraft draft, int deposit, int rate)
  {
      if (draft.EditingId.HasValue)
      {
          // Edit: update existing contract — no gold deduction
          var original = _contractStore.Get(draft.EditingId.Value);
          var updated = BuildContract(draft, deposit, rate) with
          {
              Id        = draft.EditingId.Value,
              Status    = original.Status,
              HireDate  = original.HireDate,
          };
          _contractStore.Update(draft.EditingId.Value, updated);
      }
      else
      {
          // New hire: deduct deposit
          if (Game1.player.Money < deposit)
          {
              Game1.addHUDMessage(new HUDMessage(
                  I18nHelper.Get("ui.error.cant_afford"), HUDMessage.error_type));
              return;
          }
          Game1.player.Money -= deposit;
          _contractStore.Add(BuildContract(draft, deposit, rate));
      }
      CloseFlow();
  }
  ```
- [x] Mark Step 2 complete in this plan [x]

---

### Step 3: BulletinBoardPatch (modify — add Manage Contracts button)
- [ ] Modify `Dayswork/Patches/BulletinBoardPatch.cs`
- Add static field: `private static ClickableComponent? _manageButton;`
- **Constructor_Postfix**: after creating `_hireButton`, also create `_manageButton`:
  - Positioned directly below `_hireButton` (Y += `_hireButton.bounds.Height + 8`)
  - Label: `I18nHelper.Get("bulletin.manage_contracts")`
  - `myID = 998` (one below `_hireButton`'s 999)
  - Wire D-pad: `_hireButton.downNeighborID = 998`; `_manageButton.upNeighborID = 999`
  - Add to `allClickableComponents`
- **Draw_Postfix**: draw `_manageButton` the same way as `_hireButton` (drawTextureBox + text + drawMouse)
- **ReceiveLeftClick_Postfix**: add check:
  ```csharp
  if (_manageButton is not null && _manageButton.bounds.Contains(x, y))
  {
      ModEntry.Coordinator.OpenManageFlow();
      return;
  }
  ```
- [x] Mark Step 3 complete in this plan [x]

---

### Step 4: ShiftOrchestrator + ModEntry (expose ActiveContractId)
- [ ] Modify `Dayswork/Orchestration/ShiftOrchestrator.cs`:
  - Add computed property: `public ContractId? ActiveContractId => _ctx?.ContractId;`
  - No other changes — `_ctx` is already null when no shift is running
- [ ] Modify `Dayswork/ModEntry.cs`:
  - Add static property: `internal static ShiftOrchestrator Orchestrator { get; private set; } = null!;`
  - In `Entry()`, after `var orchestrator = new ShiftOrchestrator(toolReader);`, add: `Orchestrator = orchestrator;`
- [x] Mark Step 4 complete in this plan [x]

---

### Step 5: ContractListMenu.cs (new — contract management UI)
- [ ] Create `Dayswork/UI/ContractListMenu.cs`

**Purpose**: Shows all Active and Paused contracts with Pause/Resume/Cancel/Edit actions.

**On open** (constructor):
- `_contracts = store.List().Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Paused).ToList()`
- Build `_rows` — one `ContractRow` per contract (a private record/struct with pre-formatted strings)
- Each row pre-formats: task names joined with ", ", schedule type label, status label "(Active)" or "(Paused)"
- Build `ClickableComponent` entries for each row

**ContractRow private record**:
```csharp
private sealed record ContractRow(
    Contract Contract,
    string TaskSummary,      // pre-formatted "Water Crops, Harvest Crops…"
    string ScheduleLabel,   // "One-time" or "Recurring"
    string StatusLabel);    // "(Active)" or "(Paused)"
```

**draw()**: Draws the panel (drawTextureBox), title ("Active Contracts"), "no contracts" hint if empty, then one row per contract. Each row shows task summary + schedule + status. Below each row: action buttons (Pause/Resume, Edit, Cancel). All text from `_rows` fields — no `ContractStore` calls in draw().

**receiveLeftClick**:
- Pause/Resume button: call `_store.Pause(id)` or `_store.Resume(id)`; then refresh `_contracts` and `_rows` (re-query store.List()) 
- Cancel button:
  ```csharp
  if (ModEntry.Orchestrator.ActiveContractId == contract.Id)
  {
      Game1.addHUDMessage(new HUDMessage(
          I18nHelper.Get("ui.contract_list.cancel_blocked"), HUDMessage.error_type));
      return;
  }
  _store.Cancel(contract.Id);
  // Refresh
  ```
- Edit button: `ModEntry.Coordinator.OpenEditFlow(contract.Id)` (opens full hiring flow pre-filled)
- Close button (upper-right X): `exitThisMenu()`

**Gamepad**: D-pad up/down between rows; A activates focused button; B closes menu.

Constructor signature:
```csharp
internal ContractListMenu(IContractStore store, IModHelper helper)
```

- [x] Mark Step 5 complete in this plan [x]

---

### Step 6: i18n/default.json (modify — add schedule + management keys)
- [ ] Add to `Dayswork/i18n/default.json`:
  ```json
  "bulletin.manage_contracts": "Manage Contracts",

  "ui.schedule.title": "Schedule",
  "ui.schedule.one_time": "One-Time",
  "ui.schedule.one_time_description": "Worker comes once, tomorrow morning",
  "ui.schedule.recurring": "Recurring",
  "ui.schedule.recurring_description": "Worker comes each morning automatically",
  "ui.schedule.confirm_btn": "Next",
  "ui.schedule.back_btn": "Back",

  "ui.contract_list.title": "Active Contracts",
  "ui.contract_list.no_contracts": "No active contracts",
  "ui.contract_list.pause": "Pause",
  "ui.contract_list.resume": "Resume",
  "ui.contract_list.cancel": "Cancel",
  "ui.contract_list.edit": "Edit",
  "ui.contract_list.paused_label": "(Paused)",
  "ui.contract_list.active_label": "(Active)",
  "ui.contract_list.cancel_blocked": "Cannot cancel — shift already started",
  "ui.contract_list.schedule_one_time": "One-time",
  "ui.contract_list.schedule_recurring": "Recurring"
  ```
- [x] Mark Step 6 complete in this plan [x]

---

### Step 7: ContractStoreStateTests.cs (new — FsCheck PBT-03 invariant)
- [ ] Create `Dayswork.Tests/Persistence/ContractStoreStateTests.cs`
- Adds one FsCheck property to formally satisfy PBT-03 obligation: Pause→Resume is a round-trip for randomly generated contracts

```csharp
// Property: Pause(id) followed by Resume(id) leaves contract with same Status as before Pause
[Property]
public Property PauseResume_IsRoundTrip()
{
    return Prop.ForAll(
        ContractGen.Contract().Filter(c => c.Status == ContractStatus.Active),
        contract =>
        {
            var store = new ContractStore(_ => { });
            store.Add(contract);
            store.Pause(contract.Id);
            store.Resume(contract.Id);
            return store.Get(contract.Id).Status == ContractStatus.Active;
        });
}
```

Note: The existing `ContractStoreTests` (unit tests) already cover all Pause/Resume/Cancel state transitions with explicit xUnit facts. This FsCheck property adds confidence that the behavior holds for the full space of generated `Contract` instances.

- [x] Mark Step 7 complete in this plan [x]

---

### Step 8: dotnet build + fix errors
- [ ] Run `dotnet build` from workspace root
- [ ] Fix any compilation errors
- [ ] Confirm 0 errors, 0 warnings
- [x] Mark Step 8 complete in this plan [x]

---

### Step 9: Code summary + state update
- [ ] Create `aidlc-docs/construction/u-12-hiring-ui-schedule/code/code-summary.md`
- [ ] Update `aidlc-docs/aidlc-state.md`: Current Stage → "U-12 — Code Generation complete, awaiting approval"
- [x] Mark Step 9 complete in this plan [x]

---

## Stories Covered

| Story | Coverage in U-12 |
|---|---|
| S-05 (contracts persist, schedule selectable) | `ScheduleMenu` enables one-time/recurring selection; `ContractDraft.Schedule` flows through to `BuildContract` then `ContractStore.Add` |
| S-12 (Pause/Cancel/Edit from bulletin board) | `ContractListMenu` wired to Pause/Resume/Cancel/Edit; `BulletinBoardPatch` adds Manage button; `OpenEditFlow` opens pre-filled 4-screen flow |

## Files Modified
- `Dayswork/UI/ContractDraft.cs` — add `EditingId`
- `Dayswork/UI/HiringFlowCoordinator.cs` — insert `ScheduleMenu`, implement `OpenEditFlow`/`OpenManageFlow`, update `ConfirmContract`
- `Dayswork/Patches/BulletinBoardPatch.cs` — add Manage Contracts button
- `Dayswork/Orchestration/ShiftOrchestrator.cs` — add `ActiveContractId` property
- `Dayswork/ModEntry.cs` — expose `Orchestrator` static
- `Dayswork/i18n/default.json` — add 19 new keys

## Files Created
- `Dayswork/UI/ScheduleMenu.cs`
- `Dayswork/UI/ContractListMenu.cs`
- `Dayswork.Tests/Persistence/ContractStoreStateTests.cs`
- `aidlc-docs/construction/u-12-hiring-ui-schedule/code/code-summary.md`
