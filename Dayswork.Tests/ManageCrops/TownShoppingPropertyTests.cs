namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Tests.Generators;
using FsCheck.Xunit;

public sealed class TownShoppingPropertyTests
{
    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool Affordable_plan_never_exceeds_wallet(PurchaseClampCase input)
    {
        var manifest = FertilizedManifest(input.Quantity, input.SeedCost, input.FertilizerCost);
        var plan = new PurchaseAffordabilityCalculator().ClampToWallet(manifest, input.WalletGold);

        return plan.TotalCost <= Math.Max(0, input.WalletGold);
    }

    // When the wallet covers the whole manifest, every line is bought in full with no shortfall —
    // a budget cut never silently drops part of an affordable order (the under-buying regression).
    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool Affordable_plan_buys_everything_when_wallet_covers_cost(PurchaseClampCase input)
    {
        var manifest = FertilizedManifest(input.Quantity, input.SeedCost, input.FertilizerCost);
        var plan = new PurchaseAffordabilityCalculator().ClampToWallet(manifest, input.WalletGold);

        if (input.WalletGold < manifest.TotalCost)
            return true; // shortfall branch covered by the wallet-bound property

        var lines = plan.Groups.SelectMany(group => group.Lines).ToList();
        var seeds = lines.Where(line => line.ItemId == "seed.parsnip").Sum(line => line.Quantity);
        var fertilizer = lines.Where(line => line.ItemId == "fert.basic").Sum(line => line.Quantity);
        return !plan.Shortfall && seeds == input.Quantity && fertilizer == input.Quantity;
    }

    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool Planned_tile_count_is_deterministic(ShiftPlanCase input)
    {
        var choice = input.Assignment.Choices.First();
        var first = PlannedPlantableTileCounter.CountPlantable(input.FieldState, input.Assignment, choice, isViable: true);
        var second = PlannedPlantableTileCounter.CountPlantable(input.FieldState, input.Assignment, choice, isViable: true);

        return first == second;
    }

    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool Store_hours_policy_is_total(StoreHoursCase input)
    {
        _ = StoreHoursPolicy.OpensAt(input.Store, input.DayOfMonth);
        _ = StoreHoursPolicy.IsOpen(input.Store, input.TimeOfDay, input.DayOfMonth);
        return true;
    }

    private static ShiftPurchaseManifest FertilizedManifest(int quantity, int seedCost, int fertilizerCost) =>
        new(
            new[]
            {
                new StorePurchaseGroup(
                    Store.Pierre,
                    new[]
                    {
                        new ManifestLine("fert.basic", quantity, fertilizerCost, IsFertilizer: true),
                        new ManifestLine("seed.parsnip", quantity, seedCost, IsFertilizer: false),
                    }),
            },
            Array.Empty<string>());
}
