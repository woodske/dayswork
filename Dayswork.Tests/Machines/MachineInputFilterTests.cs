namespace Dayswork.Tests.Machines;

using Dayswork.Core.Machines;
using Xunit;

public sealed class MachineInputFilterTests
{
    [Fact]
    public void Any_AllowsEverything()
    {
        Assert.True(MachineInputFilter.Any.IsAny);
        Assert.True(MachineInputFilter.Any.Allows("(O)254"));
        Assert.Empty(MachineInputFilter.Any.AllowedQualifiedIds);
    }

    [Fact]
    public void Specific_PreservesPreferenceOrder_AndDropsBlanksAndDuplicates()
    {
        var filter = MachineInputFilter.Specific(new[] { "(O)304", "", "(O)262", "(O)304", "   " });

        Assert.False(filter.IsAny);
        Assert.Equal(new[] { "(O)304", "(O)262" }, filter.AllowedQualifiedIds);
        Assert.True(filter.Allows("(O)262"));
        Assert.False(filter.Allows("(O)254"));
    }

    [Fact]
    public void Specific_EmptyAfterNormalization_CollapsesToAny()
    {
        var filter = MachineInputFilter.Specific(new[] { "", "  " });

        Assert.True(filter.IsAny);
    }

    [Fact]
    public void Specific_NullCollapsesToAny()
    {
        Assert.True(MachineInputFilter.Specific(null).IsAny);
    }

    [Fact]
    public void Equality_IsStructuralAndOrderSensitive()
    {
        var a = MachineInputFilter.Specific(new[] { "(O)304", "(O)262" });
        var b = MachineInputFilter.Specific(new[] { "(O)304", "(O)262" });
        var reordered = MachineInputFilter.Specific(new[] { "(O)262", "(O)304" });

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, reordered);
        Assert.NotEqual(a, MachineInputFilter.Any);
    }
}
