using Dayswork.Core.Domain;

namespace Dayswork.Core.Persistence;

public interface ISaveDataSerializer
{
    string Serialize(IReadOnlyList<Contract> contracts, string modVersion);
    IReadOnlyList<Contract> Deserialize(string? json);
}
