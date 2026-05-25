using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Tests.Persistence.Generators;
using Xunit;

namespace Dayswork.Tests.Persistence;

public sealed class ContractStoreTests
{
    private readonly List<string> _warnings = new List<string>();
    private readonly ContractStore _store;

    public ContractStoreTests()
    {
        _store = new ContractStore(msg => _warnings.Add(msg));
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static Contract MakeContract(ContractStatus status = ContractStatus.Active)
    {
        var id = ContractId.New();
        var tasks = new HashSet<TaskKind> { TaskKind.ClearWeeds };
        var zones = new List<Zone> { new("Farm", new TileCoord(0, 0), new TileCoord(9, 9)) };
        var destinations = new Dictionary<TaskKind, DestinationKey>
        {
            [TaskKind.ClearWeeds] = ShippingBinDestination.Instance,
        };
        return new Contract(
            Id: id,
            EnabledTasks: tasks,
            Zones: zones.AsReadOnly(),
            TaskDestinations: destinations,
            Schedule: ContractSchedule.OneTime,
            Status: status,
            HireDate: new GameDate(1, Season.Spring, 1),
            DepositAmount: 100,
            HourlyRate: 70);
    }

    // ── Add ────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_StoresContract_GetReturnsIt()
    {
        var contract = MakeContract();
        _store.Add(contract);
        Assert.Equal(contract, _store.Get(contract.Id));
    }

    [Fact]
    public void Add_DuplicateId_Throws()
    {
        var contract = MakeContract();
        _store.Add(contract);
        Assert.Throws<InvalidOperationException>(() => _store.Add(contract));
    }

    // ── Get ────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_UnknownId_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _store.Get(ContractId.New()));
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ReplacesContract()
    {
        var contract = MakeContract();
        _store.Add(contract);
        var updated = contract with { HourlyRate = 999 };
        _store.Update(contract.Id, updated);
        Assert.Equal(999, _store.Get(contract.Id).HourlyRate);
    }

    [Fact]
    public void Update_UnknownId_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            _store.Update(ContractId.New(), MakeContract()));
    }

    [Fact]
    public void Update_IdMismatch_Throws()
    {
        var contract = MakeContract();
        _store.Add(contract);
        var mismatch = MakeContract();
        Assert.Throws<ArgumentException>(() => _store.Update(contract.Id, mismatch));
    }

    // ── Cancel ─────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_Active_SetsStatusCancelled()
    {
        var contract = MakeContract(ContractStatus.Active);
        _store.Add(contract);
        _store.Cancel(contract.Id);
        Assert.Equal(ContractStatus.Cancelled, _store.Get(contract.Id).Status);
    }

    [Fact]
    public void Cancel_Paused_SetsStatusCancelled()
    {
        var contract = MakeContract(ContractStatus.Paused);
        _store.Add(contract);
        _store.Cancel(contract.Id);
        Assert.Equal(ContractStatus.Cancelled, _store.Get(contract.Id).Status);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var contract = MakeContract(ContractStatus.Cancelled);
        _store.Add(contract);
        Assert.Throws<InvalidOperationException>(() => _store.Cancel(contract.Id));
    }

    // ── Pause ──────────────────────────────────────────────────────────────

    [Fact]
    public void Pause_Active_SetsStatusPaused()
    {
        var contract = MakeContract(ContractStatus.Active);
        _store.Add(contract);
        _store.Pause(contract.Id);
        Assert.Equal(ContractStatus.Paused, _store.Get(contract.Id).Status);
    }

    [Fact]
    public void Pause_AlreadyPaused_Throws()
    {
        var contract = MakeContract(ContractStatus.Paused);
        _store.Add(contract);
        Assert.Throws<InvalidOperationException>(() => _store.Pause(contract.Id));
    }

    [Fact]
    public void Pause_Cancelled_Throws()
    {
        var contract = MakeContract(ContractStatus.Cancelled);
        _store.Add(contract);
        Assert.Throws<InvalidOperationException>(() => _store.Pause(contract.Id));
    }

    // ── Resume ─────────────────────────────────────────────────────────────

    [Fact]
    public void Resume_Paused_SetsStatusActive()
    {
        var contract = MakeContract(ContractStatus.Paused);
        _store.Add(contract);
        _store.Resume(contract.Id);
        Assert.Equal(ContractStatus.Active, _store.Get(contract.Id).Status);
    }

    [Fact]
    public void Resume_AlreadyActive_Throws()
    {
        var contract = MakeContract(ContractStatus.Active);
        _store.Add(contract);
        Assert.Throws<InvalidOperationException>(() => _store.Resume(contract.Id));
    }

    [Fact]
    public void Resume_Cancelled_Throws()
    {
        var contract = MakeContract(ContractStatus.Cancelled);
        _store.Add(contract);
        Assert.Throws<InvalidOperationException>(() => _store.Resume(contract.Id));
    }

    [Fact]
    public void ReplaceTermsSnapshot_UpdatesOnlyTermsSnapshot()
    {
        var originalTerms = U19PersistenceGen.CreateExampleCurrentSchemaContract().TermsSnapshot!;
        var replacementTerms = U19PersistenceGen.CreateAlternateTermsSnapshot();
        var contract = MakeContract() with { TermsSnapshot = originalTerms };

        _store.Add(contract);
        _store.ReplaceTermsSnapshot(contract.Id, replacementTerms);

        var stored = _store.Get(contract.Id);
        Assert.True(ContractStructuralComparer.ContractsEqual(
            contract with { TermsSnapshot = replacementTerms },
            stored));
    }

    [Fact]
    public void ReplaceTermsSnapshot_UnknownId_Throws()
    {
        var terms = U19PersistenceGen.CreateAlternateTermsSnapshot();
        Assert.Throws<KeyNotFoundException>(() => _store.ReplaceTermsSnapshot(ContractId.New(), terms));
    }

    // ── List ───────────────────────────────────────────────────────────────

    [Fact]
    public void List_ReturnsAllContracts()
    {
        var a = MakeContract();
        var b = MakeContract();
        _store.Add(a);
        _store.Add(b);
        var list = _store.List();
        Assert.Equal(2, list.Count);
        Assert.Contains(a, list);
        Assert.Contains(b, list);
    }

    // ── Hydrate ────────────────────────────────────────────────────────────

    [Fact]
    public void Hydrate_ReplacesExistingContracts_Atomically()
    {
        var old = MakeContract();
        _store.Add(old);

        var fresh = MakeContract();
        _store.Hydrate(new List<Contract> { fresh });

        var list = _store.List();
        Assert.Single(list);
        Assert.Equal(fresh, list[0]);
    }

    [Fact]
    public void Hydrate_DuplicateId_SkipsSecondAndWarns()
    {
        var contract = MakeContract();
        _store.Hydrate(new List<Contract> { contract, contract });

        Assert.Single(_store.List());
        Assert.Single(_warnings);
        Assert.Contains("Duplicate ContractId", _warnings[0]);
    }

    // ── ListActiveForDate ──────────────────────────────────────────────────

    [Fact]
    public void ListActiveForDate_ReturnsActiveContractScheduledForTomorrow()
    {
        // MakeContract uses HireDate = Spring 1 Yr1; next game day is Spring 2 Yr1.
        var contract = MakeContract(ContractStatus.Active);
        _store.Add(contract);

        var result = _store.ListActiveForDate(2, Season.Spring, 1);

        Assert.Single(result);
        Assert.Equal(contract.Id, result[0].Id);
    }

    [Fact]
    public void ListActiveForDate_ExcludesContractNotScheduledForDate()
    {
        var contract = MakeContract(ContractStatus.Active);
        _store.Add(contract);

        // Spring 3 is not the day after Spring 1 — should not be returned.
        var result = _store.ListActiveForDate(3, Season.Spring, 1);

        Assert.Empty(result);
    }
}
