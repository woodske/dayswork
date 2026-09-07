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

    private static Contract MakeContract(ContractStatus status = ContractStatus.Active) =>
        PersistenceGenerators.CreateExampleCurrentSchemaContract() with
        {
            Id = ContractId.New(),
            Status = status,
        };

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
        var updated = contract with { Schedule = ContractSchedule.Recurring };
        _store.Update(contract.Id, updated);
        Assert.Equal(ContractSchedule.Recurring, _store.Get(contract.Id).Schedule);
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
        var originalTerms = PersistenceGenerators.CreateExampleCurrentSchemaContract().TermsSnapshot!;
        var replacementTerms = PersistenceGenerators.CreateAlternateTermsSnapshot();
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
        var terms = PersistenceGenerators.CreateAlternateTermsSnapshot();
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

    // ── GetScheduledForDate / GetPrimaryOpen ───────────────────────────────

    [Fact]
    public void GetScheduledForDate_ReturnsActiveContractScheduledForTomorrow()
    {
        // A OneTime contract hired Spring 1 Yr1 is scheduled for the next game day, Spring 2 Yr1.
        var contract = MakeContract(ContractStatus.Active) with
        {
            Schedule = ContractSchedule.OneTime,
            HireDate = new GameDate(1, Season.Spring, 1),
        };
        _store.Add(contract);

        var result = _store.GetScheduledForDate(2, Season.Spring, 1);

        Assert.NotNull(result);
        Assert.Equal(contract.Id, result!.Id);
    }

    [Fact]
    public void GetScheduledForDate_ExcludesContractNotScheduledForDate()
    {
        var contract = MakeContract(ContractStatus.Active) with
        {
            Schedule = ContractSchedule.OneTime,
            HireDate = new GameDate(1, Season.Spring, 1),
        };
        _store.Add(contract);

        // Spring 3 is not the day after Spring 1 — should not be returned.
        Assert.Null(_store.GetScheduledForDate(3, Season.Spring, 1));
    }

    [Fact]
    public void GetScheduledForDate_ReturnsOnlyOneContract_WhenResidualExtrasExist()
    {
        // Saves from the reverted multi-farmhand build can still hold several Active contracts;
        // only one may run (and be charged for) per day.
        for (var i = 0; i < 3; i++)
        {
            _store.Add(MakeContract(ContractStatus.Active) with
            {
                Schedule = ContractSchedule.Recurring,
                HireDate = new GameDate(1, Season.Spring, 1),
            });
        }

        Assert.NotNull(_store.GetScheduledForDate(5, Season.Spring, 1));
    }

    [Fact]
    public void GetPrimaryOpen_PrefersActiveOverPaused()
    {
        var paused = MakeContract(ContractStatus.Paused);
        var active = MakeContract(ContractStatus.Active);
        _store.Add(paused);
        _store.Add(active);

        Assert.Equal(active.Id, _store.GetPrimaryOpen()!.Id);
    }

    [Fact]
    public void Hydrate_CancelsResidualOpenContracts_KeepingOnlyTheActiveOne()
    {
        var paused  = MakeContract(ContractStatus.Paused);
        var active  = MakeContract(ContractStatus.Active);
        var extra   = MakeContract(ContractStatus.Active);
        var done    = MakeContract(ContractStatus.Executed);

        _store.Hydrate(new[] { paused, active, extra, done });

        Assert.Equal(active.Id, _store.GetPrimaryOpen()!.Id);
        Assert.Equal(ContractStatus.Active,    _store.Get(active.Id).Status);
        Assert.Equal(ContractStatus.Cancelled, _store.Get(extra.Id).Status);
        Assert.Equal(ContractStatus.Cancelled, _store.Get(paused.Id).Status);
        Assert.Equal(ContractStatus.Executed,  _store.Get(done.Id).Status);
    }

    [Fact]
    public void GetPrimaryOpen_ReturnsNull_WhenNoOpenContract()
    {
        _store.Add(MakeContract(ContractStatus.Cancelled));

        Assert.Null(_store.GetPrimaryOpen());
    }
}
