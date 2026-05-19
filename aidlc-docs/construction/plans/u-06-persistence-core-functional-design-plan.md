# U-06 Persistence Core — Functional Design Plan

## Unit Summary
**U-06** introduces `Contract` (the core domain record), `ContractStore` (C-12, in-memory collection), and `SaveDataSerializer` (C-13, versioned JSON round-trip). It is the persistence foundation that every later unit — from U-09's hiring flow to U-15's recurring lifecycle — writes into and reads from.

**Stories addressed**: S-05 foundation (contracts survive save/load), S-19 (PBT-02 primary obligation).  
**Key NFR**: NFR-SAFE-03 (deserializing a save with missing `Dayswork.Contracts` segment yields an empty store, not a crash).

---

## Plan Steps

- [x] Step 1: Analyze unit context (done — see below)
- [x] Step 2: Create this plan file
- [x] Step 3: Ask clarifying questions (questions below)
- [x] Step 4: Collect and analyze answers
- [x] Step 5: Generate functional design artifacts
  - [x] domain-entities.md
  - [x] business-logic-model.md
  - [x] business-rules.md
- [x] Step 6: Present completion message and await approval

---

## Context Analysis

### Types available from prior units

| Type | Defined in | Role in U-06 |
|---|---|---|
| `TaskKind` (enum) | U-03 `Domain/TaskKind.cs` | Identifies enabled tasks stored in Contract |
| `IConfigSnapshot` | U-03 `Config/IConfigSnapshot.cs` | Not used directly in U-06 — no pricing logic here |
| `Zone` | U-04 `Domain/Zone.cs` | Contract stores which zones the farmhand should work |
| `TileCoord` | U-04 `Domain/TileCoord.cs` | Embedded in Zone and ChestRef |
| `ChestRef` | U-04 `Domain/ChestRef.cs` | Embedded in DestinationKey.ChestDestination |
| `DestinationKey` (hierarchy) | U-04 `Domain/DestinationKey.cs` | Per-task output routing stored in Contract |

### Components owned by U-06

| Component | Files |
|---|---|
| `Contract` (new domain record) | `Dayswork.Core/Domain/Contract.cs` |
| `IContractStore` / `ContractStore` (C-12) | `Dayswork.Core/Persistence/IContractStore.cs`, `ContractStore.cs` |
| `ISaveDataSerializer` / `SaveDataSerializer` (C-13) | `Dayswork.Core/Persistence/ISaveDataSerializer.cs`, `SaveDataSerializer.cs` |
| Versioned DTOs | `Dayswork.Core/Persistence/Dto/ContractDtoV1.cs`, `DaysworkSaveDataV1.cs` |

### API surface already decided in Application Design

```csharp
// IContractStore (C-12)
ContractId Add(Contract contract);
Contract Get(ContractId id);
void Update(ContractId id, Contract updated);
void Cancel(ContractId id);
void Pause(ContractId id);     // wired by U-12; method shell created here
void Resume(ContractId id);    // wired by U-12; method shell created here
IReadOnlyList<Contract> List();
IReadOnlyList<Contract> ListActiveForDate(int day, Season season, int year);

// ISaveDataSerializer (C-13)
string Serialize(IReadOnlyList<Contract> contracts);
IReadOnlyList<Contract> Deserialize(string? json);  // null/empty → empty list
```

---

## Questions

> Please fill in each `[Answer]:` tag with your choice letter (e.g., `[Answer]: A`).  
> For any question where none of the options fits, write a brief note after your letter.

---

### Q1 — ContractId type

What identifier type should `ContractId` use? This identifier is stored in save data and must survive save/load round-trips.

**A** — `System.Guid` wrapped in a `readonly record struct ContractId(Guid Value)`. Globally unique, zero collision risk across multiple save files or re-hiring scenarios. Slightly more verbose in save files.

**B** — `int` wrapped in `readonly record struct ContractId(int Value)`. Auto-incremented by `ContractStore`. Simple and human-readable in the save file, but requires the store to track the high-water mark and serialize it.

**C** — Raw `string`. Stored as a GUID string (`"xxxxxxxx-xxxx-..."`) so it's both unique and human-readable without a wrapper type.

[Answer]: A — `readonly record struct ContractId(Guid Value)`

---

### Q2 — Game date representation in the `Contract` record

Several fields need to capture "which in-game date" (e.g., when a one-time contract was created, to gate `ListActiveForDate`). Stardew Valley dates have three components: day-of-season (1–28), season (Spring/Summer/Fall/Winter), and year.

