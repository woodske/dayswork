# U-19 — Contract Snapshot Persistence + Legacy Cleanup: Business Rules

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A

Enforceable rules for persisting redesigned contracts and dropping unreleased legacy hourly data. See [business-logic-model.md](business-logic-model.md) for flow and [domain-entities.md](domain-entities.md) for data shapes.

---

## No player-facing deviation introduced at U-19

U-19 does not add new player-visible billing behavior. It formalizes the persistence side of already-approved redesign decisions:
- typed scope is authoritative
- saved terms snapshot is authoritative
- legacy hourly contracts are dropped instead of migrated
- recurring contracts keep saved scope and refresh their saved snapshot later

---

## Schema and envelope rules

**BR-SAVE-01 — Current redesigned persistence uses schema version 2.** Current-schema contract saves must be emitted under a schema version distinct from legacy hourly schema v1. *(FD-Q3=A)*

**BR-SAVE-02 — Schema version 1 is treated as legacy pre-release hourly data.** U-19 must not attempt to migrate, partially load, or reinterpret schema v1 contracts as current-schema contracts. *(FD-Q3=A)*

**BR-SAVE-03 — Future schema versions are rejected wholesale.** If a save envelope version is newer than the mod supports, no contracts are loaded from that envelope. *(existing serializer safety rule, carried forward)*

**BR-SAVE-04 — Invalid or null save payloads load as empty.** Invalid JSON or null envelopes must not create partial contracts. *(existing serializer safety rule, carried forward)*

---

## Authoritative persisted contract shape

**BR-CONTRACT-01 — Every current-schema contract persists both typed scope and terms snapshot.** `ContractScopeSelection` and `ContractTermsSnapshot` are both required persisted components of a valid redesigned contract. *(FD-Q1=A)*

**BR-CONTRACT-02 — One-time contracts persist the exact confirmed terms snapshot.** The saved one-time snapshot is the authoritative execution snapshot for that contract. *(FD-Q1=A, DoD)*

**BR-CONTRACT-03 — Recurring contracts persist the latest known terms snapshot plus the durable scope source of truth.** Saved scope exists so future repricing can rebuild; saved snapshot exists so the contract round-trips and reflects the latest approved/rebuilt terms. *(FD-Q1=A, FD-Q7=A, DoD)*

**BR-CONTRACT-04 — Current-schema persistence serializes typed scope explicitly.** Outdoor zones, selected animal buildings, and greenhouse scope are saved as first-class fields, not encoded through legacy zone conventions. *(FD-Q2=A)*

**BR-CONTRACT-05 — Current-schema persistence keeps compatibility financial fields temporarily.** `DepositAmount` and `HourlyRate` remain saved and loaded during the retrofit bridge, but they are not the authoritative redesign pricing model. *(FD-Q6=A)*

**BR-CONTRACT-06 — The saved terms snapshot outranks compatibility financial fields.** When redesign logic needs persisted pricing or worker energy, it reads `TermsSnapshot`, not `DepositAmount` or `HourlyRate`. *(FD-Q1=A, FD-Q6=A)*

---

## Scope and compatibility projection rules

**BR-SCOPE-01 — Saved typed scope is the authoritative persisted work scope.** `ContractScopeSelection` is the source of truth for future recurring rebuilds and later runtime scope alignment. *(FD-Q1=A, FD-Q2=A)*

**BR-SCOPE-02 — Compatibility `Zones` are derived, not authoritative.** Any historical `Zones` view used after load must be rebuilt from the saved typed scope rather than treated as the canonical persisted representation. *(FD-Q2=A)*

**BR-SCOPE-03 — Current-schema persistence must not require placeholder interior zones in the save contract.** Barn/coop and greenhouse scope may still be projected back into bridge-domain `Zones` after load, but they are not saved that way. *(FD-Q2=A)*

---

## Load and malformed-data rules

**BR-LOAD-01 — Legacy hourly contracts are dropped with no player-facing explanation.** No mail, popup, or UI message is shown when schema v1 contract data is discarded. *(FD-Q3=A, FD-Q8=A, DoD)*

**BR-LOAD-02 — Malformed current-schema contracts are skipped individually.** One bad v2 contract entry must not prevent other valid v2 contracts from loading. *(FD-Q4=A)*

**BR-LOAD-03 — Current-schema contract validity requires both scope and terms.** A v2 contract missing `ScopeSelection` or `TermsSnapshot` is malformed and must be dropped rather than auto-healed. *(FD-Q1=A, FD-Q4=A)*

