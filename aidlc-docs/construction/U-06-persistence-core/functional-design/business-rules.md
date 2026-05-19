# U-06 Persistence Core — Business Rules

---

## Invariants

### INV-PERSIST-01 — Unique ContractId within store
Every `Contract` in `ContractStore` has a `ContractId` that is unique within the store. `Add` enforces this by throwing `InvalidOperationException` on duplicate Ids. The caller (`HiringFlowCoordinator` in U-09) is responsible for generating fresh Ids via `ContractId.New()`.

### INV-PERSIST-02 — Status transition graph is a DAG
`ContractStatus` transitions are:
- `Active` → `Paused` (via `Pause`)
- `Active` → `Cancelled` (via `Cancel`)
- `Paused` → `Active` (via `Resume`)
- `Paused` → `Cancelled` (via `Cancel`)
- `Cancelled` → *(terminal; no further transitions)*

No transition goes backwards from `Cancelled`. Attempting any method on a `Cancelled` contract throws `InvalidOperationException`.

### INV-PERSIST-03 — Hydrate is all-or-nothing
`ContractStore.Hydrate()` clears all existing data before inserting the provided list. Callers may never observe a partially-populated store after a `Hydrate` call. If `Hydrate` is called with an empty list, the store is left empty.

### INV-PERSIST-04 — Round-trip fidelity (PBT-02 primary obligation)
For any valid `IReadOnlyList<Contract> contracts`:
```
Deserialize(Serialize(contracts, modVersion)).SequenceEqual(contracts) == true
```
(using structural equality on `Contract` records). This is the primary PBT-02 obligation for U-06 and must pass for ≥1000 generated inputs.

### INV-PERSIST-05 — Missing or null save data yields empty store (NFR-SAFE-03)
If `Helper.Data.ReadSaveData` returns `null` (no `Dayswork.Contracts` segment written yet) or an empty string, `Deserialize` returns an empty list without throwing. This covers the first time a player hires a farmhand (no prior save exists) and the mod-removal scenario (FR-PERSIST-02).

### INV-PERSIST-06 — Malformed contracts are skipped, not fatal (Q9-A)
If any individual `ContractDtoV1` cannot be mapped to a valid `Contract` (missing required field, unrecognised enum value, unknown destination type), that contract is skipped with a SMAPI `LogLevel.Warn` message and the remaining contracts are returned. The entire store is never sacrificed due to one bad record.

### INV-PERSIST-07 — Future schema version is tolerated gracefully
If `DaysworkSaveDataV1.SchemaVersion > 1`, the deserializer logs a `LogLevel.Warn` and returns an empty list. It does not throw, does not crash, and does not overwrite the existing save data. (The data was written by a newer mod version and cannot be safely interpreted by this one.)

### INV-PERSIST-08 — TaskDestinations contains only output-producing tasks
`ClearGrass`, `WaterCrops`, `FeedAnimals`, and `PetAnimals` must never appear as keys in `Contract.TaskDestinations`. Hay routing (silo-first, then drop on ground) is handled by the shift logic in U-10, not by the destination map (FR-TASK-09). This invariant is enforced by the hiring UI (U-09, U-11) rather than by `ContractStore` itself — the store accepts whatever is given to it.

### INV-PERSIST-09 — HireDate and financial fields are write-once
`Contract.HireDate`, `Contract.DepositAmount`, and `Contract.HourlyRate` are set at creation and never changed by `ContractStore` operations. `Cancel`, `Pause`, and `Resume` only mutate `Status`. `Update` may technically change any field (it replaces the entire record), but callers (U-12 Edit flow) must preserve these three fields from the original contract.

### INV-PERSIST-10 — GameDate.Day is in [1, 28]
`GameDate.Day` must be in the range 1–28 inclusive, matching Stardew Valley's fixed season length. Values outside this range represent invalid game dates. Validation is the caller's responsibility (U-09 `ContractPersistenceAdapter` maps from SMAPI's `WorldDate`); `GameDate` itself does not validate.

---

## Validation rules

### VAL-PERSIST-01 — Contract.EnabledTasks must be non-empty
A `Contract` with zero enabled tasks is degenerate (the worker would have nothing to do). Enforced at hire time by U-09's `SummaryMenu` before calling `IContractStore.Add`. `ContractStore` itself does not re-validate this.

### VAL-PERSIST-02 — Contract.Zones must be non-empty
A `Contract` with zero zones is degenerate. Same enforcement point as VAL-PERSIST-01.

### VAL-PERSIST-03 — ContractDtoV1 required fields during deserialization
A `ContractDtoV1` is malformed and must be skipped (INV-PERSIST-06) if any of the following conditions hold:
- `Id` is null, empty, or not parseable as a `Guid`
- `EnabledTasks` is null
- `Zones` is null or empty
- `Schedule` is not one of `"OneTime"`, `"Recurring"`
- `Status` is not one of `"Active"`, `"Paused"`, `"Cancelled"`
- `HireDate` is null, or `HireDate.Season` is not a valid `Season` enum name, or `HireDate.Day` is outside [1, 28]
- `DepositAmount < 0`
- `HourlyRate < 0`

`TaskDestinations` is allowed to be null or empty (contracts with no output-producing tasks, or where the player assigned no destinations, are valid — their items simply mail).

---

## Error handling rules

### ERR-PERSIST-01 — ContractStore throws on not-found
`Get`, `Update`, `Cancel`, `Pause`, `Resume` all throw `KeyNotFoundException` if the `ContractId` is not present in the store. Callers are responsible for checking existence first if they need non-throwing behavior.

### ERR-PERSIST-02 — Serialize never returns null
`SaveDataSerializer.Serialize` returns a non-null, non-empty JSON string. It throws only if `JsonConvert.SerializeObject` itself throws (an unexpected internal error — not a domain rule violation).

### ERR-PERSIST-03 — Deserialize never throws on bad JSON (only on internal bugs)
All JSON parsing errors for well-formed but semantically invalid contracts are caught per-record (INV-PERSIST-06). Totally unparseable JSON (not valid JSON at all) causes the top-level `JsonConvert.DeserializeObject` to throw; this is caught at the `envelope` parse level and treated as a missing-data case (log warning, return empty list).

---

## NFR traceability

| NFR | Rule | How U-06 satisfies it |
|---|---|---|
| NFR-SAFE-03 | Tolerate absent save data | INV-PERSIST-05 — null/empty JSON → empty list |
| PBT-02 | Round-trip serialization fidelity | INV-PERSIST-04 — PBT property in `SaveDataSerializerTests` |
| PBT-07 | Shared FsCheck generators | `ContractGen.cs` added to `Dayswork.Tests/Persistence/Generators/` |
| NFR-MAINT-03 | No SMAPI refs in Core | All types in `Dayswork.Core`; no `StardewValley.*` or `StardewModdingAPI.*` references |
