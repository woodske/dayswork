using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI.Layout;

internal sealed class MenuButton : ILayoutElement, IHasDesiredWidth
{
    private readonly string _label;
    private readonly Action _onClick;
    private readonly bool _enabled;
    private readonly int? _fixedWidth;
    private readonly int _height;
    private readonly string? _trailingText;
    private readonly Color _trailingColor;
    private readonly string _sound;
    private readonly HAlign _textAlign;

    private Rectangle _bounds;

    public MenuButton(
        string label,
        Action onClick,
        bool enabled = true,
        int? fixedWidth = null,
        int height = 56,
        string? trailingText = null,
        Color? trailingColor = null,
        string sound = "smallSelect",
        HAlign textAlign = HAlign.Center)
    {
        _label = label;
        _onClick = onClick;
        _enabled = enabled;
        _fixedWidth = fixedWidth;
        _height = height;
        _trailingText = trailingText;
        _trailingColor = trailingColor ?? Game1.textColor;
        _sound = sound;
        _textAlign = textAlign;
    }

    public int DesiredWidth =>
        _fixedWidth ?? (int)Math.Ceiling(Game1.smallFont.MeasureString(_label).X) + 40;

    public int Measure(int availableWidth) => _height;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        var y = bounds.Y + Math.Max(0, bounds.Height - _height) / 2;
        _bounds = new Rectangle(bounds.X, y, bounds.Width, _height);
        if (_enabled)
        {
            ctx.Register(_bounds, _label, _label, () =>
            {
                Game1.playSound(_sound);
                _onClick();
            });
        }
    }

    public void Draw(SpriteBatch b)
    {
        var tint = _enabled ? Color.White : Color.Gray;
        var textTint = _enabled ? Game1.textColor : Color.Gray;

        IClickableMenu.drawTextureBox(
            b, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, tint);

        var displayText = Truncate(_label, _bounds.Width - 20);
        var textSize = Game1.smallFont.MeasureString(displayText);

        var textX = _textAlign switch
        {
            HAlign.Left => _bounds.X + 10,
            HAlign.Right => _bounds.Right - 10 - (int)textSize.X,
            _ => _trailingText is not null
                ? _bounds.X + 20
                : _bounds.X + (_bounds.Width - (int)textSize.X) / 2,
        };

        Utility.drawTextWithShadow(
            b,
            displayText,
            Game1.smallFont,
            new Vector2(textX, _bounds.Y + (_bounds.Height - (int)textSize.Y) / 2),
            textTint);

        if (_trailingText is not null)
        {
            var trailSize = Game1.smallFont.MeasureString(_trailingText);
            Utility.drawTextWithShadow(
                b,
                _trailingText,
                Game1.smallFont,
                new Vector2(
                    _bounds.Right - 20 - trailSize.X,
                    _bounds.Y + (_bounds.Height - (int)trailSize.Y) / 2),
                _trailingColor);
        }
    }

    private static string Truncate(string label, int maxWidth)
    {
        if (string.IsNullOrEmpty(label) || Game1.smallFont.MeasureString(label).X <= maxWidth)
            return label;

        var text = label;
        while (text.Length > 1 && Game1.smallFont.MeasureString(text + "…").X > maxWidth)
            text = text[..^1];

        return text + "…";
    }
}
