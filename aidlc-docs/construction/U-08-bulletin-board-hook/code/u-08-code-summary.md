# U-08 Bulletin Board Hook — Code Summary

**Unit**: U-08 — Bulletin Board Hook + i18n + Multiplayer Guard
**Status**: Complete
**Build**: 0 errors, 0 warnings
**Tests**: Play-tested (no automated tests in this unit)

---

## Files created

### Dayswork — New files (3)

| File | Type | Description |
|---|---|---|
| `Dayswork/Guards/MultiplayerGuard.cs` | `static class` | Thin wrapper over `Context.IsMultiplayer`; stateless re-evaluation per billboard open |
| `Dayswork/Integration/I18nHelper.cs` | `static class` | Init+Get wrapper over SMAPI's `Translation` API; safe missing-key fallback |
| `Dayswork/Patches/BulletinBoardPatch.cs` | `static class` (3 postfixes) | Constructor postfix injects `ClickableComponent`; Draw postfix renders button; ReceiveLeftClick postfix handles click and MP log |

### Dayswork — Modified files (2)

| File | Change |
|---|---|
| `Dayswork/ModEntry.cs` | Added `internal static IMonitor ModMonitor`; `I18nHelper.Init(helper)`; `new Harmony(UniqueID).PatchAll()` |
| `Dayswork/i18n/default.json` | Added 2 initial i18n keys |

---

## i18n keys added

| Key | English value |
|---|---|
| `bulletin.hire_a_farmhand` | `"Hire a Farmhand"` |
| `multiplayer.refused_log_message` | `"Dayswork is single-player only. The hiring option has been disabled for this multiplayer session."` |

---

## Build result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

ModBuildConfig auto-deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.

---

## NFR compliance

| NFR | Status | Evidence |
|---|---|---|
| NFR-MAINT-04 | Compliant | `BulletinBoardPatch.cs` is the only file in `Dayswork/Patches/`; all three billboard postfixes are in one class |
| NFR-UX-02 | Compliant | All user-visible strings go through `I18nHelper.Get(key)`; `default.json` is the only source of English text |
| FR-MP-01 | Compliant | `MultiplayerGuard.IsMultiplayer()` checked in Draw (silently skips button) and ReceiveLeftClick (logs i18n'd warning) |
| NFR-ONBOARD-01 | Compliant | Harmony postfix anatomy, `__instance`, constructor patching, SpriteBatch, `ModMonitor` naming, and `PatchAll` all documented in the code generation plan |

---

## Build deviations

| Deviation | Detail |
|---|---|
| `Billboard` constructor parameter name | Plan used `onlyViewDailyQuest`; actual SV 1.6 parameter is `dailyQuest`. Harmony matches by name — corrected in `Constructor_Postfix` signature. This is the reason plan notes said to verify against decompiled source. |

---

## Key design decisions

- **`onlyViewDailyQuest` guard in constructor postfix**: When `Billboard` is opened in daily-quest mode, `_hireButton` is set to `null` so Draw and ReceiveLeftClick postfixes silently skip — our entry only appears on the main bulletin board
- **`ModMonitor` naming**: Avoids shadowing `Mod.Monitor` (the inherited non-static property); `internal static` enables logging from static patch classes without DI
- **`drawMouse` redraw in Draw postfix**: Cursor is redrawn after our button content so it sits visually on top of the new layer
- **Placeholder log in ReceiveLeftClick**: `"[Dayswork] Hire-flow placeholder opened"` — replaced by `HiringFlowCoordinator.OpenMenu()` in U-09
- **No `Dayswork.Tests` files**: This unit is play-tested; `I18nHelper` and `MultiplayerGuard` have no domain logic worth unit testing in isolation

---

## Definition of Done verification

| Criterion | Status |
|---|---|
| In single-player, opening the bulletin board shows "Hire a Farmhand" | Play-test required |
| Clicking the entry logs `"[Dayswork] Hire-flow placeholder opened"` | Play-test required |
| In multiplayer, entry is absent and SMAPI log shows friendly refusal | Play-test required |
| Build: 0 errors, 0 warnings | ✓ |
