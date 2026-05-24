namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Energy;
using Dayswork.Core.Pricing;

internal static class U18BuilderFactory
{
    public static ConfigValueResolver CreateResolver() => new();

    public static IContractTermsBuilder CreateTermsBuilder()
    {
        var resolver = CreateResolver();
        return new ContractTermsBuilder(
            new WorkScopeClassifier(),
            new OutdoorServiceBandClassifier(resolver),
            new ContractPriceCalculator(resolver),
            new PriceBreakdownBuilder(resolver),
            new WorkerEnergyProfileBuilder(resolver));
    }
}
