namespace Dayswork.Core.Persistence.Dto;

public sealed class ContractTermsSnapshotDto
{
    public PricingSnapshotDto Pricing { get; set; } = new();
    public WorkerEnergyProfileDto Energy { get; set; } = new();
}
