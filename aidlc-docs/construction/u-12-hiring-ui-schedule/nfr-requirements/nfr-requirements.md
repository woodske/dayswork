# NFR Requirements — U-12 Hiring UI: Schedule + Edit/Pause/Cancel

## Unit
U-12 — Hiring UI: Schedule + Edit/Pause/Cancel (ScheduleMenu, ContractListMenu, ContractStore extensions)

**Depth**: Minimal — all NFRs derived from approved requirements.md and prior unit patterns. No clarifying questions needed.

---

## Applicable NFRs

### NFR-UX-01 — Full Gamepad Navigation
**Requirement**: The hiring UI is fully navigable with mouse/keyboard and gamepad (FR-HIRE-03, Q24).

**How it applies to U-12**:
- `ScheduleMenu` must override `receiveGamePadButton(Buttons b)`: `Buttons.B` → back to ZoneAndChestMenu; `Buttons.A` → confirm selection; D-pad navigates between the two schedule option buttons (one-time / recurring).
- `ContractListMenu` must override `receiveGamePadButton`: D-pad scrolls through contract rows; `Buttons.A` → opens the action menu for the focused contract (Pause/Resume/Edit/Cancel); `Buttons.B` → closes the list and returns to the bulletin board.
- Both menus populate `allClickableComponents` so D-pad snapping works via `setCurrentlySnappedComponentTo`.
- `currentlySnappedComponent` is set to the first meaningful element when each menu opens.

**Enforcement**: Play-test with controller before marking U-12 Definition of Done. All interactive elements in both menus reachable without mouse.

---

### NFR-UX-02 — i18n Routing
**Requirement**: All user-visible strings routed through SMAPI's i18n system (FR-CFG-02, Q23).

**How it applies to U-12**:
- All text rendered in `ScheduleMenu.draw()` and `ContractListMenu.draw()` must come from `I18nHelper.Get(key)`.
- New i18n keys added in this unit (added to `i18n/default.json`):
  - `ui.schedule.title` — "Schedule"
  - `ui.schedule.one_time` — "One-Time"
  - `ui.schedule.one_time_description` — "Worker comes once, next morning"
  - `ui.schedule.recurring` — "Recurring"
  - `ui.schedule.recurring_description` — "Worker comes each morning automatically"
  - `ui.schedule.confirm_btn` — "Next"
  - `ui.schedule.back_btn` — "Back"
  - `ui.contract_list.title` — "Active Contracts"
  - `ui.contract_list.no_contracts` — "No active contracts"
  - `ui.contract_list.pause` — "Pause"
  - `ui.contract_list.resume` — "Resume"
  - `ui.contract_list.cancel` — "Cancel"
  - `ui.contract_list.edit` — "Edit"
  - `ui.contract_list.paused_label` — "(Paused)"
  - `ui.contract_list.active_label` — "(Active)"
  - `ui.contract_list.cancel_blocked` — "Cannot cancel — shift already started"
  - `ui.contract_list.schedule_one_time` — "One-time"
  - `ui.contract_list.schedule_recurring` — "Recurring"

**Enforcement**: No hardcoded user-visible strings in any Mod-layer file for this unit. U-16 i18n lint test will catch regressions.

---

### NFR-PERF-01 — Frame Budget for draw()
**Requirement**: The hiring UI's per-frame hooks must not introduce visible frame drops. Stardew targets 60fps.

**How it applies to U-12**:
- `ScheduleMenu.draw()` is simple: two option buttons plus description text. No per-frame data queries needed. Render from pre-computed fields only.
- `ContractListMenu.draw()` renders a list of contracts. The contract list is fetched once when the menu opens (`ContractStore.List()`) and stored in a field `_contracts`. No `ContractStore` access per frame.
- Contract row rendering: each row is a pre-built `ClickableComponent` with a cached display string. No `IZoneGeometry` or game-state calls inside `draw()`.

**Enforcement**: `draw()` in both menus must contain only sprite/text rendering calls reading from pre-computed fields. `ContractStore.List()` is called once on menu open, not per frame.

---

### NFR-SAFE-03 — Save Data Integrity for ContractStore Extensions
**Requirement**: The mod must not corrupt save files. All persisted data is namespaced via SMAPI's data API and tolerates being absent on first load.

