# U-08 Code Generation Plan — Bulletin Board Hook

## Unit
U-08 — Bulletin Board Hook + i18n + Multiplayer Guard

## Stories Implemented
- **S-01** (full) — Player discovers the "Hire a Farmhand" entry on the Pelican Town bulletin board
- **S-18** (full) — In multiplayer, the entry is absent and the SMAPI log shows a friendly refusal

## Dependencies
- U-01 (Dayswork.csproj with `<EnableHarmony>true</EnableHarmony>` and SMAPI/SV references) — complete
- No Core (Dayswork.Core) types used in this unit

## Files to Create / Modify

| # | File | Action |
|---|---|---|
| 1 | `Dayswork/Guards/MultiplayerGuard.cs` | **Create** |
| 2 | `Dayswork/Integration/I18nHelper.cs` | **Create** |
| 3 | `Dayswork/Patches/BulletinBoardPatch.cs` | **Create** |
| 4 | `Dayswork/i18n/default.json` | **Modify** (add 2 keys) |
| 5 | `Dayswork/ModEntry.cs` | **Modify** (Monitor static + I18nHelper.Init + Harmony.PatchAll) |
| 6 | Build verification (`dotnet build`) | **Execute** |
| 7 | `aidlc-docs/construction/U-08-bulletin-board-hook/code/u-08-code-summary.md` | **Create** |
| 8 | `aidlc-docs/aidlc-state.md` + `aidlc-docs/audit.md` | **Update** |

---

## Steps

### Step 1 — Create `Dayswork/Guards/MultiplayerGuard.cs`
[ ]

Thin static class; single property delegation to `Context.IsMultiplayer`.

```csharp
using StardewModdingAPI;

namespace Dayswork.Guards;

internal static class MultiplayerGuard
{
    internal static bool IsMultiplayer() => Context.IsMultiplayer;
}
```

**NFR**: Stateless; re-evaluated per billboard open (handles edge cases).

---

### Step 2 — Create `Dayswork/Integration/I18nHelper.cs`
[ ]

Static wrapper initialized once in `ModEntry.Entry`. All string lookups in `Dayswork/` go through `Get(key)`.

```csharp
using StardewModdingAPI;

namespace Dayswork.Integration;

internal static class I18nHelper
{
    private static IModHelper _helper = null!;

    internal static void Init(IModHelper helper) => _helper = helper;

    internal static string Get(string key) =>
        _helper.Translation.Get(key).ToString();
}
```

**NFR-UX-02**: All user-visible strings go through this method.
**Safe fallback**: `Translation.Get(key)` returns the key itself if missing — never throws.

---

### Step 3 — Create `Dayswork/Patches/BulletinBoardPatch.cs`
[ ]

One class, three postfixes targeting `StardewValley.Menus.Billboard`. All three are declared in the same file (one file per patched class, NFR-MAINT-04).

**Onboarding note — Harmony multi-patch syntax**:
In HarmonyLib 2.x, you can declare multiple patches against the same class by putting the class-level `[HarmonyPatch(typeof(Billboard))]` attribute on the patch class, then adding method-level `[HarmonyPatch("MethodName")]` attributes on each individual postfix. Harmony resolves the combination of class-level + method-level attributes to determine the final target.

**Onboarding note — `__instance`**:
Harmony injects the target object as `__instance` when the target method is non-static. The parameter name is a Harmony convention — it's matched by name, not by position.

**Onboarding note — Constructor patching**:
`MethodType.Constructor` is used to target constructors. `Billboard(bool onlyViewDailyQuest = false)` is the constructor we want — after it runs, the menu dimensions are set, so we can compute where to place our button.

**Onboarding note — `SpriteBatch b`**:
Stardew renders everything with XNA/MonoGame's `SpriteBatch`. In draw postfixes, you receive the same `SpriteBatch` parameter the original method used. You can draw additional elements on it — they layer on top of whatever was already drawn.

```csharp
using Dayswork.Guards;
using Dayswork.Integration;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.Patches;

[HarmonyPatch(typeof(Billboard))]
internal static class BulletinBoardPatch
{
    // The ClickableComponent we add to the billboard.
    // Stored as a field so Constructor, Draw, and ReceiveLeftClick all see it.
    // Harmony creates a new patch method call each time, but static fields persist.
    // Each Billboard instance overwrites this — acceptable because only one billboard
    // is ever open at a time.
    private static ClickableComponent? _hireButton;

    // ── Constructor postfix ──────────────────────────────────────────────────
    [HarmonyPatch(MethodType.Constructor, new[] { typeof(bool) })]
    [HarmonyPostfix]
    private static void Constructor_Postfix(Billboard __instance)
    {
        // Position the "Hire a Farmhand" button in the bottom-right corner of
        // the billboard menu. Exact coordinates are adjusted against
        // __instance.xPositionOnScreen / yPositionOnScreen / width / height
        // (all set by the base IClickableMenu constructor before our postfix runs).
        _hireButton = new ClickableComponent(
            bounds: new Rectangle(
                __instance.xPositionOnScreen + __instance.width - 220,
                __instance.yPositionOnScreen + __instance.height - 80,
                200,
                60),
            name: "DaysworkHire",
            label: I18nHelper.Get("bulletin.hire_a_farmhand"));
    }

    // ── Draw postfix ─────────────────────────────────────────────────────────
    [HarmonyPatch(nameof(Billboard.draw))]
    [HarmonyPostfix]
    private static void Draw_Postfix(Billboard __instance, SpriteBatch b)
    {
        if (MultiplayerGuard.IsMultiplayer()) return;
        if (_hireButton is null) return;

        // Draw a simple dialogue box behind the label, then the label text.
        IClickableMenu.drawTextureBox(
            b,
            _hireButton.bounds.X,
            _hireButton.bounds.Y,
            _hireButton.bounds.Width,
            _hireButton.bounds.Height,
            Color.White);

        Utility.drawTextWithShadow(
            b,
            _hireButton.label,
            Game1.smallFont,
            new Vector2(_hireButton.bounds.X + 16, _hireButton.bounds.Y + 16),
            Game1.textColor);

        // Draw cursor on top so hover states render correctly.
        __instance.drawMouse(b);
    }

    // ── ReceiveLeftClick postfix ─────────────────────────────────────────────
    [HarmonyPatch(nameof(Billboard.receiveLeftClick))]
    [HarmonyPostfix]
    private static void ReceiveLeftClick_Postfix(Billboard __instance, int x, int y)
    {
        if (MultiplayerGuard.IsMultiplayer())
        {
            ModEntry.ModMonitor.Log(
                I18nHelper.Get("multiplayer.refused_log_message"),
                LogLevel.Warn);
            return;
        }

        if (_hireButton is null) return;
        if (!_hireButton.bounds.Contains(x, y)) return;

        // U-09 will replace this log with HiringFlowCoordinator.OpenMenu().
        ModEntry.ModMonitor.Log("[Dayswork] Hire-flow placeholder opened", LogLevel.Info);
    }
}
```

