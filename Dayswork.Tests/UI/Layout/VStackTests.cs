using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using Xunit;

namespace Dayswork.Tests.UI.Layout;

public class VStackTests
{
    [Fact]
    public void Measure_SumsChildHeightsWithGaps()
    {
        var stack = new VStack(10,
            new StubElement(100, 50),
            new StubElement(100, 50),
            new StubElement(100, 50));

        var height = stack.Measure(200);

        Assert.Equal(170, height);
    }

    [Fact]
    public void Measure_SingleChild_NoGap()
    {
        var stack = new VStack(10,
            new StubElement(100, 80));

        Assert.Equal(80, stack.Measure(200));
    }

    [Fact]
    public void Arrange_PlacesChildrenSequentially()
    {
        var a = new StubElement(100, 40);
        var b = new StubElement(100, 60);
        var c = new StubElement(100, 30);
        var stack = new VStack(10, a, b, c);
        var ctx = new LayoutContext();

        stack.Measure(300);
        stack.Arrange(new Rectangle(10, 20, 300, 500), ctx);

        Assert.Equal(new Rectangle(10, 20, 300, 40), a.ArrangedBounds);
        Assert.Equal(new Rectangle(10, 70, 300, 60), b.ArrangedBounds);
        Assert.Equal(new Rectangle(10, 140, 300, 30), c.ArrangedBounds);
    }

    [Fact]
    public void Arrange_FillSpacerDistributesRemaining()
    {
        var top = new StubElement(100, 50);
        var fill = new StubFill();
        var bottom = new StubElement(100, 50);
        var stack = new VStack(0, top, fill, bottom);
        var ctx = new LayoutContext();

        stack.Measure(200);
        stack.Arrange(new Rectangle(0, 0, 200, 300), ctx);

        Assert.Equal(new Rectangle(0, 0, 200, 50), top.ArrangedBounds);
        Assert.Equal(200, fill.ArrangedBounds.Height);
        Assert.Equal(new Rectangle(0, 250, 200, 50), bottom.ArrangedBounds);
    }

    [Fact]
    public void Arrange_MultipleFillsShareEqually()
    {
        var fill1 = new StubFill();
        var fill2 = new StubFill();
        var stack = new VStack(0, fill1, fill2);
        var ctx = new LayoutContext();

        stack.Measure(200);
        stack.Arrange(new Rectangle(0, 0, 200, 100), ctx);

        Assert.Equal(50, fill1.ArrangedBounds.Height);
        Assert.Equal(50, fill2.ArrangedBounds.Height);
    }

    [Fact]
    public void Measure_FillChildrenExcludedFromMinHeight()
    {
        var stack = new VStack(10,
            new StubElement(100, 50),
            new StubFill(),
            new StubElement(100, 50));

        var height = stack.Measure(200);

        Assert.Equal(120, height);
    }
}
