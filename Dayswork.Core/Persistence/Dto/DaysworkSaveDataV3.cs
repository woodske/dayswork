namespace Dayswork.Core.Persistence.Dto;

/// <summary>
/// The <c>Dayswork.Contracts</c> save-data envelope. From schema v4 the contracts themselves live
/// in each office's <c>Building.modData</c>; this envelope is written as a marker so a 1.x mod
/// opening the save sees a newer schema and loads nothing (it cannot double-bill).
/// </summary>
public sealed class DaysworkSaveDataV3
{
    public int SchemaVersion { get; set; } = 3;
    public string ModVersion { get; set; } = "";
    public List<ContractDtoV3> Contracts { get; set; } = new();

    /// <summary>Set by the v4 marker envelope once contracts have moved into building modData.</summary>
    public bool MigratedToBuildingModData { get; set; }
}
