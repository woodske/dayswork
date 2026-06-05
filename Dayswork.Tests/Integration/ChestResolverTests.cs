using Dayswork.Integration;
using Xunit;

namespace Dayswork.Tests.Integration;

public sealed class ChestResolverTests
{
    [Fact]
    public void ShouldExcludeSelectableFarmChest_excludes_only_input_chest_tile()
    {
        Assert.True(ChestResolver.ShouldExcludeSelectableFarmChest(11, 22, 11, 22));
        Assert.False(ChestResolver.ShouldExcludeSelectableFarmChest(11, 22, 13, 22));
        Assert.False(ChestResolver.ShouldExcludeSelectableFarmChest(11, 22, 11, 24));
    }

    [Fact]
    public void ShouldExcludeSelectableFarmChest_keeps_all_chests_when_no_input_tile_exists()
    {
        Assert.False(ChestResolver.ShouldExcludeSelectableFarmChest(null, null, 11, 22));
    }
}
