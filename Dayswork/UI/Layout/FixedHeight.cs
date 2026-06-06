using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dayswork.UI.Layout;

internal sealed class FixedHeight : ILayoutElement
{
    private readonly ILayoutElement _child;
    private readonly int _height;

    public FixedHeight(ILayoutElement child, int height)
    {
        _child = child;
        _height = height;
    }

    public int Measure(int availableWidth) => _height;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        _child.Measure(bounds.Width);
        _child.Arrange(new Rectangle(bounds.X, bounds.Y, bounds.Width, _height), ctx);
    }

    public void Draw(SpriteBatch b) => _child.Draw(b);
}
