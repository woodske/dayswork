using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Dayswork.UI.Layout;

internal sealed class Separator : ILayoutElement
{
    private readonly int _height;
    private readonly float _opacity;

    private Rectangle _bounds;

    public Separator(int height = 1, float opacity = 0.5f)
    {
        _height = height;
        _opacity = opacity;
    }

    public int Measure(int availableWidth) => _height;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        _bounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, _height);
    }

    public void Draw(SpriteBatch b)
    {
        b.Draw(Game1.staminaRect, _bounds, Color.LightGray * _opacity);
    }
}
