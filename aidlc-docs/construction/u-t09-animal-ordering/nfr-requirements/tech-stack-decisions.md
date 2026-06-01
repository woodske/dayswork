# Tech Stack Decisions — u-t09-animal-ordering

No new technology. Reuse the existing stack:

- **Language/Runtime**: C# / .NET 6, Stardew Valley + SMAPI (unchanged).
- **Core logic**: pure `Dayswork.Core` (`ShiftPlanBuilder`, `WorkBatch` enum) — no SMAPI/game dependency, fully unit/property testable.
- **Runtime adapter**: `Dayswork` mod project (`ShiftOrchestrator`) — thin adapter over the pure plan, reusing `AnimalTaskHandler`, `WorkAreaScanner`, `BuildingWorkNavigator`.
- **Testing**: xUnit (examples) + FsCheck (properties), consistent with existing `Dayswork.Tests`. PBT in full mode (NFR-T09-04).
- **Persistence/config/UI**: none changed.

**Rationale**: the change is a localized re-ordering with clean invariants; the existing pure-Core + thin-adapter split already supports it. Introducing anything new would add risk without benefit.
