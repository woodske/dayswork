namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public sealed class ContractPriceCalculator : IContractPriceCalculator
{
    private readonly ConfigValueResolver _resolver;

    public ContractPriceCalculator(ConfigValueResolver resolver)
    {
        _resolver = resolver;
    }

    public ContractPriceTotals Calculate(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks,
        IReadOnlyList<OutdoorServiceBand> outdoorBands,
        IConfigSnapshot config)
    {
        var outdoorSubtotal = outdoorBands.Sum(band =>
            _resolver.ResolveOutdoorServiceBandPrice(config, new OutdoorPriceKey(band.Service, band.Band)).Value);

        var animalSubtotal = 0;
        if (scopes.AnimalBuildings.Count > 0)
        {
            foreach (var service in TaskKindSets.AnimalServices.Where(enabledTasks.Contains))
            foreach (var building in scopes.AnimalBuildings)
            {
                animalSubtotal += _resolver.ResolveAnimalBuildingPrice(
                    config,
                    new AnimalBuildingPriceKey(service, building.Tier)).Value;
            }
        }

        var greenhouseSubtotal = 0;
        if (scopes.GreenhouseWork is not null)
        {
            foreach (var service in TaskKindSets.GreenhouseServices.Where(enabledTasks.Contains))
            {
                greenhouseSubtotal += _resolver.ResolveGreenhouseServicePrice(
                    config,
                    new GreenhousePriceKey(service)).Value;
            }
        }

        return new ContractPriceTotals(
            OutdoorSubtotal: outdoorSubtotal,
            AnimalSubtotal: animalSubtotal,
            GreenhouseSubtotal: greenhouseSubtotal,
            TotalPrice: outdoorSubtotal + animalSubtotal + greenhouseSubtotal);
    }
}
