# U-03 Config Foundation — Functional Design Plan

**Unit**: U-03 Config Foundation (see [unit-of-work.md](../../inception/application-design/unit-of-work.md))
**Stage**: CONSTRUCTION → U-03 → Functional Design
**Workspace root**: `C:\Users\kwood\Repos\dayswork`

---

## Unit context

### Stories assigned to U-03
- None delivered directly. Foundation for **S-13** (GMCM exposes these fields in U-16).

### Components owned
- **C-14 ConfigSnapshot** (interface + immutable record)
- **C-15 ConfigDefaults** (static factory)

### Dependencies on other units
- **U-01 Project Scaffold** — `Dayswork.Core/Dayswork.Core.csproj`
- **U-02 Test Infrastructure** — `Dayswork.Tests/` framework; this unit drops `ConfigSnapshotGen` into `Generators/` per PBT-07

### Dependencies this unit unblocks
- **U-05 Pricing Core** — every calculator depends on `IConfigSnapshot`
- **U-10 Worker Shift** — `ShiftStateMachine` reads `HardCapTime`
- **U-13 Worker Features** — `StuckDetector` reads `Stuck*WaitMinutes`
- **U-17 GMCM + Polish** — `GMCMRegistrar` exposes every IConfigSnapshot property

### Per-unit stage decisions
| Stage | Decision | Rationale |
|---|---|---|
| Functional Design | **EXECUTE** | Defines IConfigSnapshot schema + ConfigDefaults values; new business data structure |
| NFR Requirements | **SKIP** | No perf/security/i18n NFRs apply at the config-record level. NFR-SAFE-02 (rounding) lands in U-05; NFR-MAINT-01/02 already enforced by Core project placement |
| NFR Design | **SKIP** | Cascades from NFR Requirements skip |
| Infrastructure Design | **SKIP** | Per execution plan, all units skip Infra |
| Code Generation | **EXECUTE** | Always |

---

## Architectural decisions (locked)

### Q1 — TaskKind enum placement
**Answer**: A. Move TaskKind to U-03

**Rationale**: ConfigSnapshot fundamentally needs typed task identifiers for `IReadOnlyDictionary<TaskKind, int> TaskIncrements`. String keys lose type safety; 10 named fields couple the record to the v1 task list. Moving the small enum to U-03 is the architecturally cleanest resolution. Plan deviation from [unit-of-work.md](../../inception/application-design/unit-of-work.md) (originally placed TaskKind in U-04).

### Q2 — Configurable field scope
**Answer**: A. Full FR-CFG-01 scope now

**Rationale**: Avoids retroactive Extends entries in U-10/U-13. ConfigSnapshot is finalized in U-03 with all fields needed across the lifetime of the codebase (BaseRate, TaskIncrements, AverageSpeedConstant, HardCapTime, StuckInitialWaitMinutes, StuckPostTeleportWaitMinutes).

### Q3 — Rate value type
**Answer**: A. int

**Rationale**: Matches Stardew gold semantics (gold is always integer). Defaults stay readable (50, 20, 25 not 50.0m, 20.0m). U-05's pricing math (rate × hours) uses double internally and rounds to int at deposit/refund boundary per NFR-SAFE-02.

---

## Functional Design steps

- [x] Architectural decisions Q1, Q2, Q3 locked above
- [x] Generate `aidlc-docs/construction/U-03-config-foundation/functional-design/business-logic-model.md` (data flow, lifecycle, GMCM-edit semantics)
- [x] Generate `aidlc-docs/construction/U-03-config-foundation/functional-design/business-rules.md` (invariants, validation, defaults from spec §Pricing)
- [x] Generate `aidlc-docs/construction/U-03-config-foundation/functional-design/domain-entities.md` (TaskKind, IConfigSnapshot, ConfigSnapshot, ConfigDefaults)
- [x] Update `aidlc-docs/aidlc-state.md` (Functional Design awaiting approval)
- [x] Update `aidlc-docs/audit.md`
- [x] Present REVIEW REQUIRED gate

---

## Files this stage produces

| File | Type | Purpose |
|---|---|---|
| `aidlc-docs/construction/U-03-config-foundation/functional-design/business-logic-model.md` | created | Data flow + lifecycle + GMCM semantics |
| `aidlc-docs/construction/U-03-config-foundation/functional-design/business-rules.md` | created | INV-CFG-01..07 invariants + defaults table |
| `aidlc-docs/construction/U-03-config-foundation/functional-design/domain-entities.md` | created | TaskKind enum + IConfigSnapshot + ConfigSnapshot + ConfigDefaults schemas |
| `aidlc-docs/aidlc-state.md` | modified | Advance to Functional Design Awaiting Approval |
| `aidlc-docs/audit.md` | modified | Q&A + FD generation log |

---

## Open questions for the user

None at this stage — all architectural questions answered. Implementation details (e.g., exact `AverageSpeedConstant` calibration) deferred to U-05 Pricing Core where they have observable consequences.
