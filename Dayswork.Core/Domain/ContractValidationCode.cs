namespace Dayswork.Core.Domain;

public enum ContractValidationCode
{
    NoChargeableScopeTaskPair,
    NoOutdoorScopeForSelectedOutdoorService,
    NoAnimalBuildingForSelectedAnimalService,
    NoGreenhouseScopeForSelectedGreenhouseService,

    /// <summary>
    /// A machine group is set to collect-and-reload but has no input chest, so reload can't run and
    /// the group degrades to collect-only. Informational (non-blocking).
    /// </summary>
    MachineGroupNeedsInputChest,
}
