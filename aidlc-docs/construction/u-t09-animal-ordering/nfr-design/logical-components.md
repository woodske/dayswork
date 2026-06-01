# NFR Design — Logical Components — u-t09-animal-ordering

No new infrastructure or components. NFR responsibilities map onto existing types.

| Component | Project | Responsibility (this change) | Pattern |
|---|---|---|---|
| `ShiftPlanBuilder` | `Dayswork.Core/Shifts` | Pure ordering authority: emit per-building `AnimalBuilding`+`OutdoorAnimals` pairs in deterministic building order, then one trailing `FarmForage` when Collect enabled. | P-T09-01 |
| `WorkBatch` / `BatchKind` | `Dayswork.Core/Shifts` | Add `FarmForage`; `OutdoorAnimals` `LocationName` now carries the building home key. | P-T09-01 |
| `ShiftOrchestrator` | `Dayswork/Orchestration` | Thin adapter: fill `OutdoorAnimals` with single-home grazing work; fill `FarmForage` with whole-farm forage tile work; re-point late-truffle rescan to `FarmForage`; extend the all-outdoor-empty guard to include `FarmForage`. | P-T09-02, -03, -05 |
| `AnimalTaskHandler` | `Dayswork/Orchestration` | Unchanged. Provides grazing→home attribution + idempotent pet/collect. | P-T09-03, -04 |
| `WorkAreaScanner` | `Dayswork/Orchestration` | Unchanged. Whole-farm forage scan used once by `FarmForage`. | P-T09-03 |

## Integration notes
- The pure→runtime boundary is unchanged: `ShiftPlanBuilder` returns empty skeletons; `ShiftOrchestrator.BuildInitialBatches` fills them.
- No queues, caches, circuit breakers, or external resources — N/A for an in-process single-player mod.
- No new dependency injection wiring; the orchestrator already owns `AnimalTaskHandler`, `WorkAreaScanner`, and `ShiftPlanBuilder`.
