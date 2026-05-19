# U-07 Capability & Priority Core — NFR Requirements

**Unit**: U-07 — Capability & Priority Core
**Source**: Requirements §3 (NFR section) filtered to U-07's scope

---

## Applicable NFRs

### NFR-MAINT-03 — Pure logic isolation (BLOCKING)

**Requirement**: Pure business-logic modules are separated from SMAPI/game-engine
integration so they can be unit-tested without launching Stardew.

**Applicability to U-07**: Primary and exclusive. All new files land in
`Dayswork.Core/Capabilities/`, `Dayswork.Core/Shifts/`, and `Dayswork.Core/Domain/` —
the project that has no SMAPI or StardewValley assembly references.

**Implementation constraint**: No file in these directories may reference:
- `StardewValley.*`
- `StardewModdingAPI.*`
- `Microsoft.Xna.*`
- `Harmony.*`

**Verification**: `dotnet build Dayswork.Core` succeeds with 0 errors. The `.csproj`
reference list is the enforcement gate — any accidental import produces a compile error.

---

### NFR-MAINT-01 + NFR-MAINT-02 — Test framework (BLOCKING)

**Requirement**: xUnit for unit tests; FsCheck for property-based tests.

**Applicability to U-07**: Inherited infrastructure from U-02. U-07 drops test files
into the existing `Dayswork.Tests/` project — no new packages or configuration.

**Test directories**:
- `Dayswork.Tests/Capabilities/` — CapabilityEvaluator table-driven tests
- `Dayswork.Tests/Shifts/` — TaskPriorityOrderer property-based and fact tests
- `Dayswork.Tests/Generators/` — ToolSnapshotGen (PBT-07 obligation)

---

## PBT Extension Obligations (Partial mode — enforced rules)

| Rule | Status | U-07 obligation |
|---|---|---|
| PBT-02 | N/A | No serialization in U-07 |
| PBT-03 | **ENFORCED** | TaskPriorityOrderer: two invariant properties (determinism + ordering). Each must pass ≥ 1000 generated inputs. See details below. |
| PBT-07 | **ENFORCED** | `ToolSnapshotGen` in `Dayswork.Tests/Generators/ToolSnapshotGen.cs` — provides `Arbitrary<ToolSnapshot>` for downstream units (U-10 ShiftStateMachine, U-13 orchestrator). |
| PBT-08 | **ENFORCED** | Inherited from U-02 wiring. On failure, `[Property]` prints FsCheck seed + shrunk input. No additional work. |
| PBT-09 | **ENFORCED** | FsCheck.Xunit already installed. No additional work. |

### PBT-03 properties for TaskPriorityOrderer

**Property 1 — Determinism**: For any generated subset of enabled `TaskKind` values,
calling `Order(tasks)` twice with the same input produces identical output.

```
forall tasks: IEnumerable<TaskKind> .
  orderer.Order(tasks).SequenceEqual(orderer.Order(tasks)) == true
```

**Property 2 — Ordering invariant**: For any generated subset, consecutive elements
in the output satisfy `Priority(output[i]) < Priority(output[i+1])`, where
`Priority` maps to the FR-WORK-03 rank table.

```
forall tasks: IEnumerable<TaskKind> .
  let result = orderer.Order(tasks)
  forall i in [0, result.Count - 2] .
    FR_WORK_03_Rank(result[i]) < FR_WORK_03_Rank(result[i+1])
```

**Note on CapabilityEvaluator tests**: The capability tests are exhaustive
table-driven `[Fact]` tests (5 AxeLevel × 5 AxeTarget + 5 PickaxeLevel × 3 PickTarget
= 40 cases), not property-based. PBT is not applicable here — the domain is small
enough that exhaustive enumeration is the right tool. This is consistent with PBT
guidance (PBT-03 applies to invariants over large input spaces; a 40-cell lookup table
is better verified exhaustively).

---

## Non-applicable NFRs and rationale

| NFR | Status | Rationale |
|---|---|---|
| NFR-SAFE-01 (no items lost) | N/A | No items in this unit; item buffer is U-10 (C-10 ItemBuffer). |
| NFR-SAFE-02 (gold integrity) | N/A | No pricing math; calculators live in U-05. |
| NFR-SAFE-03 (save file safety) | N/A | No persistence in this unit. |
| NFR-SAFE-04 (no player items picked up) | N/A | NPC behavior — U-10. |
| NFR-PERF-01 (per-frame update) | N/A | CapabilityEvaluator is called during task-queue building (once per zone entry per shift, per NFR-PERF-02). TaskPriorityOrderer called once at shift start. Neither is on the frame update path. |
| NFR-PERF-02 (tile scan once per zone entry) | Informational | The ShiftOrchestrator (U-10) will call CanChop/CanBreak O(n) times when scanning zone tiles — one call per object. Both methods are O(1) (single comparison); total scan cost is negligible. |
| NFR-PERF-03 (zone overlay rendering) | N/A | No UI in this unit. |
| NFR-COMPAT-01..04 | N/A | Platform compat established in U-01; no new assemblies added. |
| NFR-UX-01..03 | N/A | No UI in this unit. |
| NFR-MAINT-04 (Harmony isolation) | N/A | No Harmony patches in this unit. |
| NFR-MAINT-05 (dotnet format) | Advisory | Standard .NET naming and formatting applied during Code Generation. |
| NFR-SEC-01 | N/A | Security Baseline extension disabled for this project. |
| NFR-ONBOARD-01..02 | Advisory | Just-in-time C# explanations embedded in Code Generation plan where relevant (e.g., static class pattern, enum-as-int). |
| NFR-DIST-01..03 | N/A | Cross-cutting; handled in U-01. |
