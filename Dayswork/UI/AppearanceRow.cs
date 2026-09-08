using Dayswork.UI.Layout;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace Dayswork.UI;

/// <summary>
/// The Preferences spoke's Appearance picker: arrows either side of a live preview of the worker's
/// own down-facing idle frame, drawn from the same variant asset the spawned NPC uses — so what the
/// player sees here is the sprite, not a mock-up of it.
/// </summary>
internal sealed class AppearanceRow : ILayoutElement
{
    private const int Height = 96;

    /// <summary>Texels-to-pixels for the preview. The world draws the worker at 4×; 3× keeps the
    /// palette perfectly readable while the row still fits beside the other preference rows.</summary>
    private const int PreviewScale = 3;
    private const int ArrowWidth = 48;
    private const int ArrowHeight = 44;

    private static readonly Rectangle LeftArrowSource = new(352, 495, 12, 11);
    private static readonly Rectangle RightArrowSource = new(365, 495, 12, 11);

    private readonly string _label;
    private readonly string _appearanceKey;
    private readonly string _appearanceName;
    private readonly Action _onPrevious;
    private readonly Action _onNext;

    private Rectangle _bounds;
    private Rectangle _leftArrow;
    private Rectangle _rightArrow;
    private Rectangle _preview;

    internal AppearanceRow(
        string label,
        string appearanceKey,
        string appearanceName,
        Action onPrevious,
        Action onNext)
    {
        _label = label;
        _appearanceKey = appearanceKey;
        _appearanceName = appearanceName;
        _onPrevious = onPrevious;
        _onNext = onNext;
    }

    public int Measure(int availableWidth) => Height;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        _bounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, Height);

        var previewWidth = 16 * PreviewScale;
        var previewHeight = 32 * PreviewScale;
        _preview = new Rectangle(
            _bounds.Right - 24 - previewWidth,
            _bounds.Y + (Height - previewHeight) / 2,
            previewWidth,
            previewHeight);

        _rightArrow = new Rectangle(
            _preview.X - 12 - ArrowWidth,
            _bounds.Y + (Height - ArrowHeight) / 2,
            ArrowWidth,
            ArrowHeight);

        _leftArrow = new Rectangle(
            _rightArrow.X - 200 - ArrowWidth,
            _rightArrow.Y,
            ArrowWidth,
            ArrowHeight);

        ctx.Register(_leftArrow, "appearance.previous", _label, () =>
        {
            Game1.playSound("shwip");
            _onPrevious();
        });

        ctx.Register(_rightArrow, "appearance.next", _label, () =>
        {
            Game1.playSound("shwip");
            _onNext();
        });
    }

    public void Draw(SpriteBatch b)
    {
        Utility.drawTextWithShadow(
            b,
            _label,
            Game1.smallFont,
            new Vector2(_bounds.X + 8, _bounds.Y + (Height - (int)Game1.smallFont.MeasureString(_label).Y) / 2),
            Game1.textColor);

        b.Draw(Game1.mouseCursors, _leftArrow, LeftArrowSource, Color.White);
        b.Draw(Game1.mouseCursors, _rightArrow, RightArrowSource, Color.White);

        var nameSize = Game1.smallFont.MeasureString(_appearanceName);
        Utility.drawTextWithShadow(
            b,
            _appearanceName,
            Game1.smallFont,
            new Vector2(
                _leftArrow.Right + ((_rightArrow.X - _leftArrow.Right) - nameSize.X) / 2,
                _bounds.Y + (Height - nameSize.Y) / 2),
            Game1.textColor);

        var sheet = TryLoadPreviewSheet();
        if (sheet is null)
            return;

        // Frame 0 — the down-facing idle pose (docs/game-data/farmhand-art.md).
        b.Draw(sheet, _preview, new Rectangle(0, 0, 16, 32), Color.White);
    }

    private Texture2D? TryLoadPreviewSheet()
    {
        try
        {
            return Game1.content.Load<Texture2D>(FarmhandAppearance.SpriteAssetFor(_appearanceKey));
        }
        catch (Exception ex)
        {
            DevLog.Log($"[Dayswork][ui] Could not load the appearance preview sheet: {ex.Message}", LogLevel.Trace);
            return null;
        }
    }
}
