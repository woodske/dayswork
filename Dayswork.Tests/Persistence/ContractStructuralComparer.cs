using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

namespace Dayswork.Tests.Persistence;

internal static class ContractStructuralComparer
{
    public static bool ContractsEqual(Contract left, Contract right) =>
        left.Id == right.Id
        && left.EnabledTasks.SetEquals(right.EnabledTasks)
        && DestinationMapsEqual(left.TaskDestinations, right.TaskDestinations)
        && left.Schedule == right.Schedule
        && left.Status == right.Status
        && left.HireDate == right.HireDate
        && left.Tier == right.Tier
        && left.CategoryPriority.SequenceEqual(right.CategoryPriority)
        && ScopeSelectionsEqual(left.ScopeSelection, right.ScopeSelection)
        && TermsSnapshotsEqual(left.TermsSnapshot, right.TermsSnapshot)
        && CropPlansEqual(left.CropPlan, right.CropPlan);

    public static bool ScopeSelectionsEqual(ContractScopeSelection left, ContractScopeSelection right) =>
        ZonesEqual(left.OutdoorZones, right.OutdoorZones)
            && left.AnimalBuildings.SequenceEqual(right.AnimalBuildings)
            && left.Greenhouses.SequenceEqual(right.Greenhouses);

    public static bool TermsSnapshotsEqual(ContractTermsSnapshot? left, ContractTermsSnapshot? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        return PricingSnapshotsEqual(left.Pricing, right.Pricing)
            && WorkerEnergyProfilesEqual(left.Energy, right.Energy);
    }

    public static bool PricingSnapshotsEqual(PricingSnapshot left, PricingSnapshot right) =>
        left.TotalPrice == right.TotalPrice;

    public static bool WorkerEnergyProfilesEqual(WorkerEnergyProfile left, WorkerEnergyProfile right) =>
        left.DailyCapacity == right.DailyCapacity
        && left.ActionCosts.Count == right.ActionCosts.Count
        && left.ActionCosts.All(kvp => right.ActionCosts.TryGetValue(kvp.Key, out var value) && value == kvp.Value);

    public static string DescribeContract(Contract contract)
    {
        var enabledTasks = string.Join(",", contract.EnabledTasks.OrderBy(task => task));
        var destinations = string.Join(
            ";",
            contract.TaskDestinations
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}"));

        var scope = $"{string.Join(";", contract.ScopeSelection.OutdoorZones.Select(DescribeZone))}|"
              + $"{string.Join(";", contract.ScopeSelection.AnimalBuildings.Select(building => $"{building.LocationName}:{building.Tier}"))}|"
              + $"{(contract.ScopeSelection.Greenhouses.Count == 0 ? "none" : string.Join(",", contract.ScopeSelection.Greenhouses.Select(greenhouse => greenhouse.LocationName)))}";

        var terms = $"{contract.TermsSnapshot.Pricing.TotalPrice}|{contract.TermsSnapshot.Energy.DailyCapacity}|"
              + $"{string.Join(";", contract.TermsSnapshot.Energy.ActionCosts.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"))}";

        var priority = $"{contract.Tier}|{string.Join(",", contract.CategoryPriority)}";

        return $"{contract.Id}|{enabledTasks}|{destinations}|{contract.Schedule}|{contract.Status}|{contract.HireDate.Day}:{contract.HireDate.Season}:{contract.HireDate.Year}|{scope}|{terms}|{priority}";
    }

    public static bool CropPlansEqual(CropPlan left, CropPlan right) =>
        left.Assignments.Count == right.Assignments.Count
        && left.Assignments.Zip(right.Assignments, CropAssignmentsEqual).All(equal => equal);

    private static bool CropAssignmentsEqual(CropZoneAssignment left, CropZoneAssignment right) =>
        left.Zone == right.Zone
        && left.Mode == right.Mode
        && Equals(left.OutputChest, right.OutputChest)
        && left.GroupId == right.GroupId
        && left.Choices.Count == right.Choices.Count
        && left.Choices.Zip(right.Choices, CropChoicesEqual).All(equal => equal);

    private static bool CropChoicesEqual(SeasonCropChoice left, SeasonCropChoice right) =>
        left.Season == right.Season
        && left.StorePreference == right.StorePreference
        && left.IsLocked == right.IsLocked
        && left.OriginSeason == right.OriginSeason
        && CropDescriptorsEqual(left.Crop, right.Crop);

    private static bool CropDescriptorsEqual(CropDescriptor left, CropDescriptor right) =>
        left.CropItemId == right.CropItemId
        && left.SeedItemId == right.SeedItemId
        && left.FertilizerItemId == right.FertilizerItemId
        && left.DaysToFirstHarvest == right.DaysToFirstHarvest
        && left.FertilizedDaysToFirstHarvest == right.FertilizedDaysToFirstHarvest
        && left.RegrowDays == right.RegrowDays
        && left.Seasons.SequenceEqual(right.Seasons);

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
