# NFR Design Patterns — U-08 Bulletin Board Hook

## Unit
U-08 — Bulletin Board Hook + i18n + Multiplayer Guard

---

## Pattern 1 — Harmony Patch Isolation (NFR-MAINT-04)

### Problem
Harmony patches are global — a bug in any patch can crash any player's game. In the Stardew modding ecosystem, multiple mods often patch the same methods, and it's critical to be able to quickly identify which mod owns which patch.

### Design
One static class per patched method, living exclusively in `Dayswork/Patches/`:

```
Dayswork/
└── Patches/
    └── BulletinBoardPatch.cs     ← only file in U-08; more added in future units if needed
```

**Naming convention**: `{TargetClass}Patch.cs` — unambiguous which game class is being patched.

**Patch registration**: `new Harmony(ModManifest.UniqueID).PatchAll()` in `ModEntry.Entry()`. `PatchAll()` discovers all `[HarmonyPatch]`-attributed classes automatically; no manual enumeration.

**Patch method visibility**: `private static` — the patch method is an implementation detail of the class; it is not part of any public API.

### Postfix anatomy for U-08

```csharp
[HarmonyPatch(typeof(Billboard), "<target-method>")]
internal static class BulletinBoardPatch
{
    [HarmonyPostfix]
    private static void <TargetMethod>_Postfix(Billboard __instance /*, other params */)
    {
        if (MultiplayerGuard.IsMultiplayer()) return;   // short-circuit
        // append "Hire a Farmhand" entry
    }
}
```

**Why postfix over prefix**: We are *appending* to whatever the vanilla bulletin board renders. Prefix would require us to reason about what hasn't happened yet. Postfix is the correct hook for "add to existing output."

**Guard call placement**: The multiplayer check lives *inside* the postfix rather than preventing patch registration. This matches the spec requirement for a stateless re-evaluation per menu open (handles edge cases like a player switching between SP and MP saves without restarting).

---

## Pattern 2 — i18n Routing via Static Wrapper (NFR-UX-02)

### Problem
Stardew's i18n API is tied to `IModHelper`, which is an instance obtained at mod startup. Every piece of code that needs a translated string must either receive `IModHelper` as a dependency or reach it another way — without making the call site verbose.

### Design
`I18nHelper` is a **static class** initialized once at mod startup with the `IModHelper` instance:

```csharp
// Dayswork/Integration/I18nHelper.cs
internal static class I18nHelper
{
    private static IModHelper _helper = null!;

    internal static void Init(IModHelper helper) => _helper = helper;

    internal static string Get(string key) =>
        _helper.Translation.Get(key).ToString();
}
```

`ModEntry.Entry()` calls `I18nHelper.Init(helper)` before any other setup.

**Call sites** anywhere in `Dayswork/`:
```csharp
string label = I18nHelper.Get("bulletin.hire_a_farmhand");
```

**Why static**: `I18nHelper` has no testable domain logic — it's a pure delegation to the SMAPI framework API. There's nothing to mock in `Dayswork.Tests` (the test project can't reference `Dayswork/` per component-dependency.md rule 2). The static approach avoids dependency injection plumbing for a zero-logic adapter.

**Safe fallback**: If a key is missing from `i18n/default.json`, `Translation.Get(key)` returns the key string itself — it never throws. This means a missing translation degrades gracefully (key name shows in UI) rather than crashing.

**Keys established in U-08**:

| Key | English | Used by |
|---|---|---|
| `bulletin.hire_a_farmhand` | `"Hire a Farmhand"` | `BulletinBoardPatch` |
| `multiplayer.refused_log_message` | `"Dayswork is single-player only. The hiring option has been disabled for this multiplayer session."` | `MultiplayerGuard` |

Future units extend `i18n/default.json` by adding new keys. The file grows but keys are never removed (would break translations contributed by the community).

---

## Pattern 3 — Stateless Multiplayer Guard (FR-MP-01 / NFR-COMPAT-03)

### Problem
Stardew supports multiplayer, but this mod is single-player only. The bulletin board patch must not activate in a multiplayer session, and the player (or host/farmhand) should receive a clear explanation in the SMAPI log.

### Design
`MultiplayerGuard` is a **static utility class** with a single method:

```csharp
// Dayswork/Guards/MultiplayerGuard.cs
internal static class MultiplayerGuard
{
    internal static bool IsMultiplayer() => Context.IsMultiplayer;
}
```

**Why so thin?** The current requirement is purely a boolean check. Keeping this as a named class rather than inlining `Context.IsMultiplayer` everywhere gives a single rename point if the check ever needs to grow (e.g., checking `Context.IsMainPlayer` for future co-op host support).

**Log message pattern**: The friendly warning is emitted by `BulletinBoardPatch` when the guard fires, not by `MultiplayerGuard` itself — the guard doesn't know about logging, which would be a second responsibility:

```csharp
if (MultiplayerGuard.IsMultiplayer())
{
    ModEntry.Monitor.Log(I18nHelper.Get("multiplayer.refused_log_message"), LogLevel.Warn);
    return;
}
```

`ModEntry.Monitor` is exposed as an `internal static IMonitor` property set in `Entry()` — a standard SMAPI mod pattern for accessing the monitor from patch classes (which can't receive constructor injection since they're instantiated by Harmony).

**Stateless re-evaluation**: The check runs each time the bulletin board opens. This handles the edge case where a player loads a single-player save, then somehow enters a multiplayer context — the guard adapts without needing to subscribe to lifecycle events.
