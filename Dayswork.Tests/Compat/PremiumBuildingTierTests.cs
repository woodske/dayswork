using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Compat;

public sealed class PremiumBuildingTierTests
{
    private readonly SveExpansionProfile _sve = new();

    [Fact]
    public void Sve_maps_premium_coop_to_deluxe_coop()
    {
        Assert.Equal(AnimalBuildingTier.DeluxeCoop,
            _sve.MapPremiumBuildingTier(SveExpansionProfile.PremiumCoopBuildingType));
    }

    [Fact]
    public void Sve_maps_premium_barn_to_deluxe_barn()
    {
        Assert.Equal(AnimalBuildingTier.DeluxeBarn,
            _sve.MapPremiumBuildingTier(SveExpansionProfile.PremiumBarnBuildingType));
    }

    [Theory]
    // Vanilla building types (and SVE non-animal-building ids) have no premium mapping and fall
    // through to the existing vanilla tier inference.
    [InlineData("Coop")]
    [InlineData("Big Coop")]
    [InlineData("Deluxe Coop")]
    [InlineData("Barn")]
    [InlineData("Deluxe Barn")]
    [InlineData("Shed")]
    [InlineData("")]
    public void Sve_does_not_map_non_premium_building_types(string buildingType)
    {
        Assert.Null(_sve.MapPremiumBuildingTier(buildingType));
    }

    [Property(MaxTest = 300)]
    public void Sve_mapping_is_deterministic(string buildingType)
    {
        Assert.Equal(
            _sve.MapPremiumBuildingTier(buildingType),
            _sve.MapPremiumBuildingTier(buildingType));
    }

    [Property(MaxTest = 300)]
    public void Sve_maps_only_the_two_premium_ids(string buildingType)
    {
        var mapped = _sve.MapPremiumBuildingTier(buildingType);

        var isPremium = buildingType == SveExpansionProfile.PremiumCoopBuildingType
                        || buildingType == SveExpansionProfile.PremiumBarnBuildingType;

        Assert.Equal(isPremium, mapped is not null);
    }

    [Property(MaxTest = 300)]
    public void Vanilla_never_maps_a_premium_tier(string buildingType)
    {
        Assert.Null(new VanillaExpansionProfile().MapPremiumBuildingTier(buildingType));
    }
}
