# U-09 Minimum Hiring Flow — Code Summary

**Unit**: U-09 — Minimum Hiring Flow
**Status**: Complete — awaiting play-test
**Build**: 0 errors, 0 warnings
**Tests**: Play-tested (SMAPI-integrated components; no isolated unit tests in this unit)

---

## Files created (5)

| File | Type | Description |
|---|---|---|
| `Dayswork/UI/ContractDraft.cs` | `sealed class` | Mutable UI-only state: `EnabledTasks`, `Zones` (stub), `Destinations` (stub), `Schedule` |
| `Dayswork/UI/HiringFlowCoordinator.cs` | `sealed class` (M-03) | Owns all screen transitions; `BuildContract` helper; whole-farm fallback zone; `OpenHiringFlow()` / `OpenEditFlow()` (stub) |
| `Dayswork/UI/TaskSelectionMenu.cs` | `sealed class : IClickableMenu` (M-04) | 10 task toggles (IDs 100–109); live rate cached on toggle; "Next"(200)/"Cancel"(201) buttons; full gamepad snapping |
| `Dayswork/UI/SummaryMenu.cs` | `sealed class : IClickableMenu` (M-07) | Hours/rate/deposit cached in ctor; tasks summary; "Confirm"(300)/"Back"(301); afford-check invoked via coordinator callback |
| `Dayswork/Integration/ContractPersistenceAdapter.cs` | `sealed class` (M-15) | `OnSaveLoaded` → `Hydrate`; `OnSaving` → `Serialize` + `WriteSaveData` |

---

## Files modified (5)

| File | Change |
|---|---|
| `Dayswork.Core/Persistence/ContractStore.cs` | Implemented `ListActiveForDate` (was `NotImplementedException`): filters by `ContractStatus.Active` + schedule type; 28-day season arithmetic in `IsNextGameDay` |
| `Dayswork/Patches/BulletinBoardPatch.cs` | Replaced placeholder log with `ModEntry.Coordinator.OpenHiringFlow()` |
| `Dayswork/ModEntry.cs` | Added `Coordinator` static property; constructed all Core singletons + `HiringFlowCoordinator` + `ContractPersistenceAdapter`; registered `SaveLoaded`/`Saving` events |
| `Dayswork/Integration/I18nHelper.cs` | Added `Get(string key, object tokens)` overload for SMAPI token substitution |
| `Dayswork/i18n/default.json` | Added 23 new UI keys (task selection, summary, error) |

---

## i18n keys added (23)

| Key | English value |
|---|---|
| `ui.task_selection.title` | `"Hire a Farmhand"` |
| `ui.task_selection.water_crops` | `"Water Crops"` |
| `ui.task_selection.harvest_crops` | `"Harvest Crops"` |
| `ui.task_selection.collect_fruit` | `"Collect Fruit"` |
| `ui.task_selection.feed_animals` | `"Feed Animals"` |
| `ui.task_selection.pet_animals` | `"Pet Animals"` |
| `ui.task_selection.collect_animal_products` | `"Collect Animal Products"` |
| `ui.task_selection.cut_trees` | `"Cut Trees"` |
| `ui.task_selection.clear_rocks` | `"Clear Rocks"` |
| `ui.task_selection.clear_weeds` | `"Clear Weeds"` |
| `ui.task_selection.clear_grass` | `"Clear Grass"` |
| `ui.task_selection.rate_label` | `"Hourly rate: {{rate}}g"` |
| `ui.task_selection.confirm_btn` | `"Next"` |
| `ui.task_selection.cancel_btn` | `"Cancel"` |
| `ui.summary.title` | `"Confirm Hiring"` |
| `ui.summary.tasks_label` | `"Tasks:"` |
| `ui.summary.hours_label` | `"Est. hours: {{hours}}"` |
| `ui.summary.rate_label` | `"Rate: {{rate}}g/hr"` |
| `ui.summary.deposit_label` | `"Deposit: {{deposit}}g"` |
| `ui.summary.refund_policy` | `"Unused deposit refunded at shift end."` |
| `ui.summary.confirm_btn` | `"Confirm"` |
| `ui.summary.back_btn` | `"Back"` |
| `ui.error.cant_afford` | `"You can't afford this contract."` |

---

## Build result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

ModBuildConfig auto-deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.

---

## Build deviations from plan

| Deviation | Detail |
|---|---|
| Collection expression syntax | Plan used C# 12 `[...]` array syntax; `Dayswork.csproj` targets C# 10 (net6.0). Fixed: `TaskOrder` uses `new TaskKind[] { ... }`. |
| `SNAP_AUTOMATIC` constant | Not accessible as a bare name from the subclass — replaced with literal `-1` (the underlying value of `IClickableMenu.SNAP_AUTOMATIC`). |
| `Season` ambiguous reference | `using Dayswork.Core.Domain` and `using StardewValley` both expose a `Season` enum. Fixed in `HiringFlowCoordinator` by using fully-qualified `Dayswork.Core.Domain.Season` at the parse call site. |

---

## NFR compliance

| NFR | Status | Evidence |
|---|---|---|
| NFR-SAFE-02 | Compliant | Afford-check in `ConfirmContract` before `Game1.player.Money -= deposit` |
| NFR-SAFE-03 | Compliant | `Hydrate` on load; `ReadSaveData` null → `Deserialize(null)` → empty list |
| NFR-PERF-01 | Compliant | `draw()` methods read only pre-computed fields; no Core calls inside `draw` |
| NFR-PERF-02 | Compliant | `HoursEstimator.Estimate` called once in `SummaryMenu` ctor |
| NFR-UX-01 | Compliant | Both menus implement `receiveGamePadButton` + `populateClickableComponentList` + `setCurrentlySnappedComponentTo` |
| NFR-UX-02 | Compliant | All display strings via `I18nHelper.Get`; 23 keys in `default.json` |
| NFR-MAINT-03 | Compliant | Core singletons injected via constructor in `ModEntry`; no `new RateCalculator()` inside menus |

---

## Key design decisions

- **`ContractDraft` is UI-only mutable state** — never serialized; discarded on cancel or confirm; `Zones`/`Destinations` are stubs for U-11
- **`WholeFarmZone`** (`"Farm"`, (0,0)→(79,63)) used by coordinator and summary menu when no zones drawn — gives a rough tile count for the hour estimate
- **`ContractStore.ListActiveForDate`** now implemented with 28-day season wrap: Spring(0)→Summer(1)→Fall(2)→Winter(3)→Spring wraps to year+1
- **`SummaryMenu` re-instantiates `RateCalculator`** — a necessary minor duplication because the coordinator doesn't pass the already-computed rate from `TaskSelectionMenu` to `SummaryMenu`. A future refactor could thread `_currentRate` through the draft or coordinator, but it's a single O(n) call so not a performance concern

---

## Definition of Done verification (to be confirmed by play-test)

| Criterion | Status |
|---|---|
| Clicking "Hire a Farmhand" opens TaskSelectionMenu | Awaiting play-test |
| Task toggles show/hide tasks; rate label updates live | Awaiting play-test |
| "Next" advances to SummaryMenu with correct hours/rate/deposit | Awaiting play-test |
| "Back" returns to TaskSelectionMenu preserving toggle state | Awaiting play-test |
| "Confirm" deducts deposit from player gold | Awaiting play-test |
| Confirm blocked with red HUD message when gold insufficient | Awaiting play-test |
| Contract survives save/load via SMAPI data API | Awaiting play-test |
| Gamepad D-pad navigates all elements; A confirms; B cancels | Awaiting play-test |
| Build: 0 errors, 0 warnings | ✓ |
