using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

internal static class MenuScrollBar
{
    private const int Scale = 4;
    private const int ArrowWidth = 11 * Scale;
    private const int ArrowHeight = 12 * Scale;
    private const int RunnerWidth = 6 * Scale;
    private const int RunnerGap = 4;
    private const int ThumbMinHeight = 40;

    // ShopCachedTheme is PC-only — no static field reference; loaded via reflection at runtime.
    private record struct ScrollTheme(
        Texture2D UpTex,   Rectangle UpSrc,
        Texture2D DownTex, Rectangle DownSrc,
        Texture2D BackTex, Rectangle BackSrc,
        Texture2D FrontTex, Rectangle FrontSrc);

    private static ScrollTheme? _theme;

    public const int Gap = 8;
    public const int ReservedWidth = ArrowWidth + Gap;

    public static bool IsVisible(int totalCount, int visibleCount) => totalCount > visibleCount;

    public static Rectangle GetUpArrowRect(Rectangle viewportRect) =>
        new(viewportRect.Right + Gap, viewportRect.Y, ArrowWidth, ArrowHeight);

    public static Rectangle GetDownArrowRect(Rectangle viewportRect) =>
        new(viewportRect.Right + Gap, viewportRect.Bottom - ArrowHeight, ArrowWidth, ArrowHeight);

    public static Rectangle GetRunnerRect(Rectangle viewportRect)
    {
        var upArrow = GetUpArrowRect(viewportRect);
        var downArrow = GetDownArrowRect(viewportRect);
        var runnerX = upArrow.X + (ArrowWidth - RunnerWidth) / 2;
        var runnerY = upArrow.Bottom + RunnerGap;
        var runnerHeight = Math.Max(RunnerWidth, downArrow.Y - runnerY - RunnerGap);
        return new Rectangle(runnerX, runnerY, RunnerWidth, runnerHeight);
    }

    public static Rectangle GetThumbRect(Rectangle viewportRect, int visibleCount, int totalCount, int scrollIndex)
    {
        if (!IsVisible(totalCount, visibleCount))
            return Rectangle.Empty;

        var runner = GetRunnerRect(viewportRect);
        var thumbHeight = Math.Clamp(runner.Height * visibleCount / totalCount, ThumbMinHeight, runner.Height);
        var maxScroll = Math.Max(1, totalCount - visibleCount);
        var travel = Math.Max(0, runner.Height - thumbHeight);
        var thumbTop = runner.Y + (int)Math.Round(travel * (scrollIndex / (double)maxScroll));
        return new Rectangle(runner.X, thumbTop, runner.Width, thumbHeight);
    }

    public static void Draw(SpriteBatch b, Rectangle viewportRect, int visibleCount, int totalCount, int scrollIndex)
    {
        if (!IsVisible(totalCount, visibleCount))
            return;

        var upArrow = GetUpArrowRect(viewportRect);
        var downArrow = GetDownArrowRect(viewportRect);
        var runner = GetRunnerRect(viewportRect);
        var thumb = GetThumbRect(viewportRect, visibleCount, totalCount, scrollIndex);
        var t = GetTheme();

        DrawScaledTexture(b, t.UpTex,    upArrow,   t.UpSrc,    Color.White);
        DrawScaledTexture(b, t.DownTex,  downArrow, t.DownSrc,  Color.White);
        DrawTiledVertical(b, t.BackTex,  t.BackSrc,  runner);
        DrawTiledVertical(b, t.FrontTex, t.FrontSrc, thumb);
    }

    private static ScrollTheme GetTheme()
    {
        if (_theme.HasValue) return _theme.Value;
        _theme = TryLoadViaReflection() ?? BuildCursorsTheme();
        return _theme.Value;
    }

    private static ScrollTheme? TryLoadViaReflection()
    {
        var type = typeof(ShopMenu).GetNestedType("ShopCachedTheme",
            BindingFlags.Public | BindingFlags.NonPublic);
        if (type == null) return null;
        try
        {
            var instance = Activator.CreateInstance(type, new object?[] { null });
            Texture2D GetTex(string name) => (Texture2D)type.GetProperty(name)!.GetValue(instance)!;
            Rectangle GetRect(string name) => (Rectangle)type.GetProperty(name)!.GetValue(instance)!;
            var t = new ScrollTheme(
                GetTex("ScrollUpTexture"),       GetRect("ScrollUpSourceRect"),
                GetTex("ScrollDownTexture"),     GetRect("ScrollDownSourceRect"),
                GetTex("ScrollBarBackTexture"),  GetRect("ScrollBarBackSourceRect"),
                GetTex("ScrollBarFrontTexture"), GetRect("ScrollBarFrontSourceRect"));
            return t;
        }
        catch { return null; }
    }

