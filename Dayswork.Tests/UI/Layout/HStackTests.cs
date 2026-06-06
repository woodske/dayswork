using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using Xunit;

namespace Dayswork.Tests.UI.Layout;

public class HStackTests
{
    [Fact]
    public void Arrange_AutoChildren_PlacedByDesiredWidth()
    {
        var a = new StubElement(80, 40);
        var b = new StubElement(120, 40);
        var stack = new HStack(10, HStack.Auto(a), HStack.Auto(b));
        var ctx = new LayoutContext();

        stack.Measure(400);
        stack.Arrange(new Rectangle(0, 0, 400, 50), ctx);

        Assert.Equal(new Rectangle(0, 0, 80, 50), a.ArrangedBounds);
        Assert.Equal(new Rectangle(90, 0, 120, 50), b.ArrangedBounds);
    }

    [Fact]
    public void Arrange_FillChild_GetsRemainingWidth()
    {
        var fixedChild = new StubElement(100, 40);
        var fillChild = new StubElement(0, 40);
        var stack = new HStack(10,
            HStack.Auto(fixedChild),
            HStack.Fill(fillChild));
        var ctx = new LayoutContext();

        stack.Measure(400);
        stack.Arrange(new Rectangle(0, 0, 400, 50), ctx);

        Assert.Equal(100, fixedChild.ArrangedBounds.Width);
        Assert.Equal(290, fillChild.ArrangedBounds.Width);
    }

    [Fact]
    public void Arrange_MultipleFills_DistributeByWeight()
    {
        var a = new StubElement(0, 40);
        var b = new StubElement(0, 40);
        var stack = new HStack(0,
            HStack.Fill(a, weight: 1),
            HStack.Fill(b, weight: 2));
        var ctx = new LayoutContext();

        stack.Measure(300);
        stack.Arrange(new Rectangle(0, 0, 300, 50), ctx);

        Assert.Equal(100, a.ArrangedBounds.Width);
        Assert.Equal(200, b.ArrangedBounds.Width);
    }

    [Fact]
    public void Arrange_EqualFills_SplitEvenly()
    {
        var a = new StubElement(0, 40);
        var b = new StubElement(0, 40);
        var stack = new HStack(20,
            HStack.Fill(a),
            HStack.Fill(b));
        var ctx = new LayoutContext();

        stack.Measure(400);
        stack.Arrange(new Rectangle(0, 0, 420, 50), ctx);

        Assert.Equal(200, a.ArrangedBounds.Width);
        Assert.Equal(200, b.ArrangedBounds.Width);
    }

    [Fact]
    public void Arrange_FixedWidth_OverridesDesiredWidth()
    {
        var child = new StubElement(80, 40);
        var stack = new HStack(0, HStack.Fixed(child, 200));
        var ctx = new LayoutContext();

        stack.Measure(400);
        stack.Arrange(new Rectangle(0, 0, 400, 50), ctx);

        Assert.Equal(200, child.ArrangedBounds.Width);
    }

    [Fact]
    public void Arrange_FixedAndFill_FixedUsesExplicitWidth()
    {
        var fixedChild = new StubElement(80, 40);
        var fillChild = new StubElement(0, 40);
        var stack = new HStack(0,
            HStack.Fixed(fixedChild, 150),
            HStack.Fill(fillChild));
        var ctx = new LayoutContext();

        stack.Measure(400);
        stack.Arrange(new Rectangle(0, 0, 400, 50), ctx);

        Assert.Equal(150, fixedChild.ArrangedBounds.Width);
        Assert.Equal(250, fillChild.ArrangedBounds.Width);
    }

    [Fact]
    public void Measure_ReturnsMaxChildHeight()
    {
        var stack = new HStack(10,
            HStack.Auto(new StubElement(80, 30)),
            HStack.Auto(new StubElement(80, 50)),
            HStack.Auto(new StubElement(80, 20)));

        Assert.Equal(50, stack.Measure(400));
    }
}
