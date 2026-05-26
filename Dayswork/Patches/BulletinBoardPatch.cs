using Dayswork.Core.Guards;
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
//   Constructor      → injects our ClickableComponent and snaps the gamepad cursor to it
//   draw             → renders the button on top of the vanilla board
//   receiveLeftClick → detects mouse clicks and gamepad A presses (SDV converts A to a
//                      left-click at the cursor position; the cursor is already snapped
//                      to our button by the constructor postfix)
[HarmonyPatch(typeof(Billboard))]
internal static class BulletinBoardPatch
{
    // Both buttons persist across the three postfix calls for a single Billboard instance.
    // Safe because only one Billboard is ever open at a time.
    private static ClickableComponent? _hireButton;
    private static ClickableComponent? _manageButton;

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
            _hireButton   = null;
            _manageButton = null;
            return;
        }

        const int ButtonHeight = 60;
        const int ButtonGap    = 16;
        int topY = __instance.yPositionOnScreen + 16;

        string hireLabel   = I18nHelper.Get("bulletin.hire_a_farmhand");
        string manageLabel = I18nHelper.Get("bulletin.manage_contracts");
        int hireWidth   = (int)Game1.smallFont.MeasureString(hireLabel).X   + 32;
        int manageWidth = (int)Game1.smallFont.MeasureString(manageLabel).X + 32;

        // Place both buttons side-by-side, centered within the billboard panel.
        int totalWidth = hireWidth + ButtonGap + manageWidth;
        int startX     = __instance.xPositionOnScreen + (__instance.width - totalWidth) / 2;

        _hireButton = new ClickableComponent(
            bounds: new Rectangle(startX, topY, hireWidth, ButtonHeight),
            name: "DaysworkHire",
            label: hireLabel)
        {
            myID            = 999,
            rightNeighborID = 998,
            upNeighborID    = -1,
            downNeighborID  = -1,
            leftNeighborID  = -1,
        };

        _manageButton = new ClickableComponent(
            bounds: new Rectangle(startX + hireWidth + ButtonGap, topY, manageWidth, ButtonHeight),
            name: "DaysworkManage",
            label: manageLabel)
        {
            myID            = 998,
            leftNeighborID  = 999,
            upNeighborID    = -1,
            downNeighborID  = -1,
            rightNeighborID = -1,
        };

        __instance.allClickableComponents ??= new System.Collections.Generic.List<ClickableComponent>();
        __instance.allClickableComponents.Add(_hireButton);
        __instance.allClickableComponents.Add(_manageButton);
        __instance.currentlySnappedComponent = _hireButton;
        __instance.snapCursorToCurrentSnappedComponent();
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

        DrawBulletinButton(b, _hireButton);

        if (_manageButton is not null)
            DrawBulletinButton(b, _manageButton);

        // Redraw cursor so it sits above our button content.
        __instance.drawMouse(b);
    }

    private static void DrawBulletinButton(SpriteBatch b, ClickableComponent btn)
    {
        IClickableMenu.drawTextureBox(
            b,
            btn.bounds.X,
            btn.bounds.Y,
            btn.bounds.Width,
            btn.bounds.Height,
            Color.White);

        Utility.drawTextWithShadow(
            b,
            btn.label,
            Game1.smallFont,
            new Vector2(btn.bounds.X + 16, btn.bounds.Y + 16),
            Game1.textColor);
    }

    // ── ReceiveLeftClick postfix ─────────────────────────────────────────────
    // Runs after Billboard.receiveLeftClick(int x, int y, bool playSound).
    // We use only x and y; Harmony matches parameters by name so omitting
    // playSound is safe.
    [HarmonyPatch(nameof(Billboard.receiveLeftClick))]
    [HarmonyPostfix]
    private static void ReceiveLeftClick_Postfix(Billboard __instance, int x, int y)
    {
        var action = BulletinBoardInteractionPolicy.Evaluate(
            MultiplayerGuard.IsMultiplayer(),
            _hireButton is not null && _hireButton.bounds.Contains(x, y),
            _manageButton is not null && _manageButton.bounds.Contains(x, y));

        switch (action)
        {
            case BulletinBoardInteractionAction.BlockedByMultiplayer:
                ModEntry.ModMonitor.Log(
                    I18nHelper.Get("multiplayer.refused_log_message"),
                    LogLevel.Warn);
                return;
            case BulletinBoardInteractionAction.OpenHire:
                ModEntry.Coordinator.OpenHiringFlow();
                return;
            case BulletinBoardInteractionAction.OpenManage:
                ModEntry.Coordinator.OpenManageFlow();
                return;
        }
    }
}
