using Dayswork.Core.Config;
using Dayswork.Core.Domain;

namespace Dayswork.Integration;

public sealed class ModConfig
{
    private static readonly IConfigSnapshot DefaultSnapshot = ConfigDefaults.Build();

    public int HardCapTime { get; set; } = DefaultSnapshot.HardCapTime;
    public int StuckInitialWaitMinutes { get; set; } = DefaultSnapshot.StuckInitialWaitMinutes;
    public int StuckPostTeleportWaitMinutes { get; set; } = DefaultSnapshot.StuckPostTeleportWaitMinutes;
    public float WorkerWalkPixelsPerTick { get; set; } = DefaultSnapshot.WorkerWalkPixelsPerTick;
    public int WorkerActionAnimationMs { get; set; } = DefaultSnapshot.WorkerActionAnimationMs;
    public int WorkerEntranceHoldTicks { get; set; } = DefaultSnapshot.WorkerEntranceHoldTicks;
    public bool WorkOnHolidays { get; set; } = DefaultSnapshot.WorkOnHolidays;

    /// <summary>
    /// Global Manage Crops preferred store (U-MC-06, DEV-MC-06-01): <c>Either</c>, <c>Pierre</c>,
    /// or <c>Joja</c>. Applies to every crop group; the farmhand buys at the preferred store and
    /// falls back to the other store for anything it does not stock.
    /// </summary>
    public string PreferredCropStore { get; set; } = "Either";

    public Dictionary<string, int> EnergyTierEnergy { get; set; } = CreateEnergyTierEnergyDefaults();
    public Dictionary<string, int> EnergyTierPrice { get; set; } = CreateEnergyTierPriceDefaults();
    public Dictionary<string, int> WorkActionCosts { get; set; } = CreateWorkActionCostDefaults();

    public static ModConfig CreateDefaults() => new();

    private static Dictionary<string, int> CreateEnergyTierEnergyDefaults() =>
        DefaultSnapshot.EnergyTierEnergy.ToDictionary(
            kvp => ContractTermsConfigKeyCodec.EncodeEnergyTierKey(kvp.Key),
            kvp => kvp.Value);

    private static Dictionary<string, int> CreateEnergyTierPriceDefaults() =>
        DefaultSnapshot.EnergyTierPrice.ToDictionary(
            kvp => ContractTermsConfigKeyCodec.EncodeEnergyTierKey(kvp.Key),
            kvp => kvp.Value);

    private static Dictionary<string, int> CreateWorkActionCostDefaults() =>
        DefaultSnapshot.WorkActionCosts.ToDictionary(
            kvp => ContractTermsConfigKeyCodec.EncodeWorkActionKey(kvp.Key),
            kvp => kvp.Value);
}
