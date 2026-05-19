# U-06 Persistence Core — NFR Requirements

**Unit**: U-06 — Persistence Core  
**Source**: Requirements §3 (NFR section) filtered to U-06's scope

---

## Applicable NFRs

### NFR-SAFE-03 — Save file safety (BLOCKING)

**Requirement**: The mod must not corrupt save files. All persisted data is namespaced via SMAPI's data API and tolerates being absent on first load.

**Applicability to U-06**: Primary and exclusive. U-06 owns both the in-memory store and the serializer — this NFR is the unit's primary safety obligation.

**Implementation constraints**:
- `SaveDataSerializer.Deserialize(null)` and `Deserialize("")` both return an empty list without throwing (first-load case)
- A completely unparseable JSON blob (not valid JSON) is caught at the envelope level and treated as absent — log warning, return empty list
- Schema version mismatch (`SchemaVersion > 1`) is caught and treated as unreadable — log warning, return empty list; data is NOT overwritten
- Individual malformed `ContractDtoV1` entries are skipped with a `LogLevel.Warn` message; the rest of the list is returned (Q9-A from functional design)
- `ContractStore.Hydrate()` clears-and-replaces atomically; the store is never partially populated after a `Hydrate` call

**Verifiable gate**: Unit test `Deserialize_NullInput_ReturnsEmptyList`, `Deserialize_EmptyString_ReturnsEmptyList`, `Deserialize_MissingSchemaVersion_ReturnsEmptyList`, `Deserialize_FutureSchemaVersion_ReturnsEmptyList`.

---

### NFR-MAINT-03 — Pure logic isolation (BLOCKING)

**Requirement**: Pure business-logic modules are separated from SMAPI/game-engine integration so they can be unit-tested without launching Stardew.

**Applicability to U-06**: Primary. `ContractStore` and `SaveDataSerializer` (and all DTO types) live in `Dayswork.Core/Persistence/` — the project that has no SMAPI or StardewValley assembly references.

**Implementation constraint**: No file in `Dayswork.Core/Persistence/` or `Dayswork.Core/Domain/` (for the new U-06 domain types) may reference:
- `StardewValley.*`
- `StardewModdingAPI.*`
- `Microsoft.Xna.*`
- `Harmony.*`

The only external dependency allowed is `Newtonsoft.Json` (already declared in `Dayswork.Core.csproj` per U-01).

**Verification**: `dotnet build Dayswork.Core` succeeds with 0 errors. The `.csproj` reference list is the enforcement gate.

---

### NFR-MAINT-01 / NFR-MAINT-02 — Test framework (BLOCKING)

**Applicability to U-06**: U-02 established xUnit + FsCheck.Xunit infrastructure. U-06 adds test files to the existing framework — no new packages or configuration.

**Test files** (in `Dayswork.Tests/Persistence/`):
- `ContractStoreTests.cs` — unit tests for all `ContractStore` operations
- `SaveDataSerializerTests.cs` — unit tests for NFR-SAFE-03 edge cases + PBT-02 round-trip property
- `Generators/ContractGen.cs` — FsCheck arbitraries (PBT-07 obligation)

---

## PBT Extension Obligations (Partial mode — enforced rules)

| Rule | Status | U-06 obligation |
|---|---|---|
| PBT-02 | **ENFORCED** | **Primary obligation** for U-06. `Deserialize(Serialize(contracts, modVersion))` must equal `contracts` for all valid contract lists. Must pass ≥ 1000 generated inputs via `ContractGen`. |
| PBT-03 | N/A | No numeric invariants in U-06. Persistence is about fidelity, not arithmetic. |
| PBT-07 | **ENFORCED** | `ContractGen.cs` in `Dayswork.Tests/Persistence/Generators/`. Composes `ZoneGen` (U-04), `ChestRefGen` (U-04 via ZoneGen), and generates all new U-06 domain types. Used by downstream units' PBTs (U-09, U-12). |
| PBT-08 | **ENFORCED** | Inherited from U-02. No additional work — `[Property]` attribute captures seed + shrunk input on failure automatically. |
| PBT-09 | **ENFORCED** | FsCheck.Xunit already installed in U-02. No additional work. |

---

## Non-applicable NFRs and rationale

| NFR | Status | Rationale |
|---|---|---|
| NFR-SAFE-01 (no items lost) | N/A | Item flow belongs to U-10 (ItemBuffer) and U-14 (DepositPlanner). U-06 has no items. |
| NFR-SAFE-02 (gold integrity) | N/A | Gold math belongs to U-05 (Pricing). U-06 stores `DepositAmount` and `HourlyRate` as plain `int` fields — no arithmetic. |
| NFR-SAFE-04 (no player items picked up) | N/A | NPC behavior — U-10. |
| NFR-PERF-01..03 (per-frame, tile scan, overlay) | N/A | `ContractStore` operations are O(n) over a list bounded by the number of player contracts (realistically ≤ 10). `Serialize`/`Deserialize` are called at most once per save-load event — never per frame. No performance optimization warranted. |
| NFR-COMPAT-01..04 | N/A | Platform compat established in U-01. `Dayswork.Core` adds no new assemblies beyond Newtonsoft.Json (already present). |
| NFR-UX-01..03 | N/A | No UI in U-06. |
| NFR-MAINT-04 (Harmony isolation) | N/A | No Harmony patches in U-06. |
| NFR-MAINT-05 (dotnet format) | Advisory | Standard .NET naming and formatting applied during Code Generation. |
| NFR-SEC-01 | N/A | Security Baseline extension disabled for this project. |
| NFR-ONBOARD-01..02 | Advisory | Just-in-time C# explanations (e.g., `record with` expressions, `JsonConverter` pattern) embedded in Code Generation plan. |
| NFR-DIST-01..03 | N/A | Cross-cutting; handled in U-01. |

---

## Performance note (informational)

`ContractStore.List()` iterates `Dictionary.Values` — O(n) where n is the number of contracts. A typical player will have 1–5 active contracts, making this negligible. `Serialize` and `Deserialize` are called once per `GameLoop.Saving` / `GameLoop.SaveLoaded` event respectively — not on the hot path. No performance optimization is warranted or planned for v1.
