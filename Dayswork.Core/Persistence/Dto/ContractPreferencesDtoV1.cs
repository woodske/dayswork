namespace Dayswork.Core.Persistence.Dto;

public sealed class ContractPreferencesDtoV1
{
    public bool? AvoidBlueGrass { get; set; }

    // Enum serialized as its name; null on saves from before the idle-task preference existed.
    public string? IdleTask { get; set; }
}
