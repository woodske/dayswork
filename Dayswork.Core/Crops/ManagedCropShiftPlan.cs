namespace Dayswork.Core.Crops;

public sealed record ManagedCropShiftPlan
{
    public IReadOnlyList<TileAction> SupplyIndependentActions { get; }
    public IReadOnlyList<TileAction> SupplyDependentActions { get; }
    public IReadOnlyList<PurchaseLine> Purchases { get; }

    public ManagedCropShiftPlan(
        IReadOnlyList<TileAction>? supplyIndependentActions,
        IReadOnlyList<TileAction>? supplyDependentActions,
        IReadOnlyList<PurchaseLine>? purchases)
    {
        SupplyIndependentActions = OrderActions(supplyIndependentActions);
        SupplyDependentActions = OrderActions(supplyDependentActions);
        Purchases = (purchases ?? Array.Empty<PurchaseLine>())
            .Where(line => line.Quantity > 0)
            .OrderBy(line => line.Store)
            .ThenBy(line => line.ItemId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<TileAction> AllActions =>
        SupplyIndependentActions.Concat(SupplyDependentActions).ToList().AsReadOnly();

    private static IReadOnlyList<TileAction> OrderActions(IReadOnlyList<TileAction>? actions) =>
        (actions ?? Array.Empty<TileAction>())
            .OrderBy(action => action.LocationName, StringComparer.Ordinal)
            .ThenBy(action => action.Tile.Y)
            .ThenBy(action => action.Tile.X)
            .ThenBy(action => ActionRank(action.Kind))
            .ThenBy(action => action.ItemId, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

    public static int ActionRank(ManagedCropActionKind kind) =>
        kind switch
        {
            ManagedCropActionKind.Harvest => 0,
            ManagedCropActionKind.ClearDebris => 1,
            ManagedCropActionKind.Till => 2,
            ManagedCropActionKind.Fertilize => 3,
            ManagedCropActionKind.PlantSeed => 4,
            ManagedCropActionKind.Water => 5,
            _ => 99,
        };
}
