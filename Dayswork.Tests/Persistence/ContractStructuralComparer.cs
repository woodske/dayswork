using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

namespace Dayswork.Tests.Persistence;

internal static class ContractStructuralComparer
{
    public static bool ContractsEqual(Contract left, Contract right) =>
        left.Id == right.Id
        && left.EnabledTasks.SetEquals(right.EnabledTasks)
        && ZonesEqual(left.Zones, right.Zones)
        && DestinationMapsEqual(left.TaskDestinations, right.TaskDestinations)
        && left.Schedule == right.Schedule
        && left.Status == right.Status
        && left.HireDate == right.HireDate
        && left.DepositAmount == right.DepositAmount
        && left.HourlyRate == right.HourlyRate
        && ScopeSelectionsEqual(left.ScopeSelection, right.ScopeSelection)
        && TermsSnapshotsEqual(left.TermsSnapshot, right.TermsSnapshot);

    public static bool ScopeSelectionsEqual(ContractScopeSelection? left, ContractScopeSelection? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        return ZonesEqual(left.OutdoorZones, right.OutdoorZones)
            && left.AnimalBuildings.SequenceEqual(right.AnimalBuildings)
            && Equals(left.Greenhouse, right.Greenhouse);
    }

    public static bool TermsSnapshotsEqual(ContractTermsSnapshot? left, ContractTermsSnapshot? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        return PricingSnapshotsEqual(left.Pricing, right.Pricing)
            && WorkerEnergyProfilesEqual(left.Energy, right.Energy);
    }

    public static bool PricingSnapshotsEqual(PricingSnapshot left, PricingSnapshot right) =>
        left.OutdoorSubtotal == right.OutdoorSubtotal
        && left.AnimalSubtotal == right.AnimalSubtotal
        && left.GreenhouseSubtotal == right.GreenhouseSubtotal
        && left.TotalPrice == right.TotalPrice
        && left.LineItems.SequenceEqual(right.LineItems);

    public static bool WorkerEnergyProfilesEqual(WorkerEnergyProfile left, WorkerEnergyProfile right) =>
        left.DailyCapacity == right.DailyCapacity
        && left.ActionCosts.Count == right.ActionCosts.Count
        && left.ActionCosts.All(kvp => right.ActionCosts.TryGetValue(kvp.Key, out var value) && value == kvp.Value);

    public static string DescribeContract(Contract contract)
    {
        var enabledTasks = string.Join(",", contract.EnabledTasks.OrderBy(task => task));
        var zones = string.Join(";", contract.Zones.Select(DescribeZone));
        var destinations = string.Join(
            ";",
            contract.TaskDestinations
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}"));

        var scope = contract.ScopeSelection is null
            ? "none"
            : $"{string.Join(";", contract.ScopeSelection.OutdoorZones.Select(DescribeZone))}|"
              + $"{string.Join(";", contract.ScopeSelection.AnimalBuildings.Select(building => $"{building.LocationName}:{building.Tier}"))}|"
              + $"{contract.ScopeSelection.Greenhouse?.LocationName ?? "none"}";

        var terms = contract.TermsSnapshot is null
            ? "none"
            : $"{contract.TermsSnapshot.Pricing.TotalPrice}|{contract.TermsSnapshot.Energy.DailyCapacity}|"
              + $"{string.Join(";", contract.TermsSnapshot.Energy.ActionCosts.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"))}";

        return $"{contract.Id}|{enabledTasks}|{zones}|{destinations}|{contract.Schedule}|{contract.Status}|{contract.HireDate.Day}:{contract.HireDate.Season}:{contract.HireDate.Year}|{contract.DepositAmount}|{contract.HourlyRate}|{scope}|{terms}";
    }

    private static bool ZonesEqual(IReadOnlyList<Zone> left, IReadOnlyList<Zone> right) =>
        left.Count == right.Count && left.SequenceEqual(right);

    private static bool DestinationMapsEqual(
        IReadOnlyDictionary<TaskKind, DestinationKey> left,
        IReadOnlyDictionary<TaskKind, DestinationKey> right) =>
        left.Count == right.Count
        && left.All(kvp => right.TryGetValue(kvp.Key, out var value) && Equals(kvp.Value, value));

    private static string DescribeZone(Zone zone) =>
        $"{zone.LocationName}:{zone.TopLeft.X},{zone.TopLeft.Y}->{zone.BottomRight.X},{zone.BottomRight.Y}";
}
