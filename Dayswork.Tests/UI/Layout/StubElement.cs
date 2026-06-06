using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dayswork.Tests.UI.Layout;

internal sealed class StubElement : ILayoutElement, IHasDesiredWidth
{
    private readonly int _width;
    private readonly int _height;

    public StubElement(int width, int height)
    {
        _width = width;
        _height = height;
    }

    public int DesiredWidth => _width;
    public Rectangle ArrangedBounds { get; private set; }
    public int MeasureCount { get; private set; }
    public int LastMeasuredWidth { get; private set; }

    public int Measure(int availableWidth)
    {
        MeasureCount++;
        LastMeasuredWidth = availableWidth;
        return _height;
    }

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        ArrangedBounds = bounds;
    }

    public void Draw(SpriteBatch b) { }
}

internal sealed class StubFill : ILayoutElement, IFillable
{
    public bool IsFill => true;
    public Rectangle ArrangedBounds { get; private set; }

    public int Measure(int availableWidth) => 0;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        ArrangedBounds = bounds;
    }

    public void Draw(SpriteBatch b) { }
}
