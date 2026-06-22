namespace Dayswork.Core.Persistence.Dto;

public sealed class FarmhandUpgradeSaveDataV1
{
    public int SchemaVersion { get; set; } = 1;
    public bool SpeedPurchased { get; set; }
    public bool EnergyPurchased { get; set; }
}
