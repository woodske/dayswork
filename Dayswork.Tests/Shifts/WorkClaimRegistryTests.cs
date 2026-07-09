using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Shifts;

public sealed class WorkClaimRegistryTests
{
    private static readonly ContractId OwnerA = ContractId.New();
    private static readonly ContractId OwnerB = ContractId.New();

    [Fact]
    public void TryClaim_Unclaimed_ClaimsAndReturnsTrue()
    {
        var registry = new WorkClaimRegistry();
        var key = WorkClaimKey.TileTask("Farm", new TileCoord(5, 7), TaskKind.WaterCrops);

        Assert.True(registry.TryClaim(key, OwnerA));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void TryClaim_ByOwner_IsIdempotent()
    {
        var registry = new WorkClaimRegistry();
        var key = WorkClaimKey.TileTask("Farm", new TileCoord(5, 7), TaskKind.WaterCrops);

        Assert.True(registry.TryClaim(key, OwnerA));
        Assert.True(registry.TryClaim(key, OwnerA));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void TryClaim_ByOther_ReturnsFalseAndKeepsOwner()
    {
        var registry = new WorkClaimRegistry();
        var key = WorkClaimKey.TileTask("Farm", new TileCoord(5, 7), TaskKind.WaterCrops);

        Assert.True(registry.TryClaim(key, OwnerA));
        Assert.False(registry.TryClaim(key, OwnerB));
        Assert.True(registry.TryClaim(key, OwnerA));
    }

    [Fact]
    public void IsClaimedByOther_DistinguishesOwners()
    {
        var registry = new WorkClaimRegistry();
        var key = WorkClaimKey.Animal(42L, TaskKind.CollectAnimalProducts);

        Assert.False(registry.IsClaimedByOther(key, OwnerA)); // unclaimed
        registry.TryClaim(key, OwnerA);
        Assert.False(registry.IsClaimedByOther(key, OwnerA)); // own claim
        Assert.True(registry.IsClaimedByOther(key, OwnerB));  // other's claim
    }

    [Fact]
    public void SameTile_DifferentTasks_AreIndependentClaims()
    {
        var registry = new WorkClaimRegistry();
        var tile = new TileCoord(3, 3);

        Assert.True(registry.TryClaim(WorkClaimKey.TileTask("Farm", tile, TaskKind.WaterCrops), OwnerA));
        Assert.True(registry.TryClaim(WorkClaimKey.TileTask("Farm", tile, TaskKind.HarvestCrops), OwnerB));
    }

    [Fact]
    public void SameTile_DifferentDomains_AreIndependentClaims()
    {
        // A generic water contract can still water a managed dirt tile: TileTask and ManagedDirt
        // never collide, and neither collides with a machine parked on the same coordinates.
        var registry = new WorkClaimRegistry();
        var tile = new TileCoord(10, 10);

        Assert.True(registry.TryClaim(WorkClaimKey.TileTask("Farm", tile, TaskKind.WaterCrops), OwnerA));
        Assert.True(registry.TryClaim(WorkClaimKey.ManagedDirt("Farm", tile), OwnerB));
        Assert.True(registry.TryClaim(WorkClaimKey.Machine("Farm", tile), OwnerA));
        Assert.True(registry.TryClaim(WorkClaimKey.FishPond("Farm", tile), OwnerB));
    }

    [Fact]
    public void SameTile_DifferentLocations_AreIndependentClaims()
    {
        var registry = new WorkClaimRegistry();
        var tile = new TileCoord(1, 1);

        Assert.True(registry.TryClaim(WorkClaimKey.TileTask("Farm", tile, TaskKind.ClearWeeds), OwnerA));
        Assert.True(registry.TryClaim(WorkClaimKey.TileTask("Greenhouse", tile, TaskKind.ClearWeeds), OwnerB));
    }

    [Fact]
    public void Animal_ClaimsAreKeyedByAnimalAndTask()
    {
        var registry = new WorkClaimRegistry();

        Assert.True(registry.TryClaim(WorkClaimKey.Animal(1L, TaskKind.PetAnimals), OwnerA));
        // Same animal, different task: independent (petting and milking can split across workers).
        Assert.True(registry.TryClaim(WorkClaimKey.Animal(1L, TaskKind.CollectAnimalProducts), OwnerB));
        // Same animal + task: contested.
        Assert.False(registry.TryClaim(WorkClaimKey.Animal(1L, TaskKind.PetAnimals), OwnerB));
    }
}
