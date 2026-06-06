using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Dayswork.UI.Layout;

internal enum LabelFont { Small, Dialogue }
internal enum HAlign { Left, Center, Right }

internal sealed class Label : ILayoutElement, IHasDesiredWidth
{
    private readonly string _text;
    private readonly LabelFont _font;
    private readonly Color _color;
    private readonly bool _wrap;
    private readonly bool _ellipsize;
    private readonly HAlign _align;

    private Rectangle _bounds;
    private string _rendered = string.Empty;

    public Label(
        string text,
        LabelFont font = LabelFont.Small,
        Color? color = null,
        bool wrap = false,
        bool ellipsize = false,
        HAlign align = HAlign.Left)
    {
        _text = text;
        _font = font;
        _color = color ?? Game1.textColor;
        _wrap = wrap;
        _ellipsize = ellipsize;
        _align = align;
    }

    public int DesiredWidth => (int)Math.Ceiling(Font.MeasureString(_text).X);

    private SpriteFont Font => _font == LabelFont.Dialogue ? Game1.dialogueFont : Game1.smallFont;

    public int Measure(int availableWidth)
    {
        if (_wrap)
        {
            _rendered = Game1.parseText(_text, Font, availableWidth);
            return (int)Math.Ceiling(Font.MeasureString(_rendered).Y);
        }

        _rendered = _text;
        return (int)Math.Ceiling(Font.MeasureString(_text).Y);
    }

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        _bounds = bounds;
    }

    public void Draw(SpriteBatch b)
    {
        if (string.IsNullOrEmpty(_rendered))
            return;

        var rendered = _ellipsize && !_wrap
            ? Truncate(_rendered, _bounds.Width)
            : _rendered;
        var size = Font.MeasureString(rendered);
        var x = _align switch
        {
            HAlign.Center => _bounds.X + (_bounds.Width - (int)size.X) / 2,
            HAlign.Right => _bounds.X + _bounds.Width - (int)size.X,
            _ => _bounds.X,
        };

        var y = _bounds.Y + Math.Max(0, _bounds.Height - (int)size.Y) / 2;
        Utility.drawTextWithShadow(b, rendered, Font, new Vector2(x, y), _color);
    }

    private string Truncate(string text, int maxWidth)
    {
        if (string.IsNullOrEmpty(text) || Font.MeasureString(text).X <= maxWidth)
            return text;

        var candidate = text;
        while (candidate.Length > 1 && Font.MeasureString(candidate + "…").X > maxWidth)
            candidate = candidate[..^1];

        return candidate + "…";
    }
}
