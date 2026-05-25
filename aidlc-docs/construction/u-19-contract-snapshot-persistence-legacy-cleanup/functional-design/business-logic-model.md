# U-19 — Contract Snapshot Persistence + Legacy Cleanup: Business Logic Model

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A

Technology-agnostic flows for persisting redesigned contracts after U-18 introduced typed scope and fixed `ContractTermsSnapshot` data.

This unit replaces the old save assumption that a contract is fully described by:
- `Zones`
- `DepositAmount`
- `HourlyRate`

The new persistence contract saves:
- typed selected work scope
- the last known `ContractTermsSnapshot`
- existing contract metadata
- temporary legacy financial bridge fields for not-yet-retrofitted consumers

See [domain-entities.md](domain-entities.md) for data shapes and [business-rules.md](business-rules.md) for enforceable rules.

---

## 0. Where this plugs into the redesign

Historical `U-06` persisted hourly contracts as schema version 1. That model is no longer authoritative once pricing and energy are defined by `ContractScopeSelection` plus `ContractTermsSnapshot`.

U-19 introduces the persistence contract that later units depend on:

```text
Domain Contract
  -> SaveDataSerializer
  -> DaysworkSaveDataV2 / ContractDtoV2
  -> save file

save file
  -> SaveDataSerializer
  -> hydrated Domain Contract
  -> ContractStore
```

Important redesign differences:
- schema v1 is treated as unreleased legacy hourly data
- typed scope is saved explicitly rather than hidden in zone conventions
- the saved terms snapshot becomes the authoritative persisted pricing/energy view
- `DepositAmount` and `HourlyRate` remain only as temporary compatibility data during the retrofit

---

## 1. Current-schema save target

The authoritative current-schema save envelope is version 2.

Each current-schema contract entry carries four business groups:
1. **Common contract metadata**
   - id
   - enabled tasks
   - task destinations
   - schedule
   - status
   - hire date
2. **Authoritative selected scope**
   - outdoor zones
   - selected animal buildings
   - optional greenhouse selection
3. **Authoritative terms snapshot**
   - stable price breakdown
   - stable energy profile
4. **Temporary compatibility bridge**
   - `DepositAmount`
   - `HourlyRate`

The redesign source of truth is the pair:
- `ScopeSelection`
- `TermsSnapshot`

The legacy financial fields exist only so older runtime paths can keep working until later retrofit units switch to the new terms model everywhere.

---

## 2. Serialize current-schema contracts

`Serialize(...)` writes only the current schema version.

### 2.1 Serialize common metadata unchanged

The following continue to round-trip directly:
- contract identity
- enabled task set
- task-destination map
- schedule
- status
- hire date

These fields are not being redesigned here; they are simply carried forward alongside the new scope/terms data.

### 2.2 Serialize typed scope explicitly

The selected scope is written as an explicit structured object, not reconstructed from `Zones`.

That means:
- outdoor rectangles save as outdoor rectangles
- selected barns/coops save as typed animal-building references
- greenhouse selection saves as its own dedicated scope

No current-schema save contract needs giant placeholder interior zones to imply buildings or greenhouse scope.

### 2.3 Serialize the terms snapshot explicitly

Every current-schema contract saves the full `ContractTermsSnapshot`.

For one-time contracts:
- the saved snapshot is the exact confirmed terms that will execute later

For recurring contracts:
- the saved snapshot is the latest known terms at the time of save
- future eligible days may replace that snapshot after a rebuild from the saved scope and current config

### 2.4 Serialize bridge financial fields temporarily

Even though hourly billing is no longer authoritative, current-schema persistence keeps `DepositAmount` and `HourlyRate` during the retrofit bridge.

These values are:
- persisted
- rehydrated back into the domain contract
- not used to define legacy-vs-current schema
- not treated as the authoritative redesign pricing source

They are temporary transport data for consumers that have not yet moved to `TermsSnapshot`.

---

## 3. Deserialize by envelope version

`Deserialize(...)` first decides what kind of envelope it is reading.

### 3.1 Null, empty, invalid JSON, or null envelope

These continue to behave as safe empty-load cases:
- no contracts loaded
- maintainer-facing diagnostics for invalid parse/null envelope

### 3.2 Future schema version

If the envelope version is newer than the mod supports:
- load no contracts
- emit a maintainer-facing warning

This protects the player from half-loading newer save data into older code.

### 3.3 Legacy schema version 1

Schema version 1 is treated as the unreleased hourly-contract format.

When a v1 envelope is encountered:
- no contracts are loaded
- no player-facing explanation is created
- a maintainer-facing diagnostic records that legacy pre-release contract data was dropped

This is an envelope-level decision, not a per-contract migration attempt.

### 3.4 Current schema version 2

