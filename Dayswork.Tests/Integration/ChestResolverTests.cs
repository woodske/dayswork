using Dayswork.Integration;
using Xunit;

namespace Dayswork.Tests.Integration;

public sealed class ChestResolverTests
{
    [Fact]
    public void ShouldExcludeSelectableFarmChest_excludes_builtin_office_chest_tiles()
    {
        Assert.True(ChestResolver.ShouldExcludeSelectableFarmChest(11, 22, 33, 44, 11, 22));
        Assert.True(ChestResolver.ShouldExcludeSelectableFarmChest(11, 22, 33, 44, 33, 44));
        Assert.False(ChestResolver.ShouldExcludeSelectableFarmChest(11, 22, 33, 44, 13, 22));
        Assert.False(ChestResolver.ShouldExcludeSelectableFarmChest(11, 22, 33, 44, 11, 24));
    }

    [Fact]
    public void ShouldExcludeSelectableFarmChest_keeps_all_chests_when_no_builtin_tiles_exist()
    {
        Assert.False(ChestResolver.ShouldExcludeSelectableFarmChest(null, null, null, null, 11, 22));
    }
}
