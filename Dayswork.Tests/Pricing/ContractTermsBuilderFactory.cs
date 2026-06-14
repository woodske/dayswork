namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Energy;
using Dayswork.Core.Pricing;

internal static class ContractTermsBuilderFactory
{
    public static ConfigValueResolver CreateResolver() => new();

    public static ContractTermsBuilder CreateTermsBuilder()
    {
        var resolver = CreateResolver();
        return new ContractTermsBuilder(
            new WorkScopeClassifier(),
            new WorkerEnergyProfileBuilder(resolver),
            resolver);
    }
}
