# Code Summary — U-12 Hiring UI: Schedule + Edit/Pause/Cancel

## Build Result
**0 errors, 0 warnings.** Mod auto-deployed to `Mods/Dayswork/`.

## Files Created

| File | Purpose |
|---|---|
| `Dayswork/UI/ScheduleMenu.cs` | Screen 3 of hiring flow — one-time/recurring toggle cards; gamepad nav; updates `ContractDraft.Schedule` |
| `Dayswork/UI/ContractListMenu.cs` | Contract management UI — lists Active+Paused contracts; Pause/Resume/Cancel/Edit per row; cancel guard via `ModEntry.Orchestrator.ActiveContractId` |
| `Dayswork.Tests/Persistence/ContractStoreStateTests.cs` | FsCheck PBT-03 properties — 5 invariants: Pause→Resume round-trip, Pause sets Paused, Resume sets Active, Cancel sets Cancelled (contract remains in store), Pause→Cancel leaves Cancelled |

## Files Modified

| File | Change |
|---|---|
| `Dayswork/UI/ContractDraft.cs` | Added `EditingId ContractId?` field — set when editing an existing contract; `ConfirmContract` calls `Update` instead of `Add` when non-null |
| `Dayswork/UI/HiringFlowCoordinator.cs` | Inserted `ShowSchedule()` between ZoneAndChestMenu and SummaryMenu; implemented `OpenEditFlow()` (builds pre-filled draft, preserves Id/Status/HireDate on confirm); added `OpenManageFlow()` |
| `Dayswork/Patches/BulletinBoardPatch.cs` | Added "Manage Contracts" second button below "Hire a Farmhand"; D-pad chain (hire ↓ manage); click handler calls `ModEntry.Coordinator.OpenManageFlow()` |
| `Dayswork/Orchestration/ShiftOrchestrator.cs` | Added `ActiveContractId => _ctx?.ContractId` computed property — null when no shift running |
| `Dayswork/ModEntry.cs` | Added `internal static ShiftOrchestrator Orchestrator` static property; assigned in `Entry()` |
| `Dayswork/i18n/default.json` | Added 19 new keys: `bulletin.manage_contracts`, `ui.schedule.*` (7 keys), `ui.contract_list.*` (11 keys) |

## Key Design Decisions

- **ContractStore not changed**: `Pause/Resume/Cancel` were already fully implemented in U-06; U-12 only adds UI layer on top.
- **DTO not changed**: `ContractDtoV1.Status` already serializes Paused state as a string — no schema change needed.
- **Edit flow**: No gold adjustment on edit confirm. The updated contract inherits the original `Id`, `HireDate`, and `Status`. For recurring contracts, the scheduler will deduct the recalculated deposit next morning (FR-PAY-08). For one-time contracts, the original deposit remains.
- **Cancel guard**: Implemented in `ContractListMenu.TryCancel()` via `ModEntry.Orchestrator.ActiveContractId == contract.Id` check before calling `_store.Cancel()`. Shows `ui.contract_list.cancel_blocked` HUD message if blocked.
- **Hiring flow**: now 4 screens — TaskSelection → ZoneAndChest → **Schedule (new)** → Summary.

## Stories Implemented
- **S-05** (full): ScheduleMenu enables one-time/recurring selection; `ContractDraft.Schedule` flows through `BuildContract` to `ContractStore.Add`
- **S-12** (full): ContractListMenu with Pause/Resume/Cancel/Edit; BulletinBoardPatch "Manage Contracts" button; `OpenEditFlow` opens pre-filled 4-screen flow; cancel blocked mid-shift (FR-HIRE-15)