**A** — `readonly record struct GameDate(int Day, Season Season, int Year)` where `Season` is a new `enum Season { Spring, Summer, Fall, Winter }` defined in `Dayswork.Core/Domain/`. Readable, self-documenting, easily compared with `>` once total-days arithmetic is added if needed.

**B** — Single `int totalDays` (computed as `day + seasonIndex * 28 + (year - 1) * 112`). Compact, trivially comparable. Less readable in the save JSON.

**C** — Not needed in U-06 at all — `ListActiveForDate` will be implemented in U-09/U-15 where the SMAPI `WorldDate` type is accessible; U-06 just exposes `List()` and the caller filters.

[Answer]: A — `readonly record struct GameDate(int Day, Season Season, int Year)` with a new `Season` enum in `Dayswork.Core/Domain/`

---

### Q3 — Contract status lifecycle states

What set of status values should `ContractStatus` contain?

**A** — Three states: `Active`, `Paused`, `Cancelled`. The "currently running" condition is a runtime check (is today's date a match + state == Active), not a domain state. One-time contracts that have completed are simply `Cancelled` after the shift. Simple and sufficient.

**B** — Four states: `Active`, `Paused`, `Cancelled`, `Completed`. One-time contracts transition to `Completed` after the shift ends cleanly, so the history is distinguishable from explicit cancellation. Recurring contracts never reach `Completed`.

**C** — Four states: `Active`, `Paused`, `Cancelled`, `InProgress`. The state machine tracks whether today's shift has started (to enforce FR-HIRE-15's "can't cancel mid-shift" rule directly in the domain model rather than at the UI layer).

[Answer]: A — Three states: `Active`, `Paused`, `Cancelled`; `Cancelled` is terminal; "in-progress" enforcement stays at the UI layer (FR-HIRE-15)

---

### Q4 — Task-destination assignment storage strategy

A `Contract` needs to map tasks to output destinations. Non-output tasks (Water Crops, Feed Animals, Pet Animals) have no destination. How should the `Contract` record store this?

**A** — `IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations`. Dictionary contains **only output-producing tasks** that have an explicit assignment. Missing key = no assignment (items go to mail). Non-output tasks are never keys.

**B** — `IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations`. Dictionary contains **all enabled tasks**, with non-output tasks mapped to a new `NullDestination` sentinel (a fourth `DestinationKey` subtype that means "this task produces no output, routing is irrelevant").

**C** — Two separate fields: `IReadOnlySet<TaskKind> EnabledTasks` + `IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations`. The set tracks what's enabled; the dictionary holds only tasks with an explicit chest/bin assignment. Non-output tasks appear in `EnabledTasks` but never in `TaskDestinations`.

[Answer]: A — Dictionary of output-producing tasks only; missing key = mail fallback (FR-HIRE-10); `ClearGrass` explicitly excluded (hay routing is silo-first/drop-on-ground per FR-TASK-09)

---

### Q5 — Schedule representation on the `Contract` record

A contract is either one-time or recurring. How should this be modeled?

**A** — `ContractSchedule Schedule` where `ContractSchedule` is an `enum { OneTime, Recurring }`. Simple and sufficient for v1 since there are only two schedule types.

**B** — Discriminated union: `abstract record ContractSchedule` with `sealed record OneTime : ContractSchedule` and `sealed record Recurring : ContractSchedule`. More extensible (future schedules like "weekdays only" would add a new subtype without breaking existing deserialization), but heavier for v1.

**C** — `bool IsRecurring` property on `Contract`. Minimal — avoids defining a new type for a binary flag.

[Answer]: A — `enum ContractSchedule { OneTime, Recurring }`

---

### Q6 — Versioned DTO / top-level save data structure

The unit-of-work.md specifies `DaysworkSaveDataV1` as the top-level save DTO and `ContractDtoV1` as the per-contract DTO. How should the top-level JSON envelope be structured?

**A** — `DaysworkSaveDataV1` has two fields: `int SchemaVersion` (= 1) and `List<ContractDtoV1> Contracts`. `SaveDataSerializer` serializes and deserializes this envelope. Matches unit-of-work.md naming exactly.

**B** — No envelope wrapper. The serializer produces a JSON array `[...]` of `ContractDtoV1` objects directly, with schema version stored as a separate SMAPI key alongside it (two separate `WriteSaveData` calls from the adapter).

