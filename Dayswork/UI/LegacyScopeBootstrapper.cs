using Dayswork.Core.Domain;
using StardewModdingAPI;

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
                if (animalBuilding is not null)
                {
                    // Diagnostic (Bug: selecting normal + premium coop/barn only saves the normal
                    // ones). Captures the interior LocationName, the raw buildingType (DisplayName),
                    // and the resolved tier so we can see whether two buildings collapse because
                    // their interior Name is not unique (Contains uses LocationName + Tier equality).
                    var duplicate = draft.AnimalBuildings.Contains(animalBuilding);
                    ModEntry.ModMonitor.Log(
                        $"[Dayswork][building-select] selected animal building: location='{outline.LocationName}' " +
                        $"type='{outline.DisplayName}' tier={animalBuilding.Tier} " +
                        $"{(duplicate ? "DROPPED (duplicate LocationName+Tier already in draft)" : "added")}.",
                        LogLevel.Info);

                    if (!duplicate)
                        draft.AnimalBuildings.Add(animalBuilding);
                }
                else if (greenhouse is not null)
                {
                    ModEntry.ModMonitor.Log(
                        $"[Dayswork][building-select] selected greenhouse: location='{outline.LocationName}' type='{outline.DisplayName}'.",
                        LogLevel.Info);
                }

                if (greenhouse is not null)
                    draft.Greenhouse = greenhouse;
            }
            else
            {
                ModEntry.ModMonitor.Log(
                    $"[Dayswork][building-select] selected building NOT classified as work scope: " +
                    $"location='{outline.LocationName}' type='{outline.DisplayName}'.",
                    LogLevel.Info);
            }
        }

        ModEntry.ModMonitor.Log(
            $"[Dayswork][building-select] draft now has {draft.AnimalBuildings.Count} animal building(s): " +
            $"[{string.Join("; ", draft.AnimalBuildings.Select(b => $"{b.LocationName}:{b.Tier}"))}].",
            LogLevel.Info);

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

        // Expansion premium buildings (e.g., SVE Premium Coop/Barn) map to their nearest vanilla
        // tier before the vanilla name-substring inference, which would otherwise misclassify them as
        // the cheapest Coop/Barn tier. DisplayName carries the raw building type. Vanilla buildings
        // get no mapping and fall through unchanged. (U-SVE-03 / BR-SVE3-06)
        if (ModEntry.ExpansionCompat is { } compat &&
            compat.TryResolvePremiumBuildingTier(outline.DisplayName, out var premiumTier))
        {
            animalBuilding = new AnimalBuildingSelection(outline.LocationName, premiumTier);
            return true;
        }

        // Infer the tier from the building type (DisplayName), falling back to the location name, but
        // always key the selection on the UNIQUE outline.LocationName so two same-type buildings stay
        // distinct (TODO-08). TryInferAnimalBuildingSelection embeds whatever name it was given as the
        // selection's LocationName, so take only its Tier and rebuild with the unique key.
        var inferred = TryInferAnimalBuildingSelection(outline.DisplayName)
                       ?? TryInferAnimalBuildingSelection(outline.LocationName);
        animalBuilding = inferred is null
            ? null
            : new AnimalBuildingSelection(outline.LocationName, inferred.Tier);
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
