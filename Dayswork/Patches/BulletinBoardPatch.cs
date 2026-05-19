using Dayswork.Guards;
using Dayswork.Integration;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.Patches;

// One class per patched game class (NFR-MAINT-04).
// Three postfixes cover the full bulletin-board interaction lifecycle:
//   Constructor  → injects our ClickableComponent into the live menu
//   draw         → renders the button on top of the vanilla board
//   receiveLeftClick → detects a click on our button and responds
[HarmonyPatch(typeof(Billboard))]
internal static class BulletinBoardPatch
{
    // Persists across the three postfix calls for a single Billboard instance.
    // Safe because only one Billboard is ever open at a time.
    private static ClickableComponent? _hireButton;

    // ── Constructor postfix ──────────────────────────────────────────────────
    // Runs after Billboard(bool onlyViewDailyQuest) completes, so xPositionOnScreen /
    // yPositionOnScreen / width / height are already set by IClickableMenu's base ctor.
    [HarmonyPatch(MethodType.Constructor, new[] { typeof(bool) })]
    [HarmonyPostfix]
    private static void Constructor_Postfix(Billboard __instance, bool dailyQuest)
    {
        // Our entry belongs on the help wanted (dailyQuest = true) board,
        // not the calendar (dailyQuest = false).
        if (!dailyQuest)
        {
            _hireButton = null;
            return;
        }

        string label = I18nHelper.Get("bulletin.hire_a_farmhand");
        int buttonWidth = (int)Game1.smallFont.MeasureString(label).X + 32; // 16px padding each side
        int buttonHeight = 60;

        _hireButton = new ClickableComponent(
            bounds: new Rectangle(
                __instance.xPositionOnScreen + __instance.width / 2 - buttonWidth / 2,
                __instance.yPositionOnScreen + 16,
                buttonWidth,
                buttonHeight),
            name: "DaysworkHire",
            label: label);
    }

    // ── Draw postfix ─────────────────────────────────────────────────────────
    // Runs after Billboard.draw(SpriteBatch b). The vanilla board has already
    // rendered; we layer our button on top and redraw the cursor last so it
    // sits above our new content.
    [HarmonyPatch(nameof(Billboard.draw))]
    [HarmonyPostfix]
    private static void Draw_Postfix(Billboard __instance, SpriteBatch b)
    {
        if (MultiplayerGuard.IsMultiplayer()) return;
        if (_hireButton is null) return;

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

        // Redraw cursor so it sits above our button content.
        __instance.drawMouse(b);
    }

    // ── ReceiveLeftClick postfix ─────────────────────────────────────────────
    // Runs after Billboard.receiveLeftClick(int x, int y, bool playSound).
    // We use only x and y; Harmony matches parameters by name so omitting
    // playSound is safe.
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

        // U-09 replaces this with HiringFlowCoordinator.OpenMenu().
        ModEntry.ModMonitor.Log("[Dayswork] Hire-flow placeholder opened", LogLevel.Info);
    }
}
