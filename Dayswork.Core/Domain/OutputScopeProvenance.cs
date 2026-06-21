namespace Dayswork.Core.Domain;

public sealed record OutputScopeProvenance(OutputScopeFamily Family, string ScopeName)
{
    public static OutputScopeProvenance Unknown { get; } = new(OutputScopeFamily.Unknown, string.Empty);

    public static OutputScopeProvenance Outdoor(string locationName = "Farm") =>
        new(OutputScopeFamily.Outdoor, locationName);

    public static OutputScopeProvenance AnimalBuilding(string locationName) =>
        new(OutputScopeFamily.AnimalBuilding, locationName);

    public static OutputScopeProvenance Greenhouse(string locationName) =>
        new(OutputScopeFamily.Greenhouse, locationName);

    public static OutputScopeProvenance ManagedCrop(string assignmentKey) =>
        new(OutputScopeFamily.ManagedCrop, assignmentKey);

    public static OutputScopeProvenance Machine(string groupId) =>
        new(OutputScopeFamily.Machine, groupId);

    public static OutputScopeProvenance Cave() =>
        new(OutputScopeFamily.Cave, "FarmCave");
}