**BR-LOAD-04 — U-19 does not synthesize missing authoritative redesign fields with defaults.** If authoritative current-schema data is missing or invalid, the contract is dropped instead of guessed into existence. *(FD-Q4=A)*

**BR-LOAD-05 — Load-time compatibility projections are allowed only after authoritative redesign data is valid.** The serializer may derive `Zones` or hydrate compatibility financial fields only after the typed scope and terms snapshot successfully map. *(FD-Q2=A, FD-Q6=A)*

---

## Contract-store mutation rules

**BR-STORE-01 — The store exposes a dedicated terms-replacement seam.** `ReplaceTermsSnapshot` is a first-class store operation for recurring repricing and similar narrow mutations. *(FD-Q5=A)*

**BR-STORE-02 — `ReplaceTermsSnapshot` preserves all non-terms contract data.** Scope, schedule, status, destinations, and other fields remain unchanged when only the terms snapshot is being replaced. *(FD-Q5=A)*

**BR-STORE-03 — Whole-contract `Update(...)` remains valid for broader edits.** If a mutation changes scope, schedule, destinations, or other non-terms data, the broader update path still applies. *(FD-Q5=A)*

---

## Recurring lifecycle persistence rules

**BR-RECUR-01 — Approved recurring edits refresh persisted scope immediately.** When the player approves an edit, the saved scope is replaced now, not deferred until the next activation. *(FD-Q7=A)*

**BR-RECUR-02 — Approved recurring edits refresh the persisted snapshot immediately.** The saved recurring snapshot is updated to the newly approved preview terms as part of the edit. *(FD-Q7=A)*

**BR-RECUR-03 — Successful recurring day-start rebuilds replace the persisted snapshot immediately.** After a rebuild from saved scope and current config succeeds, the new terms snapshot becomes the saved snapshot of record. *(FD-Q7=A)*

**BR-RECUR-04 — Recurring rebuild does not overwrite the saved scope unless the player actually edited the contract.** Saved scope remains the durable source of truth across repricing events. *(FD-Q1=A, FD-Q7=A)*

---

## Diagnostics and silence rules

**BR-LOG-01 — Legacy-drop and malformed-current-schema events are maintainer-visible in logs.** U-19 records diagnostics for dropped schema v1 data and skipped malformed v2 contracts. *(FD-Q8=A)*

**BR-LOG-02 — Legacy-drop and malformed-current-schema events remain player-silent.** Logging must not create player-facing explanations, warnings, or compensation flows. *(FD-Q8=A)*

---

## Retrofit-bridge rules

**BR-BRIDGE-01 — Compatibility financial fields are temporary bridge data.** They remain persisted and hydrated only until later retrofit units remove or replace their remaining consumers. *(FD-Q6=A)*

**BR-BRIDGE-02 — Compatibility bridge fields do not define current-schema validity.** A valid redesigned contract is defined by the current-schema envelope plus valid typed scope and valid terms snapshot, not by whether the legacy financial fields still describe the same billing model. *(FD-Q6=A)*

**BR-BRIDGE-03 — The bridge does not reopen legacy migration.** Keeping compatibility fields temporarily does not authorize retaining, migrating, or reviving schema v1 hourly contracts. *(FD-Q3=A, FD-Q6=A)*

---

## Property-based testing obligations

Property-Based Testing extension is enabled in partial mode. U-19 owns one of the strongest persistence seams in the redesign, so it carries explicit persistence invariants.

| Rule | Required property / invariant |
|---|---|
| PBT-02 round-trip | Valid current-schema contracts round-trip through v2 serialization/deserialization without losing authoritative scope, authoritative terms snapshot, or preserved metadata. |
| PBT-03 invariant | Schema v1 envelopes always deserialize to no contracts; malformed v2 entries never poison valid sibling entries; `ReplaceTermsSnapshot` changes only the saved terms field. |
| PBT-07 generator quality | Generators must cover one-time and recurring contracts, mixed scope families, repeated pricing lines, missing/invalid v2 fragments, and multi-contract envelopes containing both valid and malformed entries. |
| PBT-08 shrinking | Counterexamples should shrink toward one malformed contract, one valid sibling contract, or one minimal terms replacement. |
| PBT-09 framework | FsCheck remains the framework used for persistence properties. |

Recommended concrete properties for U-19:
- valid v2 contract -> serialize -> deserialize -> same authoritative scope and terms
- v1 envelope -> deserialize -> empty contract list
- v2 envelope with one valid + one malformed -> exactly the valid contract survives
- `ReplaceTermsSnapshot` keeps id, scope, schedule, destinations, and status unchanged

Security Baseline extension is disabled project-wide, so its rules are N/A here.
