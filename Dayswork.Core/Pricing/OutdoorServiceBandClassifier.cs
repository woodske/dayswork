namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public sealed class OutdoorServiceBandClassifier : IOutdoorServiceBandClassifier
{
    private readonly ConfigValueResolver _resolver;

    public OutdoorServiceBandClassifier(ConfigValueResolver resolver)
    {
        _resolver = resolver;
    }

    public IReadOnlyList<OutdoorServiceBand> ClassifyBands(
        WorkScopeSet scopes,
        IReadOnlySet<TaskKind> enabledTasks,
        IConfigSnapshot config)
    {
        if (scopes.OutdoorWork is null)
            return Array.Empty<OutdoorServiceBand>();

        var band = ClassifyBand(scopes.OutdoorWork.TotalTileCount, config);
        return TaskKindSets.OutdoorServices
            .Where(enabledTasks.Contains)
            .Select(service => new OutdoorServiceBand(service, band, scopes.OutdoorWork.TotalTileCount))
            .ToList();
    }

    private OutdoorBandSize ClassifyBand(int totalTileCount, IConfigSnapshot config)
    {
        var smallThreshold = _resolver.ResolveOutdoorBandThreshold(config, OutdoorBandSize.Small).Value;
        var mediumThreshold = Math.Max(
            smallThreshold,
            _resolver.ResolveOutdoorBandThreshold(config, OutdoorBandSize.Medium).Value);
        var largeThreshold = Math.Max(
            mediumThreshold,
            _resolver.ResolveOutdoorBandThreshold(config, OutdoorBandSize.Large).Value);

        if (totalTileCount <= smallThreshold)
            return OutdoorBandSize.Small;

        if (totalTileCount <= mediumThreshold)
            return OutdoorBandSize.Medium;

        if (totalTileCount <= largeThreshold)
            return OutdoorBandSize.Large;

        return OutdoorBandSize.Large;
    }
}
