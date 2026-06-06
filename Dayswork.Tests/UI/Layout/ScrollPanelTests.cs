using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using Xunit;

namespace Dayswork.Tests.UI.Layout;

public sealed class ScrollPanelTests
{
    [Fact]
    public void Arrange_MeasuresVisibleItemsBeforeArranging()
    {
        var first = new StubElement(100, 20);
        var second = new StubElement(100, 20);
        var third = new StubElement(100, 20);
        var panel = new ScrollPanel(
            () => new ILayoutElement[] { first, second, third },
            itemHeight: 40,
            gap: 0);

        panel.Arrange(new Rectangle(0, 0, 300, 80), new LayoutContext());

        Assert.Equal(1, first.MeasureCount);
        Assert.Equal(248, first.LastMeasuredWidth);
        Assert.Equal(1, second.MeasureCount);
        Assert.Equal(248, second.LastMeasuredWidth);
        Assert.Equal(0, third.MeasureCount);
    }

    [Fact]
    public void Arrange_UsesFullWidthWhenAllItemsFit()
    {
        var first = new StubElement(100, 20);
        var second = new StubElement(100, 20);
        var panel = new ScrollPanel(
            () => new ILayoutElement[] { first, second },
            itemHeight: 40,
            gap: 0);

        panel.Arrange(new Rectangle(0, 0, 300, 80), new LayoutContext());

        Assert.Equal(300, first.LastMeasuredWidth);
        Assert.Equal(300, second.LastMeasuredWidth);
    }

    [Fact]
    public void Arrange_CountsRowsWithoutTrailingGap()
    {
        var rows = Enumerable.Range(0, 4)
            .Select(_ => new StubElement(100, 20))
            .ToArray();
        var panel = new ScrollPanel(
            () => rows,
            itemHeight: 88,
            gap: 8);

        panel.Arrange(new Rectangle(0, 0, 300, 376), new LayoutContext());

        Assert.All(rows, row => Assert.Equal(300, row.LastMeasuredWidth));
    }
}
