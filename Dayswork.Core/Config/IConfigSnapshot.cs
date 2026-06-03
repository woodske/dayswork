namespace Dayswork.Core.Config;

using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public interface IConfigSnapshot
{
    int HardCapTime { get; }
    int StuckInitialWaitMinutes { get; }
    int StuckPostTeleportWaitMinutes { get; }
    float WorkerWalkPixelsPerTick { get; }
    int WorkerActionAnimationMs { get; }
    int WorkerEntranceHoldTicks { get; }
    bool WorkOnHolidays { get; }

    /// <summary>Worker energy (daily capacity) granted by each purchasable tier.</summary>
    IReadOnlyDictionary<EnergyTier, int> EnergyTierEnergy { get; }

    /// <summary>Gold price the player pays for each purchasable tier.</summary>
    IReadOnlyDictionary<EnergyTier, int> EnergyTierPrice { get; }

    IReadOnlyDictionary<WorkActionKind, int> WorkActionCosts { get; }
}
