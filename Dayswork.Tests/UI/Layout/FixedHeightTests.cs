using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using Xunit;

namespace Dayswork.Tests.UI.Layout;

public sealed class FixedHeightTests
{
    [Fact]
    public void Arrange_MeasuresChildBeforeArranging()
    {
        var child = new StubElement(100, 20);
        var fixedHeight = new FixedHeight(child, 58);

        fixedHeight.Arrange(new Rectangle(10, 20, 300, 100), new LayoutContext());

        Assert.Equal(1, child.MeasureCount);
        Assert.Equal(300, child.LastMeasuredWidth);
        Assert.Equal(new Rectangle(10, 20, 300, 58), child.ArrangedBounds);
    }
}
