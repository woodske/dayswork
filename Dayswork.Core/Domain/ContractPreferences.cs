namespace Dayswork.Core.Domain;

public sealed record ContractPreferences(bool AvoidBlueGrass = true)
{
    public static readonly ContractPreferences Default = new();

    // Contracts from before preferences were introduced: preserve the original behavior
    // (clear all grass, including blue grass).
    public static readonly ContractPreferences Legacy = new(AvoidBlueGrass: false);
}
