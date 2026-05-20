# Tech Stack Decisions — U-12 Hiring UI: Schedule + Edit/Pause/Cancel

## No new tech stack decisions for U-12

All technology choices were made in prior units. U-12 follows established patterns.

| Concern | Decision | Established In |
|---|---|---|
| UI framework | `IClickableMenu` (StardewValley built-in) | U-09 |
| Gamepad nav | `ClickableComponent` + `receiveGamePadButton` override | U-09 |
| i18n | `I18nHelper.Get(key)` → `i18n/default.json` | U-08 |
| Persistence | `ContractStore` (Core) + `ContractPersistenceAdapter` flush via `IModHelper.Data` | U-06, U-09 |
| JSON serialization | Newtonsoft.Json (via SMAPI) | U-06 |
| Harmony patching | `Dayswork.Patches` namespace, `[HarmonyPatch]` attribute style | U-08 |
| Unit testing | xUnit in `Dayswork.Tests` | U-02 |
| Property-based testing | FsCheck.Xunit, generators in `Dayswork.Tests/Generators/` | U-02 |
| New field backward-compat | `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]` with explicit default | U-06 pattern |

## ContractDtoV1 New Field

`IsPaused` is added to `ContractDtoV1` using Newtonsoft.Json's `DefaultValueHandling.Populate` so that old save data (which has no `IsPaused` key) deserializes to `false` rather than throwing. This is the same pattern used for any additive save schema change.
