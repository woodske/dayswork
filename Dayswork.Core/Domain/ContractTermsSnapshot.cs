namespace Dayswork.Core.Domain;

using Dayswork.Core.Energy;

public sealed record ContractTermsSnapshot(PricingSnapshot Pricing, WorkerEnergyProfile Energy);