**Note on target method verification**: The `Billboard.draw(SpriteBatch b)` and `Billboard.receiveLeftClick(int x, int y)` signatures should be verified against the decompiled source in Visual Studio before or during generation. SMAPI's `ModBuildConfig` places decompiled SV sources in the right location for "Go to Definition" to work. If the signatures differ (e.g., additional overload parameters), the `[HarmonyPatch]` attribute may need `argumentTypes:` specified.

---

### Step 4 — Modify `Dayswork/i18n/default.json`
[ ]

Replace `{}` with the first two keys. All subsequent units append keys; existing keys are never removed.

```json
{
    "bulletin.hire_a_farmhand": "Hire a Farmhand",
    "multiplayer.refused_log_message": "Dayswork is single-player only. The hiring option has been disabled for this multiplayer session."
}
```

---

### Step 5 — Modify `Dayswork/ModEntry.cs`
[ ]

Extend the U-01 stub to:
1. Expose `internal static IMonitor ModMonitor` (named `ModMonitor` to avoid shadowing `Mod.Monitor`)
2. Call `I18nHelper.Init(helper)` before any code that needs string lookups
3. Apply Harmony patches via `new Harmony(this.ModManifest.UniqueID).PatchAll()`
4. Keep the existing startup log line

**Onboarding note — `Harmony(UniqueID).PatchAll()`**:
`PatchAll()` scans every type in the calling assembly for `[HarmonyPatch]` attributes and applies them. Passing `this.ModManifest.UniqueID` as the Harmony ID ensures conflict diagnostics in SMAPI's log name the correct mod when a patch error occurs.

**Onboarding note — `internal static IMonitor ModMonitor`**:
Harmony patch classes are `static` — they can't receive constructor injection. The standard SMAPI mod pattern for sharing the `IMonitor` with static patch classes is to expose it as a `static` property on `ModEntry`. We call it `ModMonitor` (not `Monitor`) because `Mod` already has a non-static `Monitor` property inherited from the base class; shadowing it would be confusing.

```csharp
using Dayswork.Integration;
using HarmonyLib;
using StardewModdingAPI;

namespace Dayswork;

public sealed class ModEntry : Mod
{
    internal static IMonitor ModMonitor { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        ModMonitor = this.Monitor;
        I18nHelper.Init(helper);

        var harmony = new Harmony(this.ModManifest.UniqueID);
        harmony.PatchAll();

        this.Monitor.Log("Dayswork loaded", LogLevel.Info);
    }
}
```

---

### Step 6 — Build verification
[ ]

```
dotnet build
```

Expected: **0 errors, 0 warnings**. If Harmony can't resolve the Billboard target method at build time (it resolves at runtime), the build still succeeds — a method-not-found error would surface only when the game loads the mod. Verify in SMAPI console at load time if needed.

---

### Step 7 — Create code summary doc
[ ]

`aidlc-docs/construction/U-08-bulletin-board-hook/code/u-08-code-summary.md`

---

### Step 8 — Update state and audit
[ ]

- Mark U-08 Code Generation complete in `aidlc-state.md`
- Append completion entry to `audit.md`

---

## PBT Compliance Summary

| Rule | Status |
|---|---|
| PBT-02 | N/A — no serialization |
| PBT-03 | N/A — no domain invariants |
| PBT-07 | N/A — no new generators |
| PBT-08 | N/A — no PBT tests |
| PBT-09 | Already decided (FsCheck) |

## NFR Compliance Summary

| NFR | Status | Evidence |
|---|---|---|
| NFR-MAINT-04 | Compliant | `BulletinBoardPatch.cs` in `Dayswork/Patches/`; one file, all billboard patches |
| NFR-UX-02 | Compliant | All strings via `I18nHelper.Get(key)`; `default.json` populated |
| FR-MP-01 | Compliant | Guard checked in Draw and ReceiveLeftClick; log emitted in MP |
| NFR-ONBOARD-01 | Compliant | Harmony anatomy, SpriteBatch, Context.IsMultiplayer, PatchAll all explained in plan |
