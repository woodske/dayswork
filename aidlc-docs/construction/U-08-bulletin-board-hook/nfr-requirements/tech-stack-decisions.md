# Tech Stack Decisions — U-08 Bulletin Board Hook

## Unit
U-08 — Bulletin Board Hook + i18n + Multiplayer Guard

---

## Decision Log

### TS-U08-01 — Harmony Postfix for Bulletin Board Entry
**Decision**: Use `[HarmonyPostfix]` on the bulletin board's menu-option rendering method to append the "Hire a Farmhand" entry.

**Rationale**:
- A **postfix** runs *after* the original method, making it the right choice when adding to an existing list without preventing the original behavior
- A prefix would be needed only if we wanted to block or redirect — not applicable here
- Targeting the correct method: Stardew's `Billboard.performHoverAction` or more likely the `List<ClickableComponent>` construction in `Billboard.draw` / `Billboard.receiveLeftClick` — exact target method to be confirmed in Code Generation when examining the SV source
- Single `[HarmonyPatch]` attribute on a static class in `Dayswork.Patches` — no inheritance, no Harmony instance management needed; SMAPI's built-in Harmony integration (`<EnableHarmony>true</EnableHarmony>` in .csproj) handles patch application via `new Harmony("Bindicle.Dayswork").PatchAll()`

**Alternatives rejected**:
- Prefix: rejected — doesn't naturally support "append to list" pattern
- Transpiler: rejected — too fragile for a simple entry insertion; postfix is sufficient

---

### TS-U08-02 — SMAPI `Context.IsMultiplayer` for Multiplayer Detection
**Decision**: Use `Context.IsMultiplayer` (from `StardewModdingAPI`) in `MultiplayerGuard`, not `Game1.IsMultiplayer` or `Game1.IsServer`.

**Rationale**:
- `Context.IsMultiplayer` is the SMAPI-recommended API for mods: it abstracts over host/farmhand distinctions and is safe to read from any SMAPI event handler
- `Game1.IsMultiplayer` is a lower-level Stardew field that may not be set at the same lifecycle points
- Checking inside the `Billboard` postfix (at menu-open time) is always safe — `GameLoop.GameLaunched` has fired and the player has fully loaded by that point
- Guard is stateless: re-evaluated at each bulletin board open, handling edge cases gracefully

---

### TS-U08-03 — SMAPI Translation API for I18nHelper
**Decision**: `I18nHelper` is a thin static wrapper around `IModHelper.Translation.Get(string key)`.

**Rationale**:
- `IModHelper.Translation.Get(key)` returns a `Translation` struct that implicitly converts to `string`; if the key is missing, it returns the key itself (safe fallback — never throws)
- All string lookups in `Dayswork/` must go through `I18nHelper.Get(string key)` — this gives a single refactor point if the SMAPI API ever changes, and makes the i18n lint test (U-16) trivial to write (scan for calls to `I18nHelper.Get` vs. hardcoded strings)
- `I18nHelper` is a static class (not an interface) because it has no testable domain logic — the SMAPI Translation API is framework-owned and doesn't need mocking in Dayswork.Tests

**Initial keys added in this unit**:
| Key | English value |
|---|---|
| `bulletin.hire_a_farmhand` | `"Hire a Farmhand"` |
| `multiplayer.refused_log_message` | `"Dayswork is single-player only. The hiring option has been disabled for this multiplayer session."` |

---

## No New NuGet Packages
All packages required for U-08 are already present from prior units:
- **Harmony**: bundled with SMAPI; `<EnableHarmony>true</EnableHarmony>` set in `Dayswork.csproj` (U-01)
- **SMAPI / StardewModdingAPI**: referenced in `Dayswork.csproj` (U-01)
- **StardewValley**: referenced in `Dayswork.csproj` (U-01)
- No test packages added (no Dayswork.Tests files in this unit)
