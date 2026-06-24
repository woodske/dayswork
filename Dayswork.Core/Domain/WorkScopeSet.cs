namespace Dayswork.Core.Domain;

using Dayswork.Core.Crops;
using Dayswork.Core.FishPonds;
using Dayswork.Core.Machines;

public sealed record WorkScopeSet
{
    public OutdoorWorkScope? OutdoorWork { get; }
    public IReadOnlyList<AnimalBuildingScope> AnimalBuildings { get; }
    public ManagedCropWorkScope? ManagedCrops { get; }

    /// <summary>The authored machine work, or null when no machine group is enabled.</summary>
    public MachineWorkScope? Machines { get; }

    /// <summary>The authored fish-pond work, or null when no pond is selected.</summary>
    public FishPondWorkScope? FishPonds { get; }

    /// <summary>All greenhouse work scopes (one per selected greenhouse).</summary>
    public IReadOnlyList<GreenhouseWorkScope> GreenhouseWorks { get; }

    /// <summary>Back-compat accessor: the first greenhouse work scope, or null when none.</summary>
    public GreenhouseWorkScope? GreenhouseWork => GreenhouseWorks.Count > 0 ? GreenhouseWorks[0] : null;

    public WorkScopeSet(
        OutdoorWorkScope? OutdoorWork,
        IReadOnlyList<AnimalBuildingScope> AnimalBuildings,
        IReadOnlyList<GreenhouseWorkScope> GreenhouseWorks,
        ManagedCropWorkScope? ManagedCrops = null,
        MachineWorkScope? Machines = null,
        FishPondWorkScope? FishPonds = null)
    {
        this.OutdoorWork = OutdoorWork;
        this.AnimalBuildings = AnimalBuildings;
        this.GreenhouseWorks = GreenhouseWorks;
        this.ManagedCrops = ManagedCrops;
        this.Machines = Machines;
        this.FishPonds = FishPonds;
    }

    /// <summary>
    /// Convenience factory for the common single-greenhouse (or no-greenhouse) case, used by tests
    /// and any caller that has at most one greenhouse scope. Production multi-greenhouse code uses the
    /// list constructor directly.
    /// </summary>
    public static WorkScopeSet WithSingleGreenhouse(
        OutdoorWorkScope? outdoorWork,
        IReadOnlyList<AnimalBuildingScope> animalBuildings,
        GreenhouseWorkScope? greenhouseWork,
        ManagedCropWorkScope? managedCrops = null,
        MachineWorkScope? machines = null,
        FishPondWorkScope? fishPonds = null) =>
        new(
            outdoorWork,
            animalBuildings,
            greenhouseWork is null
                ? Array.Empty<GreenhouseWorkScope>()
                : new[] { greenhouseWork },
            managedCrops,
            machines,
            fishPonds);
}