**How it applies to U-12**:
- `ContractStore.Pause(id)` and `ContractStore.Resume(id)` change the contract's `IsPaused` flag in-memory and call `_helper.Data.WriteSaveData` (via `ContractPersistenceAdapter`) to flush.
- `ContractDtoV1` must gain an `IsPaused` boolean field (default `false` on deserialization — backwards-compatible via Newtonsoft.Json default).
- `ContractStore.Cancel(id)` removes the contract from the store and flushes.
- Cancel must be guarded: if the contract's shift is active (worker is on-farm), `Cancel` returns a "blocked" result rather than removing the contract. Callers check this result before showing UI.
- The "active" check compares contract ID against `ShiftOrchestrator.ActiveContractId` (or equivalent property exposed for this purpose).

**Enforcement**: `ContractDtoV1.IsPaused` is added with `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)] public bool IsPaused { get; set; } = false;` to ensure old saves without the field deserialize cleanly. Round-trip PBT (see below) must cover paused state.

---

### NFR-MAINT-03 — SMAPI Integration Separation
**Requirement**: Pure business-logic modules are separated from SMAPI/game-engine integration (NFR-MAINT-03).

**How it applies to U-12**:
- `ContractStore` lives in `Dayswork.Core` — the `Pause(id)` and `Resume(id)` methods are pure in-memory state changes. They must not call `IModHelper` or `Game1` directly.
- Persistence flush on state change is triggered by the caller (`ContractPersistenceAdapter` in the Mod layer), not by `ContractStore` itself. This preserves Core/Mod separation established in U-06.
- `ScheduleMenu`, `ContractListMenu`, and `BulletinBoardPatch` extension all live in the Mod layer and may reference SMAPI/SV types freely.

**Enforcement**: `Dayswork.Core.csproj` must still have zero SMAPI/SV references after U-12 (build-verified).

---

### NFR-MAINT-04 — Harmony Patches Isolation
**Requirement**: Harmony patches are isolated in `Dayswork.Patches` namespace (NFR-MAINT-04).

**How it applies to U-12**:
- `BulletinBoardPatch` is extended this unit to render the contract-list entry alongside "Hire a Farmhand". The extension stays in the same `BulletinBoardPatch` class in `Dayswork/Patches/`.
- No new Harmony patch classes are introduced in U-12.

**Enforcement**: No Harmony `[HarmonyPatch]` attributes outside `Dayswork/Patches/`.

---

### PBT Compliance (Partial Mode — Blocking Rules)

#### PBT-02 — Round-Trip Serialization (BLOCKING)
**Applies**: `ContractDtoV1` gains an `IsPaused` field. The existing round-trip PBT from U-06 (`ContractGen` + `deserialize(serialize(contract)) == contract`) must be updated to cover contracts with `IsPaused = true`.

**Action**: Add a `ContractGen` variant or modify the existing generator to include `IsPaused` as a boolean Arb, confirming the field survives the serialize/deserialize cycle.

#### PBT-03 — Invariants (BLOCKING)
**Applies**: `ContractStore.Pause` / `Resume` / `Cancel` state transitions.

**Invariants to test**:
- `Pause(id)` on an Active contract → store shows contract as Paused; `Resume(id)` → store shows it Active again; net: no data lost
- `Pause(id)` on an already-Paused contract → idempotent (still Paused, no error)
- `Resume(id)` on an Active contract → idempotent (still Active, no error)
- `Cancel(id)` on a contract not currently in active shift → contract is removed from store; subsequent `List()` does not contain that id
- `Cancel(id)` on a contract in active shift → returns `CancelResult.Blocked`; contract remains in store unchanged

**Test file**: `Dayswork.Tests/Persistence/ContractStoreStateTests.cs`

---

## N/A NFRs

| NFR | Rationale |
|---|---|
| NFR-SAFE-01 | No items collected or moved in this unit (hire-time UI + contract management only) |
| NFR-SAFE-02 | No gold transactions in this unit |
| NFR-SAFE-04 | No item pickup by worker |
| NFR-PERF-02 | No tile scanning for task queue |
| NFR-PERF-03 | No zone overlay rendering |
| NFR-UX-03 | Zone draw mode established in U-11; not touched by U-12 |
| NFR-MAINT-01 | xUnit project established in U-02 |
| NFR-MAINT-02 | FsCheck established; new PBT tests added per PBT-02/PBT-03 above |
| NFR-MAINT-05 | `dotnet format` applies always; no design decisions |
| NFR-COMPAT-01 | Compatibility docs — README concern |
| NFR-COMPAT-02 | Farm-type support — no farm-map interaction in this unit |
| NFR-COMPAT-03 | Multiplayer guard established in U-08 |
| NFR-COMPAT-04 | No new required dependencies |
| Security Baseline | Disabled project-wide (Q28) |
