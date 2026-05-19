# U-05 Pricing Core — Tech Stack Decisions

**Unit**: U-05 — Pricing Core

---

## Summary

No new tech stack decisions are needed for U-05. All relevant choices were made in prior units. This document records the inherited decisions that apply to U-05's implementation.

---

## Inherited decisions

| Decision | Choice | Decided in | Rationale |
|---|---|---|---|
| Language | C# 10 / .NET 6 | U-01 (NFR-COMPAT-01) | Stardew Valley 1.6 + SMAPI 4.x requirement |
| Numeric types for rates | `int` (gold) | U-05 FD Q3:B | Integer gold avoids float leakage (NFR-SAFE-02) |
| Numeric types for hours | `double` | U-05 FD Q3:B | Fractional hours required for deposit precision |
| Rounding direction | `Math.Ceiling` | U-05 FD Q3:B | Always round up — mod never undercharges |
| Clamp direction | `Math.Clamp(v, 0, deposit)` | U-05 FD Q5:A | Both-direction safety guard (NFR-SAFE-02) |
| Discriminated union pattern | Abstract record hierarchy | U-05 FD Q5:B | `DepositResult` forces explicit caller handling of Zero case |
| Unit test framework | xUnit | U-02 (NFR-MAINT-01, Q4) | Already installed in `Dayswork.Tests.csproj` |
| PBT framework | FsCheck.Xunit | U-02 (PBT-09) | Already installed; `[Property]` + seed logging wired |
| Shared generator location | `Dayswork.Tests/Pricing/Generators/PricingGen.cs` | U-05 FD (PBT-07) | Mirrors U-03's `ConfigSnapshotGen` and U-04's `ZoneGen` patterns |
| Core project isolation | `Dayswork.Core.csproj` — no SMAPI/SV refs | U-01 (NFR-MAINT-03) | Build-time enforcement; all U-05 code stays in `Dayswork.Core/Pricing/` |

---

## No new packages

U-05 adds no NuGet references. The `Dayswork.Core` project uses only `Newtonsoft.Json` (not needed by U-05 but already present). The `Dayswork.Tests` project already has xUnit and FsCheck.Xunit from U-02.

---

## Design pattern: stateless services with interface injection

All four calculators follow the same pattern established in prior units:

```
Interface (I{Name}.cs)       <- dependency injection point
Implementation ({Name}.cs)   <- stateless sealed class, injected via constructor elsewhere
```

This pattern means:
- Tests instantiate the implementation directly (`new RateCalculator()`)
- Production code (U-09 SummaryMenu, U-10 ShiftOrchestrator) receives `IRateCalculator` via constructor injection through ModEntry's composition root
- FsCheck tests can construct any arbitrary inputs without needing SMAPI running

No IoC container is used — SMAPI mods typically wire dependencies manually in `ModEntry.Entry()` (Service S-A in `services.md`).
