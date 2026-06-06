using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI.Layout;

internal sealed class ToggleRow : ILayoutElement
{
    private readonly string _label;
    private readonly bool _isChecked;
    private readonly Action _onToggle;
    private readonly bool _enabled;
    private readonly int _height;

    private Rectangle _bounds;

    public ToggleRow(
        string label,
        bool isChecked,
        Action onToggle,
        bool enabled = true,
        int height = 54)
    {
        _label = label;
        _isChecked = isChecked;
        _onToggle = onToggle;
        _enabled = enabled;
        _height = height;
    }

    public int Measure(int availableWidth) => _height;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        _bounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, _height);
        if (_enabled)
        {
            ctx.Register(_bounds, _label, _label, () =>
            {
                Game1.playSound("drumkit6");
                _onToggle();
            });
        }
    }

    public void Draw(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(
            b, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, Color.White);

        var checkboxSrc = new Rectangle(_isChecked ? 236 : 227, 425, 9, 9);
        b.Draw(
            Game1.mouseCursors,
            new Rectangle(_bounds.X + 8, _bounds.Y + (_bounds.Height - 36) / 2, 36, 36),
            checkboxSrc,
            Color.White);

        Utility.drawTextWithShadow(
            b,
            _label,
            Game1.smallFont,
            new Vector2(_bounds.X + 52, _bounds.Y + (_bounds.Height - (int)Game1.smallFont.MeasureString(_label).Y) / 2),
            Game1.textColor);
    }
}
