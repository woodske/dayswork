using Dayswork.Core.Domain;

namespace Dayswork.Core.Persistence;

/// <summary>
/// The in-memory map of <c>office building id → its one contract</c> (2.0 hard rule 3: one
/// contract per office, N offices ⇒ N contracts). Durable storage is each office's
/// <c>Building.modData</c>; this store owns the invariants and announces every committed mutation
/// through <see cref="ContractCommitted"/> so the host can write that one office's string back.
/// Hydration goes the other way and is deliberately silent — loading is not a mutation.
/// </summary>
public sealed class OfficeContractStore
{
    private readonly Dictionary<Guid, Contract> _byOffice = new();
    private readonly Action<string> _logWarning;

    public OfficeContractStore(Action<string> logWarning)
    {
        _logWarning = logWarning;
    }

    /// <summary>
    /// Invoked after every committed mutation with the office and its new contract (null once the
    /// office holds none). The host persists it; nothing else may write contract modData.
    /// </summary>
    public Action<Guid, Contract?>? ContractCommitted { get; set; }

    // ── Reads ────────────────────────────────────────────────────────────────

    public Contract? ForOffice(Guid officeId) =>
        _byOffice.TryGetValue(officeId, out var contract) ? contract : null;

    /// <summary>An office is free to hire when it holds no contract, or holds one that has run its
    /// course (Executed) or been cancelled.</summary>
    public bool HasOpenContract(Guid officeId) => IsOpen(ForOffice(officeId));

    public IReadOnlyList<Contract> List() => _byOffice.Values.ToList().AsReadOnly();

    public IReadOnlyList<Contract> OpenContracts() =>
        _byOffice.Values.Where(IsOpen).ToList().AsReadOnly();

    /// <summary>Every Active contract due to run on the given date — one per office, in no
    /// particular order. The caller starts a shift for each.</summary>
    public IReadOnlyList<Contract> ScheduledForDate(int day, Season season, int year)
    {
        var target = new GameDate(day, season, year);
        return _byOffice.Values
            .Where(contract => contract.Status == ContractStatus.Active && IsScheduledForDate(contract, target))
            .ToList()
            .AsReadOnly();
    }

    // ── Mutations (each bumps Revision and announces the commit) ──────────────

    public Contract Add(Contract contract)
    {
        if (contract.OfficeId == Guid.Empty)
            throw new ArgumentException("A contract must be bound to an office before it can be stored.", nameof(contract));
        if (HasOpenContract(contract.OfficeId))
            throw new InvalidOperationException($"Office {contract.OfficeId} already has an open contract.");

        return Commit(contract.OfficeId, contract);
    }

    public Contract Update(Guid officeId, Contract updated)
    {
        var existing = Require(officeId);
        if (updated.Id != existing.Id)
            throw new ArgumentException($"Updated contract {updated.Id} does not match office {officeId}'s contract {existing.Id}.", nameof(updated));

        return Commit(officeId, updated with { OfficeId = officeId });
    }

    public Contract Cancel(Guid officeId)
    {
        var existing = Require(officeId);
        if (existing.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException($"Office {officeId}'s contract is already cancelled.");

        return Commit(officeId, existing with { Status = ContractStatus.Cancelled });
    }

    public Contract Pause(Guid officeId)
    {
        var existing = Require(officeId);
        if (existing.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException($"Cannot pause office {officeId}'s cancelled contract.");
        if (existing.Status == ContractStatus.Paused)
            throw new InvalidOperationException($"Office {officeId}'s contract is already paused.");

        return Commit(officeId, existing with { Status = ContractStatus.Paused });
    }

    public Contract Resume(Guid officeId)
    {
        var existing = Require(officeId);
        if (existing.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException($"Cannot resume office {officeId}'s cancelled contract.");
        if (existing.Status == ContractStatus.Active)
            throw new InvalidOperationException($"Office {officeId}'s contract is already active.");

        return Commit(officeId, existing with { Status = ContractStatus.Active });
    }

    public Contract ReplaceTermsSnapshot(Guid officeId, ContractTermsSnapshot terms) =>
        Commit(officeId, Require(officeId) with { TermsSnapshot = terms });

    /// <summary>
    /// Drops an office's contract entirely — the office was demolished, so the contract goes with
    /// the building (no refund; see the 2.0 plan's D1). Announces a null commit so any cached
    /// modData view clears too.
    /// </summary>
    public void RemoveOffice(Guid officeId)
    {
        if (!_byOffice.Remove(officeId))
            return;

        ContractCommitted?.Invoke(officeId, null);
    }

    // ── Hydration (load path — no commits, no revision bumps) ─────────────────

    /// <summary>Loads one office's stored contract. A contract whose OfficeId disagrees with the
    /// building it was read from is re-bound to that building: the building is ground truth.</summary>
    public void HydrateOffice(Guid officeId, Contract? contract)
    {
        if (contract is null)
        {
            _byOffice.Remove(officeId);
            return;
        }

        if (contract.OfficeId != officeId)
        {
            _logWarning($"Contract {contract.Id} was stored on office {officeId} but claims office {contract.OfficeId} — re-binding it to the building it was found on.");
            contract = contract with { OfficeId = officeId };
        }

        _byOffice[officeId] = contract;
    }

    /// <summary>Replaces the whole map — used on save load and on returning to the title.</summary>
    public void HydrateAll(IEnumerable<KeyValuePair<Guid, Contract>> contracts)
    {
        _byOffice.Clear();
        foreach (var (officeId, contract) in contracts)
            HydrateOffice(officeId, contract);
    }

    public void Clear() => _byOffice.Clear();

    // ── Internals ────────────────────────────────────────────────────────────

    private Contract Require(Guid officeId) =>
        ForOffice(officeId) ?? throw new KeyNotFoundException($"Office {officeId} has no contract.");

    private Contract Commit(Guid officeId, Contract contract)
    {
        var committed = contract with { Revision = contract.Revision + 1 };
        _byOffice[officeId] = committed;
        ContractCommitted?.Invoke(officeId, committed);
        return committed;
    }

    private static bool IsOpen(Contract? contract) =>
        contract is { Status: ContractStatus.Active or ContractStatus.Paused };

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
}
