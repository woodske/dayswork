namespace Dayswork.Tests.ManageCrops;

using Dayswork.Core.Crops;
using Dayswork.Tests.Generators;
using Dayswork.Tests.Persistence;
using FsCheck;
using FsCheck.Xunit;

public sealed class ManageCropsPropertyTests
{
    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool CropPlanSerialization_RoundTrip_IsIdentity(CropPlan cropPlan)
    {
        var dto = CropPlanSerialization.MapDomainToDto(cropPlan);
        var hydrated = CropPlanSerialization.MapDtoToDomain(dto);

        return ContractStructuralComparer.CropPlansEqual(cropPlan, hydrated);
    }

    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool PlantingViability_IsDeterministic(ViabilityCase input)
    {
        var calculator = new PlantingViabilityCalculator();

        var first = calculator.IsPlantingViable(input.FieldState, input.Crop, input.Crop.RequiresFertilizer);
        var second = calculator.IsPlantingViable(input.FieldState, input.Crop, input.Crop.RequiresFertilizer);

        return first == second;
    }

    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool PlantingViability_SeasonAgnosticLocation_IsAlwaysViable(CropDescriptor crop)
    {
        var calculator = new PlantingViabilityCalculator();
        var field = new FieldState("Greenhouse", new Dayswork.Core.Domain.GameDate(28, Dayswork.Core.Domain.Season.Winter, 1), true, Array.Empty<TileState>());

        return calculator.IsPlantingViable(field, crop, crop.RequiresFertilizer);
    }

    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool CropSupplyPlanner_FertilizerRequiredCompletion_IsAtomic(SupplyCase input)
    {
        var planner = new CropSupplyPlanner();
        var completed = planner.CompletableTiles(input.Crop, input.ViableTiles, input.Inventory);

        if (!input.Crop.RequiresFertilizer)
            return completed <= input.Inventory.QuantityOf(input.Crop.SeedItemId);

        return completed <= input.Inventory.QuantityOf(input.Crop.SeedItemId)
            && completed <= input.Inventory.QuantityOf(input.Crop.FertilizerItemId!);
    }

    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool SeasonAssignmentResolver_ApplyingSameChoiceTwice_IsIdempotent(CropZoneAssignment assignment, SeasonCropChoice choice)
    {
        var resolver = new SeasonAssignmentResolver();
        var once = resolver.ApplyChoice(assignment, choice);
        var twice = resolver.ApplyChoice(once, choice);

        return once.Choices.SequenceEqual(twice.Choices);
    }

    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool StoreResolver_IsDeterministic(StoreCase input)
    {
        var resolver = new StoreResolver();

        var first = resolver.Resolve(input.Preference, input.ItemId, input.IsFestival, input.Stock);
        var second = resolver.Resolve(input.Preference, input.ItemId, input.IsFestival, input.Stock);

        return first == second;
    }

    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public bool StoreResolver_FestivalDay_AlwaysResolvesNoStore(StoreCase input)
    {
        var resolver = new StoreResolver();

        var result = resolver.Resolve(input.Preference, input.ItemId, true, input.Stock);

        return result.Store is null && result.ClosedReason == StoreClosedReason.Festival
            || input.Preference == StorePreference.InputChestOnly && result.ClosedReason == StoreClosedReason.ChestSupplyOnly;
    }

    [Property(Arbitrary = new[] { typeof(ManageCropsGen) }, MaxTest = 200)]
    public Property CropShiftPlanner_TileActionsRespectDependencyOrder(ShiftPlanCase input)
    {
        var planner = new CropShiftPlanner();
        var plan = planner.Plan(input.Assignment, input.FieldState, input.Inventory, input.Stock, false);

        var ordered = plan.AllActions
            .GroupBy(action => $"{action.LocationName}:{action.Tile.X}:{action.Tile.Y}")
            .All(group =>
            {
                var ranks = group.Select(action => ManagedCropShiftPlan.ActionRank(action.Kind)).ToList();
                return ranks.SequenceEqual(ranks.OrderBy(rank => rank));
            });

        return ordered.ToProperty()
            .Label(string.Join("|", plan.AllActions.Select(action => $"{action.Tile.X},{action.Tile.Y}:{action.Kind}")));
    }
}
