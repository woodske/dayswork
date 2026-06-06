using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dayswork.UI.Layout;

internal sealed class HStack : ILayoutElement
{
    private readonly Column[] _columns;
    private readonly int _gap;

    public HStack(int gap, params Column[] columns)
    {
        _gap = gap;
        _columns = columns;
    }

    public static Column Auto(ILayoutElement element) => new(element);
    public static Column Fixed(ILayoutElement element, int width) => new(element, ExplicitWidth: width);
    public static Column Fill(ILayoutElement element, int weight = 1) => new(element, FillWeight: weight);

    public int Measure(int availableWidth)
    {
        var maxH = 0;
        foreach (var col in _columns)
        {
            var h = col.Element.Measure(availableWidth);
            if (h > maxH)
                maxH = h;
        }

        return maxH;
    }

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        var widths = new int[_columns.Length];
        var totalFixed = 0;
        var totalWeight = 0;

        for (var i = 0; i < _columns.Length; i++)
        {
            if (_columns[i].IsFill)
            {
                totalWeight += _columns[i].FillWeight;
            }
            else if (_columns[i].ExplicitWidth > 0)
            {
                widths[i] = _columns[i].ExplicitWidth;
                totalFixed += widths[i];
            }
            else
            {
                widths[i] = _columns[i].Element is IHasDesiredWidth hw ? hw.DesiredWidth : 0;
                totalFixed += widths[i];
            }
        }

        var gapTotal = Math.Max(0, _columns.Length - 1) * _gap;
        var remaining = Math.Max(0, bounds.Width - totalFixed - gapTotal);

        if (totalWeight > 0)
        {
            for (var i = 0; i < _columns.Length; i++)
            {
                if (_columns[i].IsFill)
                    widths[i] = remaining * _columns[i].FillWeight / totalWeight;
            }
        }

        var x = bounds.X;
        for (var i = 0; i < _columns.Length; i++)
        {
            _columns[i].Element.Arrange(new Rectangle(x, bounds.Y, widths[i], bounds.Height), ctx);
            x += widths[i] + _gap;
        }
    }

    public void Draw(SpriteBatch b)
    {
        foreach (var col in _columns)
            col.Element.Draw(b);
    }

    internal sealed record Column(ILayoutElement Element, int FillWeight = 0, int ExplicitWidth = 0)
    {
        public bool IsFill => FillWeight > 0;
    }
}
