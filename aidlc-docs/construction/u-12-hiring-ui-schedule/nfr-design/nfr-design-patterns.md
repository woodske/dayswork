# NFR Design Patterns — U-12 Hiring UI: Schedule + Edit/Pause/Cancel

## Pattern 1: Pre-Compute on Open
**Addresses**: NFR-PERF-01 (draw() frame budget)

**Problem**: `ScheduleMenu.draw()` and `ContractListMenu.draw()` run every frame while open. Querying `ContractStore.List()` or formatting display strings inside `draw()` wastes CPU.

**Solution**: All data needed for rendering is fetched once when the menu opens and stored in fields. The render path reads those fields only.

**ScheduleMenu** (simple — two static options):
- No per-frame data query needed. The selected schedule type is a single `ScheduleType` enum field updated on button click.

**ContractListMenu** state-change triggers:

| Trigger | Action | Fields Updated |
|---|---|---|
| Menu opens | `ContractStore.List()` | `_contracts` (List\<Contract\>) |
| Menu opens | Format each contract's display string | `_contractRows` (List\<ContractRow\>) |
| Pause/Resume/Cancel executed | Re-query `ContractStore.List()` and reformat | `_contracts`, `_contractRows` |

**ContractRow** is a private record or struct:
```csharp
private record ContractRow(
    Contract Contract,
    ClickableTextureComponent ActionBtn,  // pause/resume
    ClickableTextureComponent CancelBtn,
    ClickableTextureComponent EditBtn,
    string DisplayLabel,  // pre-formatted: "Tasks + schedule type"
    string StatusLabel    // "(Active)" or "(Paused)"
);
```

**Invariant**: `draw()` iterates `_contractRows` reading pre-formatted strings and ClickableComponents only. No `ContractStore`, `I18nHelper`, or string formatting in the render path.

---

## Pattern 2: State Transition Result Enum
**Addresses**: NFR-SAFE-03 (safe state management for Pause/Resume/Cancel)

**Problem**: `ContractStore.Cancel(id)` may be called on an active-shift contract (FR-HIRE-15). Throwing an exception would be unergonomic for the caller. Silently ignoring would hide bugs.

**Solution**: `ContractStore` operations that can be blocked return a result enum, not void or bool.

```csharp
// Dayswork.Core/Persistence/ContractOperationResult.cs
public enum ContractOperationResult
{
    Success,
    NotFound,
    Blocked  // cancel attempted during active shift
}
```

**Method signatures on `ContractStore`** (added in this unit):

```csharp
public ContractOperationResult Pause(string contractId);
public ContractOperationResult Resume(string contractId);
public ContractOperationResult Cancel(string contractId, bool isShiftActive);
```

**Caller pattern in `ContractListMenu`**:
```csharp
var result = _store.Cancel(contract.Id, isShiftActive: _orchestrator.IsShiftActive(contract.Id));
if (result == ContractOperationResult.Blocked)
{
    // Show message: I18nHelper.Get("ui.contract_list.cancel_blocked")
    return;
}
// Re-query and refresh _contractRows
```

**Why `isShiftActive` is passed in** rather than queried inside `ContractStore`: Core must not reference Mod-layer services. The caller (Mod layer) resolves the active-shift check from `ShiftOrchestrator` and passes it as a plain bool. This keeps Core pure.

---

## Pattern 3: Backward-Compatible Save Field
**Addresses**: NFR-SAFE-03 (save data integrity for `IsPaused`)