    private static ScrollTheme BuildCursorsTheme()
    {
        // Fallback for platforms where ShopCachedTheme is absent (Android).
        // Rects are standard vanilla Cursors values; run on PC with DevLog.Enabled to verify.
        var tex = Game1.mouseCursors;
        return new ScrollTheme(
            tex, new Rectangle(421, 459, 11, 12),
            tex, new Rectangle(421, 472, 11, 12),
            tex, new Rectangle(403, 383, 6,  6),
            tex, new Rectangle(435, 463, 6, 10));
    }

    public static bool TryBeginDrag(
        Rectangle viewportRect,
        int visibleCount,
        int totalCount,
        int scrollIndex,
        int x,
        int y,
        out int dragOffset)
    {
        var thumb = GetThumbRect(viewportRect, visibleCount, totalCount, scrollIndex);
        if (!thumb.Contains(x, y))
        {
            dragOffset = 0;
            return false;
        }

        dragOffset = y - thumb.Y;
        return true;
    }

    public static bool UpArrowContains(Rectangle viewportRect, int totalCount, int visibleCount, int x, int y) =>
        IsVisible(totalCount, visibleCount) && GetUpArrowRect(viewportRect).Contains(x, y);

    public static bool DownArrowContains(Rectangle viewportRect, int totalCount, int visibleCount, int x, int y) =>
        IsVisible(totalCount, visibleCount) && GetDownArrowRect(viewportRect).Contains(x, y);

    public static bool TrackContains(Rectangle viewportRect, int visibleCount, int totalCount, int x, int y) =>
        IsVisible(totalCount, visibleCount) && GetRunnerRect(viewportRect).Contains(x, y);

    public static int GetTrackClickScrollIndex(
        Rectangle viewportRect,
        int visibleCount,
        int totalCount,
        int currentIndex,
        int clickY)
    {
        if (!IsVisible(totalCount, visibleCount))
            return 0;

        var thumb = GetThumbRect(viewportRect, visibleCount, totalCount, currentIndex);
        var pageSize = Math.Max(1, visibleCount - 1);

        if (clickY < thumb.Y)
            return Math.Max(0, currentIndex - pageSize);

        if (clickY > thumb.Bottom)
            return Math.Min(totalCount - visibleCount, currentIndex + pageSize);

        return currentIndex;
    }

    public static int GetDragScrollIndex(
        Rectangle viewportRect,
        int visibleCount,
        int totalCount,
        int dragOffset,
        int mouseY)
    {
        if (!IsVisible(totalCount, visibleCount))
            return 0;

        var runner = GetRunnerRect(viewportRect);
        var thumb = GetThumbRect(viewportRect, visibleCount, totalCount, 0);
        var thumbHeight = thumb.Height;
        var minThumbTop = runner.Y;
        var maxThumbTop = runner.Bottom - thumbHeight;
        var desiredThumbTop = Math.Clamp(mouseY - dragOffset, minThumbTop, maxThumbTop);
        var travel = Math.Max(1, maxThumbTop - minThumbTop);
        var ratio = (desiredThumbTop - minThumbTop) / (double)travel;
        var maxScroll = Math.Max(0, totalCount - visibleCount);

        return (int)Math.Round(ratio * maxScroll);
    }

    private static void DrawScaledTexture(SpriteBatch b, Texture2D texture, Rectangle destination, Rectangle sourceRect, Color color) =>
        b.Draw(texture, destination, sourceRect, color);

    private static void DrawTiledVertical(SpriteBatch b, Texture2D texture, Rectangle sourceRect, Rectangle destination)
    {
        var y = destination.Y;
        while (y < destination.Bottom)
        {
            var height = Math.Min(sourceRect.Height * Scale, destination.Bottom - y);
            b.Draw(
                texture,
                new Rectangle(destination.X, y, destination.Width, height),
                sourceRect,
                Color.White);
            y += height;
        }
    }
}
