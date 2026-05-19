# U-06 Persistence Core — NFR Design Patterns

**Unit**: U-06 — Persistence Core  
**NFRs addressed**: NFR-SAFE-03, NFR-MAINT-03, PBT-02, PBT-07

---

## Pattern 1: Exception Barrier with Skip-and-Warn

**Addresses**: NFR-SAFE-03 — tolerate absent/malformed save data; Q9-A decision

### Problem

`Deserialize` processes a list of `ContractDtoV1` entries from a JSON array. Any single entry may be malformed — missing fields, an unrecognised `DestinationKey` type, or an invalid enum string (e.g., a value written by a future mod version). If any single entry throws during mapping, a naive implementation loses all contracts after that point (uncaught exception) or loses all contracts (fail-fast strategy).

### Solution: Per-record exception barrier

A `try/catch` wraps each individual `MapDtoToDomain(dto)` call. On any exception, the malformed contract is skipped; a `LogLevel.Warn` message is written to the SMAPI console with the contract's `Id` and the exception message; processing continues with the next entry.

```
results = new List<Contract>()
for each dto in envelope.Contracts:
    try:
        results.Add(MapDtoToDomain(dto))
    catch Exception ex:
        Monitor.Log($"Skipping contract {dto.Id}: {ex.Message}", LogLevel.Warn)

return results.AsReadOnly()
```

### Scope of the barrier

The barrier isolates **per-contract** failures only. Failures at the envelope level (completely unparseable JSON, null envelope) are caught separately and result in an empty list — also without throwing (see Pattern 2).

The barrier does **not** swallow exceptions from `ContractStore` operations or from `Serialize` — those indicate programming errors, not data-compatibility issues.

### Invariants maintained

- A save file written by a future mod version that adds new fields: old fields round-trip cleanly; new fields are ignored by Newtonsoft's default missing-member handling.
- A save file with one corrupt entry: the remaining N−1 contracts are loaded normally.
- A save file with all corrupt entries: the store starts empty (same as no save data).

---

## Pattern 2: Null-Safe Empty Result

**Addresses**: NFR-SAFE-03 — "tolerates being absent on first load"; FR-PERSIST-02

### Problem

`Helper.Data.ReadSaveData<string>(key)` returns `null` when the key has never been written (first play session, or after uninstalling and reinstalling the mod). A naive `JsonConvert.DeserializeObject` call on `null` throws `ArgumentNullException`.

### Solution: Early-return empty list at three guard points

```
// Guard 1: null or empty input
if (string.IsNullOrEmpty(json))
    return ImmutableList<Contract>.Empty

// Guard 2: null envelope (valid JSON but maps to null)
var envelope = JsonConvert.DeserializeObject<DaysworkSaveDataV1>(json)
if (envelope is null)
    Monitor.Log("...", Warn); return empty

// Guard 3: unrecognised schema version
if (envelope.SchemaVersion > 1)
    Monitor.Log("...", Warn); return empty
```

Each guard returns an empty list and logs at most a warning — no exception propagates to the caller. The `ContractPersistenceAdapter` in U-09 then calls `ContractStore.Hydrate([])`, leaving the store empty and ready for new hires.

### Why three guards instead of one

| Guard | What it catches |
|---|---|
| Null/empty string | First-load; mod removed and reinstalled |
| Null envelope | Valid JSON that Newtonsoft maps to null (e.g., `"null"` string) |
| Future schema version | Save written by a newer mod version — uninterpretable |

---

## Pattern 3: Versioned Envelope

**Addresses**: NFR-SAFE-03 (forward compatibility); Q6-C decision

### Problem

The save data format will evolve as the mod gains features. A player who upgrades the mod mid-save must not lose contracts or crash. A player who *downgrades* the mod must get a graceful warning rather than corrupted data.

### Solution: Top-level envelope with `SchemaVersion` and `ModVersion`

```json
{
  "SchemaVersion": 1,
  "ModVersion": "0.1.0",
  "Contracts": [ ... ]
}
```

- **`SchemaVersion`**: Integer. The current version is `1`. Future breaking changes increment this. Deserializer rejects `SchemaVersion > 1` (Pattern 2, Guard 3).
- **`ModVersion`**: String. Records which mod version wrote the data. Future migration code can use this to apply field-level patches before the main deserialization. Not inspected by v1 code — written for future use.

### Migration path (v1 → v2, hypothetical)

When a future v2 mod version adds a required field to `ContractDtoV1`:
1. Define `ContractDtoV2` with the new field.
2. In the deserializer, branch on `SchemaVersion`: `1` → migrate (fill the new field with a default), `2` → deserialize natively.
3. `ModVersion` provides additional context if the migration needs to handle a partial-update scenario.

v1 mod encounters a `SchemaVersion: 2` file → Guard 3 fires → empty store + warning. The player must upgrade the mod to read their data. No corruption.

---

## Pattern 4: Immutable Domain Record + `with` Expressions for Status Transitions

**Addresses**: NFR-MAINT-03 (pure, testable logic); Q3-A (3-state status)

### Problem

`ContractStore` must support status transitions (`Active` → `Paused`, `Paused` → `Active`, any → `Cancelled`) without exposing mutable state. Mutable domain objects invite bugs where callers hold a stale reference to the pre-transition contract.