**Problem**: Existing saves (pre-U-12) have no `IsPaused` key in the JSON. Deserializing without special handling would leave `IsPaused` at `false` (C# default) — which is correct behavior — but only if Newtonsoft.Json doesn't throw on a missing field.

**Solution**: Decorate `ContractDtoV1.IsPaused` with Newtonsoft.Json's `DefaultValueHandling.Populate` and set the C# default to `false`.

```csharp
// In ContractDtoV1 (Dayswork.Core/Persistence/Dto/ContractDtoV1.cs)
[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
public bool IsPaused { get; set; } = false;
```

`DefaultValueHandling.Populate` ensures that when the JSON key is missing entirely, the property is set to the C# default (`false`) rather than being left uninitialized. This matches the pre-U-12 behavior (no contract was paused) and is the established pattern for additive save schema changes (same approach as any new field on a DTO).

**Round-trip PBT update** (PBT-02 obligation): The `ContractGen` FsCheck generator must include `IsPaused` as a random bool. The existing `serialize → deserialize → equality` property test in `Dayswork.Tests/Persistence/` must cover both `IsPaused = true` and `IsPaused = false` cases, which FsCheck will generate automatically once the field is on the generator.

---

## Pattern 4: Cancel Guard (Active-Shift Check)
**Addresses**: FR-HIRE-15 (cancel unavailable during active shift), NFR-SAFE-03

**Problem**: The player can open the bulletin board while a worker is on the farm. The Cancel button must be disabled (or show a friendly error) in that case.

**Solution**: `ShiftOrchestrator` exposes a read-only property `ActiveContractId` (or `null` if no shift is running). `ContractListMenu` reads this once on open and once after each action.

```csharp
// ShiftOrchestrator (extended this unit):
public string? ActiveContractId { get; private set; }
// Set to contract.Id when shift starts; null when shift ends.
```

**ContractListMenu** uses `ActiveContractId` to:
1. Render the Cancel button as greyed-out for the active contract
2. Show `ui.contract_list.cancel_blocked` text if the player clicks it anyway
3. Not disable Edit or Pause — those remain available even mid-shift (pausing takes effect next morning)

**Why Edit and Pause are not blocked mid-shift**: FR-HIRE-12 says recurring contracts can be paused or edited "any time before 6am". By the time the player has opened the bulletin board during the shift, the day has already started. Pausing mid-day takes effect the following morning (the current shift runs to completion). Edit mid-shift is also allowed — changes apply next morning. Only Cancel is blocked (FR-HIRE-15).

---

## Pattern 5: PBT Invariant Tests for ContractStore State Transitions
**Addresses**: PBT-03 (blocking rule)

**Problem**: `Pause`, `Resume`, and `Cancel` introduce state transitions on `ContractStore`. Without invariant tests, regression risk on these methods is high.

**Solution**: FsCheck property tests in `Dayswork.Tests/Persistence/ContractStoreStateTests.cs`.

**Properties to implement**:

```csharp
// Property 1: Pause → Resume is a round-trip (net: no data change)
[Property]
public Property PauseResume_IsRoundTrip()
{
    return Prop.ForAll(ContractGen.ActiveContract(), contract =>
    {
        var store = new ContractStore();
        store.Add(contract);
        store.Pause(contract.Id);
        store.Resume(contract.Id);
        return store.Get(contract.Id).IsPaused == false;
    });
}

// Property 2: Pause is idempotent
[Property]
public Property Pause_IsIdempotent()
{
    return Prop.ForAll(ContractGen.ActiveContract(), contract =>
    {
        var store = new ContractStore();
        store.Add(contract);
        store.Pause(contract.Id);
        store.Pause(contract.Id); // second call
        return store.Get(contract.Id).IsPaused == true;
    });
}

// Property 3: Resume on Active is idempotent
[Property]
public Property Resume_OnActive_IsIdempotent()
{
    return Prop.ForAll(ContractGen.ActiveContract(), contract =>
    {
        var store = new ContractStore();
        store.Add(contract);
        // contract is active (not paused) — resume should be no-op
        store.Resume(contract.Id);
        return store.Get(contract.Id).IsPaused == false;
    });
}

// Property 4: Cancel removes the contract from the store
[Property]
public Property Cancel_RemovesContract()
{
    return Prop.ForAll(ContractGen.ActiveContract(), contract =>
    {
        var store = new ContractStore();
        store.Add(contract);
        var result = store.Cancel(contract.Id, isShiftActive: false);
        return result == ContractOperationResult.Success
            && store.Get(contract.Id) == null;
    });
}

// Property 5: Cancel during active shift returns Blocked
[Property]
public Property Cancel_DuringActiveShift_IsBlocked()
{
    return Prop.ForAll(ContractGen.ActiveContract(), contract =>
    {
        var store = new ContractStore();
        store.Add(contract);
        var result = store.Cancel(contract.Id, isShiftActive: true);
        return result == ContractOperationResult.Blocked
            && store.Get(contract.Id) != null; // contract unchanged
    });
}
```

**Generator update** (`ContractGen.cs`): Add `IsPaused` as `Arb.From(Gen.Elements(true, false))` in the existing `ContractGen` Arbitrary. Add a named variant `ContractGen.ActiveContract()` that generates contracts with `IsPaused = false` for the Pause/Resume tests.

---

## Pattern 6: Constructor Injection
**Addresses**: NFR-MAINT-03 (SMAPI separation)

**Problem**: `ContractListMenu` needs `ContractStore` and `ShiftOrchestrator`. Newing these inside the menu couples it to concrete classes and prevents testing.

**Solution**: Both are injected via constructor from `HiringFlowCoordinator`. `ContractListMenu` receives interfaces:

```csharp
public ContractListMenu(
    IContractStore store,
    ShiftOrchestrator orchestrator,
    IModHelper helper)
```

`HiringFlowCoordinator` already holds these singletons (wired in `ModEntry`). It passes them through when opening `ContractListMenu` from `BulletinBoardPatch`.

**`BulletinBoardPatch` extension wiring**: `BulletinBoardPatch` is a static class with a Harmony postfix. It needs access to `HiringFlowCoordinator` to open `ContractListMenu`. This access is via a static reference set during `ModEntry.Entry()` — same pattern used to open the hiring flow from the bulletin board (established in U-09).

---

## Resilience Assessment

| Concern | Handling |
|---|---|
| `ContractStore.List()` returns empty | `ContractListMenu` shows `ui.contract_list.no_contracts` label; valid state |
| `Cancel` called on unknown ID | Returns `ContractOperationResult.NotFound`; caller logs and no-ops |
| `Pause` called on unknown ID | Returns `ContractOperationResult.NotFound`; caller logs and no-ops |
| Edit opens full flow with no tasks selected | Not possible — ContractDraft is pre-filled from existing contract values |
| Player opens ContractListMenu during 6am spawn window | `ShiftOrchestrator.ActiveContractId` is set before `TimeChanged` fires for 6am; Cancel is correctly blocked |

## Scalability Assessment
N/A — single-player SMAPI mod, no concurrency, no distributed state.

## Security Assessment
N/A — Security Baseline disabled (Q28).
