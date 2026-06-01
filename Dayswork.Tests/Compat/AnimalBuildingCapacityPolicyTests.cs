using Dayswork.Core.Compat;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Compat;

public sealed class AnimalBuildingCapacityPolicyTests
{
    private readonly AnimalBuildingCapacityPolicy _policy = new();

    [Theory]
    [InlineData(4, 4, 4)]
    [InlineData(12, 12, 12)]
    [InlineData(8, 16, 8)]    // troughs below max -> troughs
    [InlineData(20, 16, 16)]  // troughs above max -> max
    [InlineData(0, 16, 0)]
    [InlineData(-3, 16, 0)]   // negative trough count clamps to 0
    [InlineData(10, -5, 0)]   // negative max clamps to 0
    public void Derives_clamped_capacity(int troughs, int max, int expected)
    {
        Assert.Equal(expected, _policy.DeriveCapacity(new AnimalBuildingCapacityInputs(troughs, max)));
    }

    [Property(MaxTest = 500)]
    public void Capacity_is_within_bounds_and_total(int troughs, int max)
    {
        var result = _policy.DeriveCapacity(new AnimalBuildingCapacityInputs(troughs, max));

        var expectedTroughs = Math.Max(0, troughs);
        var expectedMax = Math.Max(0, max);

        Assert.Equal(Math.Min(expectedTroughs, expectedMax), result);
        Assert.True(result >= 0);
        Assert.True(result <= expectedMax);
    }
}
