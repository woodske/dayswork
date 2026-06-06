using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dayswork.UI.Layout;

internal sealed class VStack : ILayoutElement
{
    private readonly ILayoutElement[] _children;
    private readonly int _gap;

    public VStack(int gap, params ILayoutElement[] children)
    {
        _gap = gap;
        _children = children;
    }

    public int Measure(int availableWidth)
    {
        var total = 0;
        foreach (var child in _children)
        {
            if (child is IFillable { IsFill: true })
                continue;

            total += child.Measure(availableWidth);
        }

        return total + Math.Max(0, _children.Length - 1) * _gap;
    }

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        var measured = new int[_children.Length];
        var fillCount = 0;
        var fixedTotal = 0;

        for (var i = 0; i < _children.Length; i++)
        {
            if (_children[i] is IFillable { IsFill: true })
            {
                fillCount++;
            }
            else
            {
                measured[i] = _children[i].Measure(bounds.Width);
                fixedTotal += measured[i];
            }
        }

        var gapTotal = Math.Max(0, _children.Length - 1) * _gap;
        var remaining = Math.Max(0, bounds.Height - fixedTotal - gapTotal);
        var fillHeight = fillCount > 0 ? remaining / fillCount : 0;

        var y = bounds.Y;
        for (var i = 0; i < _children.Length; i++)
        {
            var h = _children[i] is IFillable { IsFill: true } ? fillHeight : measured[i];
            _children[i].Arrange(new Rectangle(bounds.X, y, bounds.Width, h), ctx);
            y += h + _gap;
        }
    }

    public void Draw(SpriteBatch b)
    {
        foreach (var child in _children)
            child.Draw(b);
    }
}
