namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public sealed class PriceBreakdownBuilder : IPriceBreakdownBuilder
{
    private readonly ConfigValueResolver _resolver;

    public PriceBreakdownBuilder(ConfigValueResolver resolver)
    {
        _resolver = resolver;
    }

    public PricingSnapshot BuildSnapshot(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks,
        IReadOnlyList<OutdoorServiceBand> outdoorBands,
        ContractPriceTotals totals,
        IConfigSnapshot config)
    {
        var lineItems = new Dictionary<(PricingFamily Family, TaskKind Service, OutdoorBandSize? Band, AnimalBuildingTier? Tier), PricingLineItem>();

        foreach (var outdoorBand in outdoorBands)
        {
            var key = (PricingFamily.Outdoor, outdoorBand.Service, outdoorBand.Band, (AnimalBuildingTier?)null);
            var unitPrice = _resolver.ResolveOutdoorServiceBandPrice(
                config,
                new OutdoorPriceKey(outdoorBand.Service, outdoorBand.Band)).Value;
            lineItems[key] = new PricingLineItem(
                Family: PricingFamily.Outdoor,
                Service: outdoorBand.Service,
                Quantity: 1,
                UnitPrice: unitPrice,
                LineTotal: unitPrice,
                OutdoorBand: outdoorBand.Band,
                AnimalTier: null);
        }

        if (scopes.AnimalBuildings.Count > 0)
        {
            foreach (var service in TaskKindSets.AnimalServices.Where(enabledTasks.Contains))
            foreach (var building in scopes.AnimalBuildings)
            {
                var key = (PricingFamily.AnimalBuilding, service, (OutdoorBandSize?)null, (AnimalBuildingTier?)building.Tier);
                var unitPrice = _resolver.ResolveAnimalBuildingPrice(
                    config,
                    new AnimalBuildingPriceKey(service, building.Tier)).Value;

                if (lineItems.TryGetValue(key, out var existingAnimalLine))
                {
                    lineItems[key] = existingAnimalLine with
                    {
                        Quantity = existingAnimalLine.Quantity + 1,
                        LineTotal = existingAnimalLine.LineTotal + unitPrice,
                    };
                }
                else
                {
                    lineItems[key] = new PricingLineItem(
                        Family: PricingFamily.AnimalBuilding,
                        Service: service,
                        Quantity: 1,
                        UnitPrice: unitPrice,
                        LineTotal: unitPrice,
                        OutdoorBand: null,
                        AnimalTier: building.Tier);
                }
            }
        }

        if (scopes.GreenhouseWork is not null)
        {
            foreach (var service in TaskKindSets.GreenhouseServices.Where(enabledTasks.Contains))
            {
                var key = (PricingFamily.Greenhouse, service, (OutdoorBandSize?)null, (AnimalBuildingTier?)null);
                var unitPrice = _resolver.ResolveGreenhouseServicePrice(
                    config,
                    new GreenhousePriceKey(service)).Value;
                lineItems[key] = new PricingLineItem(
                    Family: PricingFamily.Greenhouse,
                    Service: service,
                    Quantity: 1,
                    UnitPrice: unitPrice,
                    LineTotal: unitPrice,
                    OutdoorBand: null,
                    AnimalTier: null);
            }
        }

        var orderedLineItems = lineItems.Values
            .OrderBy(line => FamilyOrder(line.Family))
            .ThenBy(line => (int)line.Service)
            .ThenBy(line => line.OutdoorBand.HasValue ? (int)line.OutdoorBand.Value : int.MaxValue)
            .ThenBy(line => line.AnimalTier.HasValue ? (int)line.AnimalTier.Value : int.MaxValue)
            .ToList();

        return new PricingSnapshot(
            LineItems: orderedLineItems,
            OutdoorSubtotal: totals.OutdoorSubtotal,
            AnimalSubtotal: totals.AnimalSubtotal,
            GreenhouseSubtotal: totals.GreenhouseSubtotal,
            TotalPrice: totals.TotalPrice);
    }

    private static int FamilyOrder(PricingFamily family) => family switch
    {
        PricingFamily.Outdoor => 0,
        PricingFamily.AnimalBuilding => 1,
        PricingFamily.Greenhouse => 2,
        _ => int.MaxValue,
    };
}
