using Dayswork.Core.Domain;

namespace Dayswork.Core.Persistence;

public sealed class ContractStore
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

    public void ReplaceTermsSnapshot(ContractId id, ContractTermsSnapshot terms)
    {
        var existing = Get(id);
        _contracts[id] = existing with { TermsSnapshot = terms };
    }

    public IReadOnlyList<Contract> List() =>
        _contracts.Values.ToList().AsReadOnly();

    /// <summary>
    /// The single open contract (hard rule 3: at most one Active/Paused contract at a time).
    /// Active wins over Paused so residual contracts left by older saves can never shadow the one
    /// the shift engine actually runs; returns null when nothing is open.
    /// </summary>
    public Contract? GetPrimaryOpen() =>
        _contracts.Values.FirstOrDefault(c => c.Status == ContractStatus.Active)
        ?? _contracts.Values.FirstOrDefault(c => c.Status == ContractStatus.Paused);

    /// <summary>
    /// The one Active contract due to run on the given date, or null. Single-contract by design
    /// (hard rule 3) — residual extras from older saves are ignored rather than charged for.
    /// </summary>
    public Contract? GetScheduledForDate(int day, Season season, int year)
    {
        var target = new GameDate(day, season, year);
        return _contracts.Values
            .FirstOrDefault(c => c.Status == ContractStatus.Active && IsScheduledForDate(c, target));
    }

    private static bool IsScheduledForDate(Contract contract, GameDate date) =>
        contract.Schedule == ContractSchedule.Recurring || IsNextGameDay(contract.HireDate, date);

    // Stardew seasons are 28 days; four seasons per year cycling Spring→Summer→Fall→Winter→Spring.
    private static bool IsNextGameDay(GameDate hire, GameDate candidate)
    {
        var nextDay    = hire.Day + 1;
        var nextSeason = hire.Season;
        var nextYear   = hire.Year;

        if (nextDay > 28)
        {
            nextDay    = 1;
            nextSeason = (Season)(((int)hire.Season + 1) % 4);
            if (nextSeason == Season.Spring) nextYear++;   // wrapped past Winter
        }

        return candidate == new GameDate(nextDay, nextSeason, nextYear);
    }

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

        CancelResidualOpenContracts();
    }

    // Saves written by the reverted multi-farmhand build can hold several open contracts. Only one
    // is reachable (hard rule 3), so the extras are cancelled on load: they were invisible in the
    // UI yet still billed the player at day start.
    private void CancelResidualOpenContracts()
    {
        var primary = GetPrimaryOpen();
        if (primary is null)
            return;

        foreach (var contract in _contracts.Values.ToList())
        {
            if (contract.Id == primary.Id)
                continue;
            if (contract.Status is not (ContractStatus.Active or ContractStatus.Paused))
                continue;

            _contracts[contract.Id] = contract with { Status = ContractStatus.Cancelled };
            _logWarning($"Cancelled residual open contract {contract.Id} — only one contract is supported.");
        }
    }
}
