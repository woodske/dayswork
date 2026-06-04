namespace Dayswork.Core.Domain;

using Dayswork.Core.Crops;

public sealed record WorkScopeSet
{
    public OutdoorWorkScope? OutdoorWork { get; }
    public IReadOnlyList<AnimalBuildingScope> AnimalBuildings { get; }
    public ManagedCropWorkScope? ManagedCrops { get; }

    /// <summary>All greenhouse work scopes (one per selected greenhouse, TODO-10).</summary>
    public IReadOnlyList<GreenhouseWorkScope> GreenhouseWorks { get; }

    /// <summary>Back-compat accessor: the first greenhouse work scope, or null when none.</summary>
    public GreenhouseWorkScope? GreenhouseWork => GreenhouseWorks.Count > 0 ? GreenhouseWorks[0] : null;

    public WorkScopeSet(
        OutdoorWorkScope? OutdoorWork,
        IReadOnlyList<AnimalBuildingScope> AnimalBuildings,
        IReadOnlyList<GreenhouseWorkScope> GreenhouseWorks,
        ManagedCropWorkScope? ManagedCrops = null)
    {
        this.OutdoorWork = OutdoorWork;
        this.AnimalBuildings = AnimalBuildings;
        this.GreenhouseWorks = GreenhouseWorks;
        this.ManagedCrops = ManagedCrops;
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
        ManagedCropWorkScope? managedCrops = null) =>
        new(
            outdoorWork,
            animalBuildings,
            greenhouseWork is null
                ? Array.Empty<GreenhouseWorkScope>()
                : new[] { greenhouseWork },
            managedCrops);
}
