using Dayswork.Core.Domain;

namespace Dayswork.Core.Persistence;

public sealed class ContractStore : IContractStore
{
    private readonly Dictionary<ContractId, Contract> _contracts = new();
    private readonly Action<string> _logWarning;

    public ContractStore(Action<string> logWarning)
    {
        _logWarning = logWarning;
    }

    public ContractId Add(Contract contract)
    {
        if (_contracts.ContainsKey(contract.Id))
            throw new InvalidOperationException($"A contract with Id {contract.Id} already exists.");
        _contracts[contract.Id] = contract;
        return contract.Id;
    }

    public Contract Get(ContractId id)
    {
        if (!_contracts.TryGetValue(id, out var contract))
            throw new KeyNotFoundException($"No contract with Id {id}.");
        return contract;
    }

    public void Update(ContractId id, Contract updated)
    {
        if (!_contracts.ContainsKey(id))
            throw new KeyNotFoundException($"No contract with Id {id}.");
        if (updated.Id != id)
            throw new ArgumentException($"Updated contract Id {updated.Id} does not match target Id {id}.");
        _contracts[id] = updated;
    }

    public void Cancel(ContractId id)
    {
        var existing = Get(id);
        if (existing.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException($"Contract {id} is already cancelled.");
        _contracts[id] = existing with { Status = ContractStatus.Cancelled };
    }

    public void Pause(ContractId id)
    {
        var existing = Get(id);
        if (existing.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException($"Cannot pause cancelled contract {id}.");
        if (existing.Status == ContractStatus.Paused)
            throw new InvalidOperationException($"Contract {id} is already paused.");
        _contracts[id] = existing with { Status = ContractStatus.Paused };
    }

    public void Resume(ContractId id)
    {
        var existing = Get(id);
        if (existing.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException($"Cannot resume cancelled contract {id}.");
        if (existing.Status == ContractStatus.Active)
            throw new InvalidOperationException($"Contract {id} is already active.");
        _contracts[id] = existing with { Status = ContractStatus.Active };
    }

    public IReadOnlyList<Contract> List() =>
        _contracts.Values.ToList().AsReadOnly();

    public IReadOnlyList<Contract> ListActiveForDate(int day, Season season, int year) =>
        throw new NotImplementedException("ListActiveForDate is implemented in U-09.");

    public void Hydrate(IReadOnlyList<Contract> contracts)
    {
        _contracts.Clear();
        foreach (var contract in contracts)
        {
            if (_contracts.ContainsKey(contract.Id))
            {
                _logWarning($"Duplicate ContractId {contract.Id} in save data — skipping.");
                continue;
            }
            _contracts[contract.Id] = contract;
        }
    }
}