### Solution: `sealed record` + `with` on every mutation

`Contract` is a `sealed record`. `ContractStore` never modifies a stored `Contract` in-place. Every status-changing operation replaces the entry in the internal dictionary with a new record:

```csharp
// ContractStore.Pause():
var existing = _contracts[id];          // fetch immutable record
_contracts[id] = existing with          // replace with new record
{
    Status = ContractStatus.Paused
};
```

**Benefits**:
- Callers that hold a reference to the old `Contract` see the pre-transition status — they cannot observe a half-mutated object.
- `Contract` record equality is structural: two records with the same field values are equal. PBT-02 round-trip assertions use `==` directly.
- The `with` expression is the idiomatic C# approach to non-destructive mutation of records — it is what the language feature was designed for.

### Status transition enforcement

The status DAG (Active → Paused → Active, any → Cancelled; Cancelled terminal) is enforced by guard clauses at the top of each method before the `with` expression is applied. Invalid transitions throw `InvalidOperationException`.

---

## Pattern 5: Explicit DTO Mapping Layer

**Addresses**: NFR-MAINT-03 (no hidden magic); Q1-B (no `StringEnumConverter`)

### Problem

Attribute-driven serialization (e.g., `[JsonConverter(typeof(StringEnumConverter))]` on properties, or `TypeNameHandling.Auto`) creates invisible behaviour — a future developer reading the DTO class cannot tell how it serializes without knowing Newtonsoft's conventions. It also makes the format harder to control precisely.

### Solution: Explicit two-way mapping methods in `SaveDataSerializer`

`SaveDataSerializer` contains two private mapping methods:

```
Contract   MapDomainToDto(Contract) → ContractDtoV1
Contract   MapDtoToDomain(ContractDtoV1) → Contract
```

Each field mapping is a single, readable line:

```csharp
// Domain → DTO (serialize direction):
dto.Status   = contract.Status.ToString();
dto.Schedule = contract.Schedule.ToString();

// DTO → Domain (deserialize direction):
var status   = Enum.Parse<ContractStatus>(dto.Status);
var schedule = Enum.Parse<ContractSchedule>(dto.Schedule);
```

**What Newtonsoft handles**: Serializing `DaysworkSaveDataV1` (a plain class with `int`, `string`, and `List<ContractDtoV1>` fields). Newtonsoft's default behaviour is correct for all these types — no custom converter needed.

**What the mapping layer handles**: All domain-specific conversions (ContractId ↔ Guid string, GameDate ↔ GameDateDtoV1, DestinationKey ↔ DestinationDtoV1). None of these appear as C# enum or polymorphic types in the DTO classes — Newtonsoft sees only strings, ints, and plain objects.

### DestinationKey dispatch

The most complex mapping is `DestinationDtoV1` → `DestinationKey`. It is a plain `switch` expression:

```csharp
DestinationKey MapDestination(DestinationDtoV1 dto) => dto.Type switch
{
    "Chest"       => new ChestDestination(new ChestRef(dto.LocationName!,
                         new TileCoord(dto.X!.Value, dto.Y!.Value))),
    "ShippingBin" => ShippingBinDestination.Instance,
    "Mail"        => MailDestination.Instance,
    _             => throw new JsonException($"Unknown destination type: '{dto.Type}'"),
};
```

The `_` arm throw is caught by Pattern 1's exception barrier — skips the contract, logs a warning.

---

## Pattern 6: Atomic Hydration

**Addresses**: NFR-SAFE-03 (no partial state); Q2-A decision

### Problem

On `SaveLoaded`, the `ContractPersistenceAdapter` calls `Hydrate(contracts)` with the deserialized list. If `Hydrate` inserts contracts one-by-one and throws partway through (e.g., duplicate Id in save data), the store would be partially populated — some contracts present, others missing.

### Solution: Clear-first, then insert all

```csharp
public void Hydrate(IReadOnlyList<Contract> contracts)
{
    _contracts.Clear();                    // atomic wipe
    foreach (var contract in contracts)
    {
        if (_contracts.ContainsKey(contract.Id))
        {
            Monitor.Log($"Duplicate ContractId {contract.Id} in save data — skipping.", Warn);
            continue;
        }
        _contracts[contract.Id] = contract;
    }
}
```

After `Hydrate` completes, the store's content exactly matches the input list (minus any duplicates, which are logged and skipped). There is no intermediate state where the store has old contracts mixed with new ones.

**SMAPI event guarantee**: `GameLoop.SaveLoaded` fires exactly once per game session. `Hydrate` is called exactly once. The "store must be empty" precondition holds by construction.

---

## Summary: patterns × NFRs

| Pattern | NFR-SAFE-03 | NFR-MAINT-03 | PBT-02 | PBT-07 |
|---|---|---|---|---|
| Exception Barrier | Primary | Supporting | Enabled by | — |
| Null-Safe Empty Result | Primary | — | — | — |
| Versioned Envelope | Primary | — | — | — |
| Immutable Record + `with` | — | Primary | Enabled by | — |
| Explicit DTO Mapping Layer | Supporting | Primary | — | — |
| Atomic Hydration | Primary | — | — | — |
