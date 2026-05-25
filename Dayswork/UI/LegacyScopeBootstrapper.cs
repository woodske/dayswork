using Dayswork.Core.Domain;

namespace Dayswork.UI;

internal static class LegacyScopeBootstrapper
{
    private static readonly TileCoord CompatibilityPlaceholderTopLeft = new(0, 0);
    private static readonly TileCoord CompatibilityPlaceholderBottomRight = new(999, 999);

    public static void HydrateDraft(ContractDraft draft, Contract contract)
    {
        var selection = contract.ScopeSelection ?? Bootstrap(contract.Zones);

        draft.OutdoorZones.Clear();
        draft.OutdoorZones.AddRange(selection.OutdoorZones);
        draft.AnimalBuildings.Clear();
        draft.AnimalBuildings.AddRange(selection.AnimalBuildings);
        draft.Greenhouse = selection.Greenhouse;
        draft.HydrationMode = contract.ScopeSelection is null
            ? DraftHydrationMode.DerivedFromCompatibilityZones
            : DraftHydrationMode.HydratedFromAuthoritativeScope;
    }

    public static ContractScopeSelection Bootstrap(IReadOnlyList<Zone> compatibilityZones)
    {
        var outdoorZones = compatibilityZones
            .Where(zone => string.Equals(zone.LocationName, "Farm", StringComparison.OrdinalIgnoreCase))
            .OrderBy(DescribeZone, StringComparer.Ordinal)
            .ToList();

        var greenhouseZone = compatibilityZones.FirstOrDefault(zone => IsGreenhouseLocation(zone.LocationName));
        var greenhouse = greenhouseZone is null ? null : new GreenhouseSelection(greenhouseZone.LocationName);

        var animalBuildings = compatibilityZones
            .Where(zone =>
                !string.Equals(zone.LocationName, "Farm", StringComparison.OrdinalIgnoreCase)
                && !IsGreenhouseLocation(zone.LocationName))
            .Select(zone => TryInferAnimalBuildingSelection(zone.LocationName))
            .Where(selection => selection is not null)
            .Cast<AnimalBuildingSelection>()
            .Distinct()
            .OrderBy(selection => selection.LocationName, StringComparer.Ordinal)
            .ThenBy(selection => selection.Tier)
            .ToList();

        return new ContractScopeSelection(
            OutdoorZones: outdoorZones.AsReadOnly(),
            AnimalBuildings: animalBuildings.AsReadOnly(),
            Greenhouse: greenhouse);
    }

    public static IReadOnlyList<Zone> ProjectCompatibilityZones(ContractScopeSelection selection)
    {
        var zones = new List<Zone>();
        zones.AddRange(selection.OutdoorZones);

        zones.AddRange(selection.AnimalBuildings.Select(building =>
            new Zone(
                building.LocationName,
                CompatibilityPlaceholderTopLeft,
                CompatibilityPlaceholderBottomRight)));

        if (selection.Greenhouse is not null)
        {
            zones.Add(new Zone(
                selection.Greenhouse.LocationName,
                CompatibilityPlaceholderTopLeft,
                CompatibilityPlaceholderBottomRight));
        }

        return zones
            .Distinct()
            .OrderBy(DescribeZone, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    public static IReadOnlyList<BuildingOutline> FilterSupportedBuildings(IEnumerable<BuildingOutline> outlines) =>
        outlines
            .Where(IsSupportedWorkScopeBuilding)
            .OrderBy(outline => outline.DisplayName, StringComparer.Ordinal)
            .ThenBy(outline => outline.LocationName, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

    public static bool TryApplySelectedBuildings(
        ContractDraft draft,
        IEnumerable<BuildingOutline> selectedBuildings)
    {
        draft.AnimalBuildings.Clear();
        draft.Greenhouse = null;

        var anySupported = false;
        foreach (var outline in selectedBuildings)
        {
            if (TryClassify(outline, out var animalBuilding, out var greenhouse))
            {
                anySupported = true;
                if (animalBuilding is not null && !draft.AnimalBuildings.Contains(animalBuilding))
                    draft.AnimalBuildings.Add(animalBuilding);

                if (greenhouse is not null)
                    draft.Greenhouse = greenhouse;
            }
        }

        return anySupported;
    }

    public static bool IsSupportedWorkScopeBuilding(BuildingOutline outline) =>
        TryClassify(outline, out _, out _);

    private static bool TryClassify(
        BuildingOutline outline,
        out AnimalBuildingSelection? animalBuilding,
        out GreenhouseSelection? greenhouse)
    {
        greenhouse = null;
        animalBuilding = null;

        if (IsGreenhouseLocation(outline.LocationName) || IsGreenhouseLocation(outline.DisplayName))
        {
            greenhouse = new GreenhouseSelection(outline.LocationName);
            return true;
        }

        animalBuilding = TryInferAnimalBuildingSelection(outline.DisplayName)
                         ?? TryInferAnimalBuildingSelection(outline.LocationName);
        return animalBuilding is not null;
    }

    private static bool IsGreenhouseLocation(string locationName) =>
        locationName.Contains("Greenhouse", StringComparison.OrdinalIgnoreCase);

    private static AnimalBuildingSelection? TryInferAnimalBuildingSelection(string locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
            return null;

        var normalized = locationName.Trim();
        if (normalized.Contains("Coop3", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Deluxe Coop", StringComparison.OrdinalIgnoreCase))
        {
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.DeluxeCoop);
        }

        if (normalized.Contains("Coop2", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Big Coop", StringComparison.OrdinalIgnoreCase))
        {
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.BigCoop);
        }

        if (normalized.Contains("Coop", StringComparison.OrdinalIgnoreCase))
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.Coop);

        if (normalized.Contains("Barn3", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Deluxe Barn", StringComparison.OrdinalIgnoreCase))
        {
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.DeluxeBarn);
        }

        if (normalized.Contains("Barn2", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Big Barn", StringComparison.OrdinalIgnoreCase))
        {
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.BigBarn);
        }

        if (normalized.Contains("Barn", StringComparison.OrdinalIgnoreCase))
            return new AnimalBuildingSelection(locationName, AnimalBuildingTier.Barn);

        return null;
    }

    private static string DescribeZone(Zone zone) =>
        $"{zone.LocationName}|{zone.TopLeft.X}|{zone.TopLeft.Y}|{zone.BottomRight.X}|{zone.BottomRight.Y}";
}
