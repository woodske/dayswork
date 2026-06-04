namespace Dayswork.Core.Crops;

public sealed class PlantingViabilityCalculator
{
    private const int LastSeasonDay = 28;

    public bool IsPlantingViable(FieldState fieldState, CropDescriptor crop, bool useFertilizer)
    {
        if (fieldState.IsSeasonAgnosticLocation)
            return true;

        var daysToFirstHarvest = crop.EffectiveDaysToFirstHarvest(useFertilizer);
        return fieldState.Date.Day + daysToFirstHarvest <= LastSeasonDay;
    }
}
