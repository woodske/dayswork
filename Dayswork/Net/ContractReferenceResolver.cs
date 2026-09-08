using Dayswork.Core.Domain;
using Dayswork.Integration;
using Dayswork.Orchestration;
using StardewValley;

namespace Dayswork.Net;

/// <summary>
/// Re-resolves every world reference a contract carries against the host's own world. A client
/// authored its draft against what its game had synced, which may be behind (or, for locations
/// outside the farm, absent), so the host checks the references itself before committing — the
/// difference between "the client saw a chest" and "there is a chest" is exactly what the
/// ChestMissing / MachineMissing / PondMissing rejections report.
/// </summary>
internal sealed class ContractReferenceResolver
{
    private readonly ChestResolver _chestResolver;
    private readonly MachineReader _machineReader = new();
    private readonly FishPondReader _fishPondReader = new();

    public ContractReferenceResolver(ChestResolver chestResolver) => _chestResolver = chestResolver;

    public sealed record Result(
        IReadOnlyCollection<string> MissingChests,
        IReadOnlyCollection<string> MissingMachines,
        IReadOnlyCollection<string> MissingPonds);

    public Result Resolve(Contract contract)
    {
        var missingChests = new List<string>();
        var missingMachines = new List<string>();
        var missingPonds = new List<string>();

        foreach (var chestRef in EnumerateChestRefs(contract))
        {
            if (_chestResolver.ResolveChest(chestRef) is null)
                missingChests.Add(Describe(chestRef.LocationName, chestRef.Tile));
        }

        foreach (var machine in contract.MachineScope.AllMachines)
        {
            var location = Game1.getLocationFromName(machine.LocationName);
            if (location is null || _machineReader.Resolve(location, machine.Tile, machine.ExpectedQualifiedId) is null)
                missingMachines.Add(Describe(machine.LocationName, machine.Tile));
        }

        foreach (var pond in contract.FishPondScope.Ponds)
        {
            var location = Game1.getLocationFromName(pond.LocationName);
            if (location is null || _fishPondReader.Resolve(location, pond.Tile) is null)
                missingPonds.Add(Describe(pond.LocationName, pond.Tile));
        }

        return new Result(missingChests, missingMachines, missingPonds);
    }

    /// <summary>Every chest the contract points at: task destinations, managed-crop group outputs,
    /// machine-group input chests and outputs, and the fish-pond output.</summary>
    private static IEnumerable<ChestRef> EnumerateChestRefs(Contract contract)
    {
        foreach (var destination in contract.TaskDestinations.Values)
        {
            if (destination is ChestDestination chest)
                yield return chest.Ref;
        }

        foreach (var assignment in contract.CropPlan.Assignments)
        {
            if (assignment.OutputDestination is ChestDestination chest)
                yield return chest.Ref;
        }

        foreach (var group in contract.MachineScope.Groups)
        {
            if (group.InputChest is { } inputChest)
                yield return inputChest;
            if (group.OutputDestination is ChestDestination chest)
                yield return chest.Ref;
        }

        if (contract.FishPondScope.OutputDestination is ChestDestination pondChest)
            yield return pondChest.Ref;
    }

    private static string Describe(string locationName, TileCoord tile) =>
        $"{locationName}({tile.X},{tile.Y})";
}
