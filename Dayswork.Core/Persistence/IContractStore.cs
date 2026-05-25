using Dayswork.Core.Domain;

namespace Dayswork.Core.Persistence;

public interface IContractStore
{
    ContractId Add(Contract contract);
    Contract Get(ContractId id);
    void Update(ContractId id, Contract updated);
    void Cancel(ContractId id);
    void Pause(ContractId id);
    void Resume(ContractId id);
    void ReplaceTermsSnapshot(ContractId id, ContractTermsSnapshot terms);
    IReadOnlyList<Contract> List();
    IReadOnlyList<Contract> ListActiveForDate(int day, Season season, int year);
    void Hydrate(IReadOnlyList<Contract> contracts);
}
