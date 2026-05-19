using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Smoke;

public class FrameworkSmokeTests
{
    [Fact]
    public void XUnit_framework_is_wired_up()
    {
        Assert.True(true);
    }

    [Property]
    public bool FsCheck_framework_is_wired_up(int x)
    {
        return x + 0 == x;
    }
}
