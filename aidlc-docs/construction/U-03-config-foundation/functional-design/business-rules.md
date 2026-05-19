# U-03 Config Foundation — Business Rules

**Unit**: U-03 Config Foundation
**Scope**: Invariants and default values for IConfigSnapshot

---

## Invariants

| # | Invariant | Rationale | Source |
|---|---|---|---|
| INV-CFG-01 | `BaseRate ≥ 0` | A negative rate would pay the player to hire — nonsense | FR-PAY-01 |
| INV-CFG-02 | All `TaskIncrements` values `≥ 0` | Same rationale per task | FR-PAY-01 |
| INV-CFG-03 | `TaskIncrements` contains an entry for **every** `TaskKind` value | Lookup `TaskIncrements[kind]` must never throw `KeyNotFoundException` for any defined task | derived; FR-TASK-01 lists 10 tasks |
| INV-CFG-04 | `AverageSpeedConstant > 0` | U-05's `HoursEstimator` divides by this value; zero would crash | derived from U-05 needs |
| INV-CFG-05 | `1000 ≤ HardCapTime ≤ 2600` | Valid Stardew time-of-day range (`HHMM` format: 06:00 to 26:00) | Stardew time format |
| INV-CFG-06 | `StuckInitialWaitMinutes ≥ 1` | Zero would trigger stuck-recovery on the first idle tick (a normal pause between actions) | FR-WORK-11 / FR-WORK-13 |
| INV-CFG-07 | `StuckPostTeleportWaitMinutes ≥ 1` | Same rationale — zero would re-escalate immediately after a teleport | FR-WORK-13 |

**Enforcement strategy**:
- `ConfigDefaults.Build()` constructs values that satisfy all INV-CFG-* by construction (verified by U-03 unit tests).
- Any future code path that builds an `IConfigSnapshot` (notably U-16's `GMCMRegistrar` rebuild after GMCM save) must validate against the same invariants before publishing. The validator can be a static helper added in U-16 — out of scope for U-03.
- The immutable record carries the invariants statically once constructed; downstream code may rely on them without re-checking.

---

## Default values

**Source**: spec §Pricing rate table (FR-PAY-01) + spec §Worker behavior + FR-WORK-13.

### Pricing defaults
| Field | Default | Source |
|---|---|---|
| `BaseRate` | `50` (g/hr) | spec §Pricing, FR-PAY-01 |
| `TaskIncrements[WaterCrops]` | `20` | spec §Pricing |
| `TaskIncrements[HarvestCrops]` | `25` | spec §Pricing |
| `TaskIncrements[CollectFruit]` | `15` | spec §Pricing |
| `TaskIncrements[FeedAnimals]` | `20` | spec §Pricing |
| `TaskIncrements[PetAnimals]` | `10` | spec §Pricing |
| `TaskIncrements[CollectAnimalProducts]` | `15` | spec §Pricing |
| `TaskIncrements[CutTrees]` | `30` | spec §Pricing ("Higher rate — more labor") |
| `TaskIncrements[ClearRocks]` | `20` | spec §Pricing |
| `TaskIncrements[ClearWeeds]` | `20` | spec §Pricing |
| `TaskIncrements[ClearGrass]` | `20` | spec §Pricing |

### Worker / shift defaults
| Field | Default | Source |
|---|---|---|
| `AverageSpeedConstant` | `5.0` (in-game min per actionable tile) | placeholder for U-05 calibration; tunable via GMCM in U-16 |
| `HardCapTime` | `2000` (8pm in `HHMM` Stardew time) | spec §Arrival & departure |
| `StuckInitialWaitMinutes` | `10` | FR-WORK-13 ("default: 10 in-game minutes") |
| `StuckPostTeleportWaitMinutes` | `10` | FR-WORK-13 (symmetric with initial wait per spec) |

> **Note on `AverageSpeedConstant`**: the units (in-game minutes per actionable tile) and exact calibration land in U-05's HoursEstimator design. The `5.0` placeholder represents "worker covers ~12 actionable tiles per in-game hour" — defensible but not authoritative. U-16's GMCM exposes it for player tuning.

---

## Validation expectations for unit tests (executed in U-03 Code Generation)

1. `ConfigDefaults.Build()` returns a non-null `IConfigSnapshot`.
2. `BaseRate == 50`.
3. `TaskIncrements` contains exactly the 10 entries listed above with the exact default values (per-task assertion).
4. `TaskIncrements.Count == Enum.GetValues<TaskKind>().Length` (enforces INV-CFG-03 — catches the case where a TaskKind is added without a corresponding rate default).
5. `AverageSpeedConstant > 0` (INV-CFG-04).
6. `HardCapTime == 2000` and falls in `[1000, 2600]` (INV-CFG-05).
7. `StuckInitialWaitMinutes >= 1` and `StuckPostTeleportWaitMinutes >= 1` (INV-CFG-06/07).
8. The returned snapshot is **value-equal** to a freshly built second snapshot (`ConfigDefaults.Build().Equals(ConfigDefaults.Build())`) — exercises record value-equality and confirms the factory is deterministic.

The U-04+ test files in `Dayswork.Tests/Generators/` will add a `ConfigSnapshotGen` FsCheck arbitrary that produces snapshots satisfying every INV-CFG-* invariant. That generator becomes the primary input shape for U-05+ PBTs (PBT-07).
