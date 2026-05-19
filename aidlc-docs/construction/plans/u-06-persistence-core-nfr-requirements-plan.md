# U-06 Persistence Core — NFR Requirements Plan

## Steps

- [x] Step 1: Analyze functional design artifacts
- [x] Step 2: Create this plan
- [x] Step 3: Generate questions (2 open items — all other decisions already settled)
- [x] Step 4: Collect answers
- [x] Step 5: Generate NFR artifacts
  - [x] nfr-requirements.md
  - [x] tech-stack-decisions.md
- [x] Step 6: Present completion message and await approval

---

## Pre-decided tech stack (no questions needed)

| Decision | Resolution | Source |
|---|---|---|
| JSON library | Newtonsoft.Json — already in `Dayswork.Core.csproj` per U-01 | U-01 unit definition |
| No SMAPI refs in Core | Enforced by project reference list | NFR-MAINT-03 |
| JSON formatting | `Formatting.Indented` — human-readable save files | Functional design |
| Null field handling | `NullValueHandling.Ignore` — omit nulls (e.g., chest coords for ShippingBin) | Functional design |
| Test framework | xUnit + FsCheck.Xunit — already installed in U-02 | NFR-MAINT-01/02 |
| SMAPI save key | `"Dayswork.Contracts"` — defined in U-09 adapter, not U-06 concern | unit-of-work.md |

---

## Open questions

### Q1 — Enum serialization format (StringEnumConverter scope)

The save JSON contains multiple enum values (`ContractStatus`, `ContractSchedule`, `Season`). The DTOs currently store these as `string` fields. Should the `JsonSerializerSettings` apply `StringEnumConverter` globally, or should conversion be handled manually (enum → `ToString()` / `Enum.Parse()`) in the DTO mapping code?

**A** — Apply `StringEnumConverter` globally in `_serializerSettings`. All enums anywhere in the serialized object graph automatically serialize as their string names. Less code in mapping; relies on Newtonsoft convention.

**B** — Handle enum ↔ string conversion explicitly in the DTO mapping code (i.e., `status.ToString()` / `Enum.Parse<ContractStatus>(dto.Status)`). No `StringEnumConverter` needed — the DTO classes use plain `string` fields throughout, so Newtonsoft never touches enums directly. More explicit; no hidden converter magic.

[Answer]: B — Explicit `ToString()` / `Enum.Parse<T>()` in mapping code; DTOs use plain `string` fields; no converter

---

### Q2 — `ContractStore.Hydrate` defensive guard

`Hydrate` is designed to be called exactly once per save-load (by `ContractPersistenceAdapter` on SMAPI's `SaveLoaded` event). Should it enforce this assumption at runtime?

**A** — Silent clear-and-replace. The store is guaranteed empty at the only valid call site. No guard needed — adds noise without value.

**B** — Throw `InvalidOperationException` if the store is non-empty when `Hydrate` is called. Catches misuse early during development (e.g., if the adapter is accidentally wired to fire more than once).

[Answer]: A — Silent clear-and-replace; no defensive guard
