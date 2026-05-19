# U-07 Capability & Priority Core — Tech Stack Decisions

**Unit**: U-07 — Capability & Priority Core

---

## New tech stack decisions: none

All tech stack choices for U-07 were inherited from prior units. No new packages,
frameworks, or tools are introduced.

| Concern | Decision | Decided in |
|---|---|---|
| Language | C# 10 / .NET 6 | U-01 |
| Production assembly | `Dayswork.Core` class library | U-01 |
| Test assembly | `Dayswork.Tests` class library | U-02 |
| Unit test framework | xUnit | U-02 (Q4) |
| PBT framework | FsCheck.Xunit 2.16.5 | U-02 (PBT-09) |
| Serialization | Newtonsoft.Json (already present) | U-01 |
| SMAPI / Harmony | Not referenced in Core | U-01 (NFR-MAINT-03) |

---

## Implementation notes

### Static class pattern for `CapabilityMatrix`

`CapabilityMatrix` is a `static class` with `static bool` methods — no instantiation,
no DI registration. This is idiomatic C# for a pure lookup table with no instance
state. The `static` modifier makes the "no instances" intent explicit at the
language level.

C# note for readers new to the language: a `static class` cannot be instantiated
(`new CapabilityMatrix()` is a compile error) and all its members must be `static`.
It's the C# equivalent of a utility/helper module in other languages.

### Enum-as-int for `ToolLevel`

`ToolLevel` values (Basic=0 through Iridium=4) match Stardew Valley's internal
`UpgradeLevel` int. The Mod layer can cast directly:
`(ToolLevel)player.getToolFromName("Axe").UpgradeLevel`. No translation table needed.

### LINQ OrderBy for `TaskPriorityOrderer`

The stable sort over at most 10 elements will use a simple `LINQ .OrderBy(t => _rank[t])`
where `_rank` is a `static readonly Dictionary<TaskKind, int>` populated from the
FR-WORK-03 table. LINQ's `OrderBy` is a stable sort in .NET — equal keys preserve
input order (though with unique ranks this doesn't matter here).
