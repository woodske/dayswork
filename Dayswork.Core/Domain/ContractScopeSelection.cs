namespace Dayswork.Core.Domain;

public sealed record ContractScopeSelection
{
    public IReadOnlyList<Zone> OutdoorZones { get; }
    public IReadOnlyList<AnimalBuildingSelection> AnimalBuildings { get; }

    /// <summary>
    /// All greenhouse work locations selected for this contract. A farm may expose more than one
    /// (e.g. the vanilla Greenhouse plus SVE's Grandpa's Shed greenhouse), so this is a list rather
    /// than a single value. Order is the selection order; callers that need a stable order
    /// sort by <see cref="GreenhouseSelection.LocationName"/>.
    /// </summary>
    public IReadOnlyList<GreenhouseSelection> Greenhouses { get; }

    /// <summary>Back-compat accessor: the first selected greenhouse, or null when none are selected.</summary>
    public GreenhouseSelection? Greenhouse => Greenhouses.Count > 0 ? Greenhouses[0] : null;

    public ContractScopeSelection(
        IReadOnlyList<Zone> OutdoorZones,
        IReadOnlyList<AnimalBuildingSelection> AnimalBuildings,
        IReadOnlyList<GreenhouseSelection> Greenhouses)
    {
        this.OutdoorZones = OutdoorZones;
        this.AnimalBuildings = AnimalBuildings;
        this.Greenhouses = Greenhouses;
    }

    // Back-compat single-greenhouse constructor so existing call sites and persistence (which carried
    // exactly one greenhouse) keep working unchanged.
    public ContractScopeSelection(
        IReadOnlyList<Zone> OutdoorZones,
        IReadOnlyList<AnimalBuildingSelection> AnimalBuildings,
        GreenhouseSelection? Greenhouse)
        : this(
            OutdoorZones,
            AnimalBuildings,
            Greenhouse is null
                ? Array.Empty<GreenhouseSelection>()
                : new[] { Greenhouse })
    {
    }
}
