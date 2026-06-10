using Dayswork.Core.Domain;

namespace Dayswork.UI;

internal static class LegacyScopeBootstrapper
{
    public static void HydrateDraft(ContractDraft draft, Contract contract)
    {
        var selection = contract.ScopeSelection;

        draft.OutdoorZones.Clear();
        draft.OutdoorZones.AddRange(selection.OutdoorZones);
        draft.AnimalBuildings.Clear();
        draft.AnimalBuildings.AddRange(selection.AnimalBuildings);
        draft.Greenhouses.Clear();
        draft.Greenhouses.AddRange(selection.Greenhouses);
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
        draft.Greenhouses.Clear();

        var anySupported = false;
        foreach (var outline in selectedBuildings)
        {
            if (TryClassify(outline, out var animalBuilding, out var greenhouse))
            {
                anySupported = true;
                if (animalBuilding is not null && !draft.AnimalBuildings.Contains(animalBuilding))
                    draft.AnimalBuildings.Add(animalBuilding);

                if (greenhouse is not null && !draft.Greenhouses.Contains(greenhouse))
                    draft.Greenhouses.Add(greenhouse);
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

        // Expansion premium buildings (e.g., SVE Premium Coop/Barn) map to their nearest vanilla
        // tier before the vanilla name-substring inference, which would otherwise misclassify them as
        // the cheapest Coop/Barn tier. DisplayName carries the raw building type. Vanilla buildings
        // get no mapping and fall through unchanged.
        if (ModEntry.ExpansionCompat is { } compat &&
            compat.TryResolvePremiumBuildingTier(outline.DisplayName, out var premiumTier))
        {
            animalBuilding = new AnimalBuildingSelection(outline.LocationName, premiumTier);
            return true;
        }

        // Infer the tier from the building type (DisplayName), falling back to the location name, but
        // always key the selection on the UNIQUE outline.LocationName so two same-type buildings stay
        // distinct. TryInferAnimalBuildingSelection embeds whatever name it was given as the
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
}
