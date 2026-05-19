# Logical Components — U-08 Bulletin Board Hook

## Unit
U-08 — Bulletin Board Hook + i18n + Multiplayer Guard

---

## Component Map

```
Dayswork/ (Mod project)
│
├── ModEntry.cs                         [EXTENDED — adds Harmony init + I18nHelper.Init]
│   ├── static IMonitor Monitor          ← exposed for patch classes that can't use DI
│   └── Entry(IModHelper helper)
│       ├── I18nHelper.Init(helper)      ← Step 1: i18n ready
│       └── new Harmony(UniqueID)
│             .PatchAll()               ← Step 2: all [HarmonyPatch] classes applied
│
├── Patches/
│   └── BulletinBoardPatch.cs           [OWNED — M-02]
│       ├── [HarmonyPatch(typeof(Billboard), "<target>")]
│       └── [HarmonyPostfix] Draw_Postfix(Billboard __instance, ...)
│           ├── MultiplayerGuard.IsMultiplayer() → return early + log
│           └── append "Hire a Farmhand" clickable entry
│
├── Guards/
│   └── MultiplayerGuard.cs             [OWNED — M-18]
│       └── static bool IsMultiplayer()
│               └── Context.IsMultiplayer
│
├── Integration/
│   └── I18nHelper.cs                   [OWNED — M-21]
│       ├── static Init(IModHelper)
│       └── static string Get(string key)
│               └── helper.Translation.Get(key).ToString()
│
└── i18n/
    └── default.json                    [EXTENDED — adds first 2 keys]
        ├── "bulletin.hire_a_farmhand"
        └── "multiplayer.refused_log_message"
```

---

## Initialization Sequence (inside ModEntry.Entry)

```
Entry(IModHelper helper)
  │
  ├─ 1. I18nHelper.Init(helper)           ← i18n available to all subsequent code
  │
  ├─ 2. Monitor exposed as static prop    ← patch classes can call ModEntry.Monitor.Log(...)
  │
  └─ 3. new Harmony(UniqueID).PatchAll()  ← BulletinBoardPatch applied to Billboard
```

Order matters: `I18nHelper.Init` must precede `PatchAll` so that if the patch fires during initialization (unlikely but safe to guard against), string lookups don't null-reference.

---

## Call Flow: Player Opens Bulletin Board

```
Player clicks bulletin board
  │
  └─ Stardew: Billboard.<target>() executes (vanilla behavior)
       │
       └─ Harmony postfix: BulletinBoardPatch.<target>_Postfix()
            │
            ├─ MultiplayerGuard.IsMultiplayer()?
            │    YES → ModEntry.Monitor.Log(I18nHelper.Get("multiplayer.refused_log_message"), Warn)
            │          return  (entry not added)
            │
            └─ NO → append ClickableComponent("Hire a Farmhand" label from I18nHelper)
                    to billboard's option list
```

---

## What U-09 Extends From This Unit

- `BulletinBoardPatch` — U-09 will replace the placeholder `"[Dayswork] Hire-flow placeholder opened"` log with a real call to `HiringFlowCoordinator.OpenMenu()`
- `i18n/default.json` — U-09 adds ~10 new keys for the TaskSelection and Summary menus
- `ModEntry` — U-09 wires `HiringFlowCoordinator` and `ContractPersistenceAdapter` into the composition root

---

## Test Coverage

Per the unit definition, U-08 is **play-tested** rather than unit-tested:
- `I18nHelper` has no domain logic worth testing in isolation (pure delegation to SMAPI API, which can't be exercised without loading the game)
- `MultiplayerGuard` is a one-liner wrapping a SMAPI context property — no test value
- `BulletinBoardPatch` behavior is verified by opening the bulletin board in-game

No files are added to `Dayswork.Tests` in this unit.
