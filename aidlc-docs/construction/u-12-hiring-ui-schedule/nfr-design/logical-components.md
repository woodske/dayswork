# Logical Components — U-12 Hiring UI: Schedule + Edit/Pause/Cancel

## No new infrastructure components

U-12 introduces no caches, queues, circuit breakers, or background services. All logic runs on the game's main thread inside SMAPI event callbacks. The logical structure below describes how the new and extended components fit into the existing architecture.

---

## Component Map

### New Components (owned by U-12)

| Component | Layer | Role |
|---|---|---|
| `ScheduleMenu` (M-06) | Mod / UI | Screen 3 of the hiring flow. Two toggle buttons: one-time / recurring. Updates `ContractDraft.ScheduleType`. Inserted between `ZoneAndChestMenu` and `SummaryMenu` by `HiringFlowCoordinator`. |
| `ContractListMenu` | Mod / UI | Opened from `BulletinBoardPatch` when the player selects "Manage Contracts". Lists all active (and paused) contracts with Pause/Resume/Cancel/Edit actions. |
| `ContractOperationResult` | Core | Enum: Success / NotFound / Blocked. Returned by `ContractStore.Pause`, `Resume`, `Cancel`. Keeps Core free of exception-based control flow for expected business conditions. |

### Extended Components (modified by U-12)

| Component | Layer | Extension |
|---|---|---|
| `ContractStore` (C-12) | Core | Adds `Pause(id)`, `Resume(id)`, `Cancel(id, isShiftActive)` methods. Adds `IsPaused` to in-memory `Contract` record and `ContractDtoV1`. |
| `ContractDtoV1` | Core | Gains `bool IsPaused = false` with `DefaultValueHandling.Populate` for backwards-compat. |
| `HiringFlowCoordinator` (M-03) | Mod | Inserts `ScheduleMenu` into the flow between `ZoneAndChestMenu` and `SummaryMenu`. Constructs `ScheduleMenu` with the shared `ContractDraft`. |
| `BulletinBoardPatch` (M-02) | Mod / Patches | Adds rendering of a "Manage Contracts" entry in the bulletin board menu. On click, opens `ContractListMenu` via a static coordinator reference (pattern from U-09). |
| `ShiftOrchestrator` (M-12) | Mod | Exposes `ActiveContractId` property (null when no shift running). Set when a shift starts; cleared when a shift ends. Used by `ContractListMenu` to determine Cancel eligibility. |
| `ContractGen` | Tests | Updated to include `IsPaused` as an Arb bool. Adds `ContractGen.ActiveContract()` variant (IsPaused = false). |

---

## Data Flow: Schedule Selection

```
Player opens bulletin board
    → BulletinBoardPatch shows "Hire a Farmhand"
    → HiringFlowCoordinator.BeginHiringFlow()
        → TaskSelectionMenu (Screen 1)
        → ZoneAndChestMenu (Screen 2)
        → ScheduleMenu (Screen 3) ← NEW THIS UNIT
            Player selects: one-time | recurring
            ContractDraft.ScheduleType = selected
        → SummaryMenu (Screen 4)
        → Confirm → ContractPersistenceAdapter.Save(contract)
```

---

## Data Flow: Contract Management

```
Player opens bulletin board
    → BulletinBoardPatch shows "Manage Contracts" ← NEW THIS UNIT
    → ContractListMenu opens
        Load: ContractStore.List() → _contracts
        Load: ShiftOrchestrator.ActiveContractId → _activeId
        Render: ContractRow per contract (pre-formatted)

    Player clicks Pause:
        ContractStore.Pause(id) → Success/NotFound
        ContractPersistenceAdapter.Flush()
        Re-query ContractStore.List() → refresh _contractRows

    Player clicks Resume:
        ContractStore.Resume(id) → Success/NotFound
        ContractPersistenceAdapter.Flush()
        Refresh _contractRows

    Player clicks Cancel:
        isShiftActive = (ActiveContractId == id)
        ContractStore.Cancel(id, isShiftActive)
            → Blocked: show cancel_blocked message, no change
            → Success: Flush + Refresh
    
    Player clicks Edit:
        Build ContractDraft pre-filled from existing contract
        HiringFlowCoordinator.BeginEditFlow(contractDraft)
            → Opens TaskSelectionMenu (Screen 1) with pre-filled toggles
            → Full 4-screen flow; on confirm, replaces existing contract in store
```

---

## No New Infrastructure

| Infrastructure Type | Status | Rationale |
|---|---|---|
| Message queue | N/A | In-process method calls only |
| Cache layer | N/A | `_contracts` is a menu-lifetime field, not a cross-session cache |
| Background thread | N/A | All SMAPI callbacks run on main thread |
| Circuit breaker | N/A | No remote calls |
| Retry logic | N/A | All operations are in-process; no transient failure modes |