**C** — `DaysworkSaveDataV1` wraps the contracts and also includes a `string ModVersion` field (the mod's own version string from `manifest.json`), so future migrations can detect which mod version wrote the data.

[Answer]: C — `DaysworkSaveDataV1` with `SchemaVersion`, `ModVersion` (from manifest), and `Contracts`

---

### Q7 — `DestinationKey` JSON serialization (discriminated union over the wire)

`DestinationKey` has three subtypes (`ChestDestination`, `ShippingBinDestination`, `MailDestination`). Newtonsoft.Json does not automatically deserialize discriminated unions. How should the DTO layer handle this?

**A** — Type-tag pattern in `ContractDtoV1`: the destination field is a plain JSON object with a `"Type"` string field (`"Chest"`, `"ShippingBin"`, `"Mail"`), plus optional `"LocationName"`, `"X"`, `"Y"` fields for `Chest`. A custom `JsonConverter` on `SaveDataSerializer` handles the mapping. Example: `{"Type":"Chest","LocationName":"Farm","X":3,"Y":5}`.

**B** — Serialize as an int enum tag + a nullable chest sub-object: `{"Kind":0,"Chest":{"Location":"Farm","X":3,"Y":5}}` (Kind 0=Chest, 1=ShippingBin, 2=Mail). Compact but slightly less human-readable.

**C** — Use separate nullable DTO fields: `ContractDtoV1` has a `string DestKind` field and nullable `string? ChestLocation`, `int? ChestX`, `int? ChestY`. No custom converter needed — Newtonsoft handles flat nullable fields out-of-the-box.

[Answer]: A — Type-tag `"Type"` discriminator with optional `LocationName`/`X`/`Y`; custom `JsonConverter` in `SaveDataSerializer`

---

### Q8 — `ContractStore` hydration strategy (on save load)

`ContractStore` starts empty at mod startup. When the player loads a save, it needs to be populated from the serialized data. The hydration happens in U-09 when `ContractPersistenceAdapter` wires the SMAPI `GameLoop.SaveLoaded` event. Which store API should the adapter use?

**A** — `ContractStore` exposes a `void Hydrate(IReadOnlyList<Contract> contracts)` method that replaces all content atomically. The adapter calls `Deserialize()` then `Hydrate()`. Clear separation: the store doesn't know about JSON, and the serializer doesn't know about the store.

**B** — The adapter calls `ContractStore.Add()` in a loop for each deserialized contract. No special hydration method needed — Add already handles individual contracts, and the store is guaranteed empty at save-load time (SMAPI loads saves once per session).

**C** — `ContractStore` accepts `IReadOnlyList<Contract>? initialContracts` in its constructor. The adapter constructs the store (or passes it initial data) via the composition root in `ModEntry`. Hydration is a constructor concern, not a method call.

[Answer]: A — `void Hydrate(IReadOnlyList<Contract> contracts)` for atomic replacement; clear separation between store and serializer

---

### Q9 — Handling malformed or partially-valid contracts during deserialization

If a saved contract has a missing required field or an unrecognized `DestinationKey` type (e.g., from a future mod version writing data that this version can't parse), what should `SaveDataSerializer.Deserialize()` do?

**A** — **Skip the malformed contract** and log a warning to the SMAPI console. Return all valid contracts from the collection. Partial data is better than a crash or losing the whole contract list.

**B** — **Return an empty list and log an error**. If any contract fails to deserialize, the whole segment is treated as unreadable (same as missing). Conservative: avoids a partially-loaded contract store in an undefined state.

**C** — **Throw an exception**. Deserialization failure is a programming error or data corruption and should surface immediately rather than silently degrading. The SMAPI error handler will catch and log it.

[Answer]: A — Skip malformed contracts with SMAPI `LogLevel.Warn`; return all valid contracts from the rest of the list

---

### Q10 — `ListActiveForDate` implementation location and signature

`IContractStore.ListActiveForDate(int day, Season season, int year)` is in the Application Design signature. Based on your answer to Q2, should U-06 implement this method now or stub it?

**A** — Implement it fully in U-06. It filters `List()` by: status is `Active` AND (schedule is `Recurring` OR (schedule is `OneTime` AND contract was created on or before this date and hasn't run yet)). Requires `GameDate` from Q2-A to be available.

**B** — Implement a stub that throws `NotImplementedException` in U-06; full implementation lands in U-09 when the caller (RecurringContractScheduler) is first wired. The method signature is locked by the interface.

**C** — Remove `ListActiveForDate` from U-06's interface entirely; replace with a simpler `ListActive()` that returns all non-Cancelled, non-Paused contracts. The calendar filtering (festival checks, one-time expiry) lives in U-15's `CalendarHandlers`.

[Answer]: B — Stub with `NotImplementedException`; interface signature locked here; full implementation deferred to U-09
