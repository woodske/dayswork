using Dayswork.UI;
using Xunit;

namespace Dayswork.Tests.UI;

public sealed class ContractMenuViewportTests
{
    [Fact]
    public void GetVisibleCount_DropsRowsWhenViewportIsCompressed()
    {
        var visibleCount = ContractMenuViewport.GetVisibleCount(252, 60, 12);

        Assert.Equal(3, visibleCount);
    }

    [Fact]
    public void GetVisibleCount_DoesNotCountTrailingGap()
    {
        var visibleCount = ContractMenuViewport.GetVisibleCount(376, 88, 8);

        Assert.Equal(4, visibleCount);
    }

    [Fact]
    public void GetVisibleVariableCount_ReturnsAtLeastOneRowWhenFirstRowIsTall()
    {
        var visibleCount = ContractMenuViewport.GetVisibleVariableCount(new[] { 120, 90, 90 }, 0, 100);

        Assert.Equal(1, visibleCount);
    }

    [Fact]
    public void GetVisibleVariableCount_UsesStartIndexWindow()
    {
        var visibleCount = ContractMenuViewport.GetVisibleVariableCount(new[] { 90, 90, 90 }, 1, 180);

        Assert.Equal(2, visibleCount);
    }
}