For a v2 envelope:
- iterate contracts one by one
- map each contract independently
- if one contract is malformed, skip only that contract
- continue loading other valid current-schema contracts
- log maintainer-facing diagnostics for each skipped malformed entry

This preserves as much valid saved work as possible without inventing partial repairs.

---

## 4. Hydrate the bridge domain contract

The current domain `Contract` type still carries both historical and redesign fields. U-19 defines how a current-schema contract is rehydrated into that bridge shape.

### 4.1 Authoritative fields mapped directly

The following become first-class hydrated values:
- `ScopeSelection`
- `TermsSnapshot`

These are the authoritative redesign fields after load.

### 4.2 Compatibility zone projection

Current-schema persistence does not save building and greenhouse scope by smuggling them through `Zones`.

Instead, on load the serializer derives a compatibility `Zones` projection from the saved `ScopeSelection` so older domain consumers can continue reading the contract through the historical `Zones` field during the retrofit.

That compatibility projection may include:
- saved outdoor zones as-is
- compatibility building/greenhouse placeholders where older callers still expect them

The important business rule is:
- saved typed scope is authoritative
- projected `Zones` are compatibility output, not source of truth

### 4.3 Compatibility financial projection

`DepositAmount` and `HourlyRate` are rehydrated from the saved bridge fields into the domain contract.

Again:
- they remain available to older consumers
- they are not the authoritative redesign billing model
- `TermsSnapshot` remains the persisted source of truth for the new pricing model

---

## 5. Contract-store mutation model

U-19 extends the store so recurring repricing can persist new terms without replacing unrelated contract data.

### 5.1 Existing whole-contract operations remain

The store still supports:
- add
- get
- full update
- cancel
- pause
- resume
- list
- hydrate

These are still needed when callers change broader contract state such as scope, schedule, destinations, or status.

### 5.2 Explicit terms replacement seam

`ReplaceTermsSnapshot(contractId, terms)` is added as a dedicated mutation seam.

Business meaning:
- target an existing saved contract
- replace only the saved `TermsSnapshot`
- preserve scope, schedule, destinations, status, and other contract metadata

This seam exists because recurring repricing is conceptually narrower than a full contract rewrite.

### 5.3 When full update still applies

Whole-contract `Update(...)` remains the correct operation when:
- approved edits change saved scope
- schedule changes
- destinations change
- compatibility bridge fields must be revised as part of a broader edit

So the store now supports both:
- broad contract replacement
- narrow terms-snapshot replacement

---

## 6. One-time vs. recurring persistence lifecycle

### 6.1 One-time contracts

At confirmation/save time:
- persist typed scope
- persist the confirmed terms snapshot
- persist compatibility financial fields

On later load:
- execute from that saved snapshot unchanged

One-time contracts do not reprice themselves after being confirmed.

### 6.2 Recurring contracts at creation time

At creation/save time:
- persist typed scope
- persist the preview/confirmation terms snapshot current at that moment
- persist compatibility financial fields

This gives the player a stable saved record immediately, even before the next day-start rebuild happens.

### 6.3 Recurring contracts after approved edits

When the player approves an edit to a recurring contract:
- save the new typed scope immediately
- save the newly approved preview snapshot immediately
- persist those changes now rather than waiting for the next morning

This keeps the persisted recurring contract aligned with the player’s last approved configuration.

### 6.4 Recurring contracts after successful day-start rebuild

On a later eligible day, the scheduler may rebuild terms from:
- saved scope
- enabled tasks
- current config

When that rebuild succeeds:
- replace the persisted snapshot immediately
- leave the saved scope unchanged

That means the saved recurring contract always carries:
- the durable scope source of truth
- the latest known rebuilt terms snapshot

---

## 7. Data-flow summary

```text
Serialize current-schema contract
  -> write common metadata
  -> write typed scope selection
  -> write terms snapshot
  -> write compatibility financial fields
  -> emit schema v2 envelope

Deserialize envelope
  -> if v1: drop as legacy pre-release hourly data
  -> if v2: map each contract independently
  -> if malformed: skip that contract and continue
  -> hydrate domain contract with authoritative scope/terms plus compatibility projections

Recurring repricing
  -> rebuild from saved scope + current config
  -> replace persisted terms snapshot
  -> keep other contract data intact
```

---

## 8. What U-19 explicitly does not decide

- exact runtime consumer switchover away from compatibility `DepositAmount` / `HourlyRate`
- exact JSON property names or serializer-library details
- UI language for edited/loaded contracts
- recurring billing behavior itself at day start
- removal of compatibility `Zones` or financial fields after the later retrofit units retire those consumers

Those belong to later construction units, especially the hiring-flow and recurring-lifecycle retrofits.
