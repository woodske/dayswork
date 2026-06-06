using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dayswork.UI.Layout;

internal sealed class Padding : ILayoutElement
{
    private readonly ILayoutElement _child;
    private readonly int _top, _right, _bottom, _left;

    public Padding(ILayoutElement child, int top = 0, int right = 0, int bottom = 0, int left = 0)
    {
        _child = child;
        _top = top;
        _right = right;
        _bottom = bottom;
        _left = left;
    }

    public int Measure(int availableWidth) =>
        _top + _child.Measure(availableWidth - _left - _right) + _bottom;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        var inner = new Rectangle(
            bounds.X + _left,
            bounds.Y + _top,
            bounds.Width - _left - _right,
            bounds.Height - _top - _bottom);
        _child.Arrange(inner, ctx);
    }

    public void Draw(SpriteBatch b) => _child.Draw(b);
}
