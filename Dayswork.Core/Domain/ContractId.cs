namespace Dayswork.Core.Domain;

public readonly record struct ContractId(Guid Value)
{
    public static ContractId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
