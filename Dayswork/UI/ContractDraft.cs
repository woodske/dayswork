using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

namespace Dayswork.UI;

internal sealed class ContractDraft
{
    public HashSet<TaskKind> EnabledTasks { get; } = new();
    public List<Zone> OutdoorZones { get; } = new();
    public List<AnimalBuildingSelection> AnimalBuildings { get; } = new();
    public Dictionary<TaskKind, DestinationKey> Destinations { get; } = new();
    public ContractSchedule Schedule { get; set; } = ContractSchedule.OneTime;
    public ContractId? EditingId { get; set; }
    public DraftHydrationMode HydrationMode { get; set; } = DraftHydrationMode.NewDraft;
    public List<GreenhouseSelection> Greenhouses { get; } = new();
    public DraftPreviewState PreviewState { get; set; } = DraftPreviewState.Empty;

    public bool IsEditing => EditingId.HasValue;

    public ContractScopeSelection ScopeSelection =>
        new(
            OutdoorZones: OutdoorZones
                .Distinct()
                .OrderBy(DescribeZone, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly(),
            AnimalBuildings: AnimalBuildings
                .Distinct()
                .OrderBy(building => building.LocationName, StringComparer.Ordinal)
                .ThenBy(building => building.Tier)
                .ToList()
                .AsReadOnly(),
            Greenhouses: Greenhouses
                .Distinct()
                .OrderBy(greenhouse => greenhouse.LocationName, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly());

    private static string DescribeZone(Zone zone) =>
        $"{zone.LocationName}|{zone.TopLeft.X}|{zone.TopLeft.Y}|{zone.BottomRight.X}|{zone.BottomRight.Y}";
}

internal enum DraftHydrationMode
{
    NewDraft,
    HydratedFromAuthoritativeScope,
    DerivedFromCompatibilityZones,
}

internal sealed record DraftPreviewState(
    ContractPreview Preview,
    IReadOnlyList<ServiceContributionRow> ServiceRows,
    ScopeSummaryModel ScopeSummary,
    SummaryReviewModel ReviewModel)
{
    public static DraftPreviewState Empty { get; } =
        new(
            new ContractPreview(
                IsValid: false,
                ValidationIssues: Array.Empty<ContractValidationIssue>(),
                ProposedTerms: null),
            Array.Empty<ServiceContributionRow>(),
            new ScopeSummaryModel(
                Array.Empty<Zone>(),
                Array.Empty<AnimalBuildingSelection>(),
                Array.Empty<GreenhouseSelection>()),
            new SummaryReviewModel(
                Array.Empty<TaskKind>(),
                new ScopeSummaryModel(
                    Array.Empty<Zone>(),
                    Array.Empty<AnimalBuildingSelection>(),
                    Array.Empty<GreenhouseSelection>()),
                null,
                null,
                PaymentTimingKind.OneTimeChargeNow,
                Array.Empty<ValidationDisplayMessage>(),
                false));
}

internal sealed record ServiceContributionRow(
    TaskKind Service,
    ServiceContributionState RowState,
    IReadOnlyList<PricingLineItem> PricingLines,
    int? DisplayAmount);

internal enum ServiceContributionState
{
    Charged,
    NeedsOutdoorScope,
    NeedsAnimalBuildingScope,
    NeedsGreenhouseScope,
}

internal sealed record ScopeSummaryModel(
    IReadOnlyList<Zone> OutdoorZones,
    IReadOnlyList<AnimalBuildingSelection> AnimalBuildings,
    IReadOnlyList<GreenhouseSelection> Greenhouses)
{
    public GreenhouseSelection? Greenhouse => Greenhouses.Count > 0 ? Greenhouses[0] : null;
}

internal sealed record SummaryReviewModel(
    IReadOnlyList<TaskKind> SelectedTasks,
    ScopeSummaryModel ScopeSummary,
    PricingSnapshot? Pricing,
    WorkerEnergyProfile? WorkerEnergy,
    PaymentTimingKind PaymentTimingKind,
    IReadOnlyList<ValidationDisplayMessage> ValidationMessages,
    bool CanConfirm);

internal enum PaymentTimingKind
{
    OneTimeChargeNow,
    RecurringStartsNextEligibleDay,
    RecurringEditAppliesNextEligibleDay,
}

internal sealed record ValidationDisplayMessage(
    ContractValidationCode Code,
    TaskKind? RelatedTask);
