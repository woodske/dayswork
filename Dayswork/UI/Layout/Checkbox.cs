using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI.Layout;

internal sealed class Checkbox : ILayoutElement, IHasDesiredWidth
{
    private readonly bool _isChecked;
    private readonly Action<bool> _onToggle;
    private readonly bool _enabled;
    private readonly int _size;

    private Rectangle _bounds;

    public Checkbox(bool isChecked, Action<bool> onToggle, bool enabled = true, int size = 36)
    {
        _isChecked = isChecked;
        _onToggle = onToggle;
        _enabled = enabled;
        _size = size;
    }

    public int DesiredWidth => _size;

    public int Measure(int availableWidth) => _size;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        var y = bounds.Y + Math.Max(0, bounds.Height - _size) / 2;
        _bounds = new Rectangle(bounds.X, y, _size, _size);
        if (_enabled)
        {
            ctx.Register(_bounds, "checkbox", string.Empty, () =>
            {
                Game1.playSound("drumkit6");
                _onToggle(!_isChecked);
            });
        }
    }

    public void Draw(SpriteBatch b)
    {
        var tint = _enabled ? Color.White : Color.Gray;
        IClickableMenu.drawTextureBox(b, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, tint);

        if (_isChecked)
        {
            var inner = new Rectangle(_bounds.X + 8, _bounds.Y + 8, _bounds.Width - 16, _bounds.Height - 16);
            b.Draw(Game1.staminaRect, inner, Color.LimeGreen);
        }
    }
}
