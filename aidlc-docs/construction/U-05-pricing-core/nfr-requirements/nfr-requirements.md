# U-05 Pricing Core — NFR Requirements

**Unit**: U-05 — Pricing Core
**Source**: Requirements §3 (NFR section) filtered to U-05's scope

---

## Applicable NFRs

### NFR-SAFE-02 — Gold integrity (BLOCKING)

**Requirement**: No gold is ever lost beyond the contractually-billed hourly rate × hours worked. Refunds are integer-clamped to avoid floating-point gold leakage.

**Applicability to U-05**: Primary. All four calculators are responsible for maintaining this invariant.

**Implementation constraints** (from functional design decisions Q3:B, Q5 upper:A):
- `rate` stored as `int` (gold per hour) — no floating-point in the rate itself
- `estimatedHours` and `hoursWorked` stored as `double` — necessary precision for time
- Deposit = `(int)Math.Ceiling(rate × estimatedHours)` — rounds up so the mod never undercharges
- Billable = `(int)Math.Ceiling(rate × hoursWorked)` — same rounding direction
- Refund = `Math.Clamp(deposit - billable, 0, deposit)` — hard bounds prevent negative refund or over-refund

**Verifiable invariants** (enforced by PBT-03 tests):
- `refund ∈ [0, deposit]` for all valid inputs
- `deposit - refund ≤ Math.Ceiling(rate × hoursWorked)` — net charge bounded
- `refund == deposit` when `hoursWorked == 0.0` — empty zone = full refund

**Pass/fail gate**: All PBT-03 properties for RefundCalculator must pass with ≥ 1000 generated inputs.

---

### NFR-MAINT-03 — Pure logic isolation (BLOCKING)

**Requirement**: Pure business-logic modules (rate calculation, deposit/refund math) are separated from SMAPI/game-engine integration so they can be unit-tested without launching Stardew.

**Applicability to U-05**: Primary and exclusive. All four calculators live in `Dayswork.Core/Pricing/` — the project that has no SMAPI or StardewValley assembly references (enforced by the `.csproj` reference list, verified in U-01 and U-04).

**Implementation constraint**: No file in `Dayswork.Core/Pricing/` may reference:
- `StardewValley.*`
- `StardewModdingAPI.*`
- `Microsoft.Xna.*`
- `Harmony.*`

**Verification**: `dotnet build Dayswork.Core` must succeed with 0 errors. Since `Dayswork.Core.csproj` does not reference those assemblies, any accidental import will produce a compile error — the build itself is the verification gate.

---

### NFR-MAINT-01 + NFR-MAINT-02 — Test framework (BLOCKING)

**Requirement**: xUnit for unit tests; FsCheck for property-based tests.

**Applicability to U-05**: U-02 established the test infrastructure. U-05 must add test files to the existing framework — no new packages or configuration needed.

**Implementation constraint**: All U-05 tests live in `Dayswork.Tests/Pricing/`. Test class discovery is handled by xUnit's auto-detection. FsCheck properties use `[Property]` attribute from `FsCheck.Xunit`.

---

## PBT Extension Obligations (Partial mode — enforced rules)

| Rule | Status | U-05 obligation |
|---|---|---|
| PBT-02 | N/A | No serialization in U-05 |
| PBT-03 | **ENFORCED** | 12 invariant properties across all 4 calculators (listed in `business-rules.md`). Each must pass ≥ 1000 generated inputs. |
| PBT-07 | **ENFORCED** | `PricingGen` module in `Dayswork.Tests/Pricing/Generators/PricingGen.cs` — provides typed arbitraries for rate, estimatedHours, deposit, hoursWorked. Composes with `ConfigSnapshotGen` (U-03) and `ZoneGen` (U-04). |
| PBT-08 | **ENFORCED** | Inherited from U-02 wiring. On failure, `[Property]` prints the FsCheck seed + shrunk input. No additional work needed. |
| PBT-09 | **ENFORCED** | FsCheck.Xunit — already installed. No additional work needed. |

---

## Non-applicable NFRs and rationale

| NFR | Status | Rationale |
|---|---|---|
| NFR-SAFE-01 (no items lost) | N/A | Items are held in the item buffer (U-10, C-10 ItemBuffer). Pricing has no items. |
| NFR-SAFE-03 (save file safety) | N/A | U-05 has no persistence. Save logic lives in U-06. |
| NFR-SAFE-04 (no player items picked up) | N/A | NPC behavior — U-10. |
| NFR-PERF-01..03 (per-frame, tile scan, overlay) | N/A | Calculators are called once per hire or once per morning tick — not per frame. |
| NFR-COMPAT-01..04 | N/A | Platform compat established in U-01. No new assemblies added by U-05. |
| NFR-UX-01..03 | N/A | No UI in U-05. |
| NFR-MAINT-04 (Harmony isolation) | N/A | No Harmony patches in U-05. |
| NFR-MAINT-05 (dotnet format) | Advisory | Standard .NET naming and formatting applied during Code Generation. |
| NFR-SEC-01 | N/A | Security Baseline extension disabled for this project. |
| NFR-ONBOARD-01..02 | Advisory | Just-in-time C# explanations embedded in Code Generation plan. |
| NFR-DIST-01..03 | N/A | Cross-cutting; handled in U-01 manifest/README. |

---

## Performance note (informational)

`RateCalculator` iterates over at most 10 `TaskKind` values. `HoursEstimator` iterates over the player's zone list (typical: 1–5 zones). `DepositCalculator` and `RefundCalculator` are O(1). Total CPU time per call is negligible — well under 1 microsecond. No performance optimization is warranted.

---

## AverageSpeedConstant calibration note

The default `AverageSpeedConstant` in `ConfigDefaults.Build()` was set to `5.0` as a placeholder in U-03. U-05's functional design finalized the **unit** as "real minutes per tile per task." The **default value** requires calibration during U-05 Code Generation.

Calibration target: the deposit for a representative early-game contract (e.g., a 30×20-tile zone, 2 tasks, base config) should feel meaningful but affordable to a mid-game player (roughly 300–800g). Code Generation will compute the recommended default and update `ConfigDefaults.Build()` accordingly.

This is a gameplay-balance concern, not a safety or correctness concern. The value is player-tunable via GMCM in U-16 (FR-PAY-09, FR-CFG-01).
