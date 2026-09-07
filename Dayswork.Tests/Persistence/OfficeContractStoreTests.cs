using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Tests.Persistence.Generators;
using Xunit;

namespace Dayswork.Tests.Persistence;

public sealed class OfficeContractStoreTests
{
    private readonly List<string> _warnings = new();
    private readonly List<(Guid OfficeId, Contract? Contract)> _commits = new();
    private readonly OfficeContractStore _store;

    public OfficeContractStoreTests()
    {
        _store = new OfficeContractStore(msg => _warnings.Add(msg));
        _store.ContractCommitted = (officeId, contract) => _commits.Add((officeId, contract));
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static readonly Guid OfficeA = Guid.Parse("0a0a0a0a-0000-0000-0000-000000000001");
    private static readonly Guid OfficeB = Guid.Parse("0b0b0b0b-0000-0000-0000-000000000002");

    private static Contract MakeContract(Guid officeId, ContractStatus status = ContractStatus.Active) =>
        PersistenceGenerators.CreateExampleCurrentSchemaContract() with
        {
            Id = ContractId.New(),
            OfficeId = officeId,
            OwnerId = 1234L,
            Status = status,
        };

    // ── Add ────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_StoresContractAgainstItsOffice()
    {
        var contract = MakeContract(OfficeA);
        _store.Add(contract);

        Assert.Equal(contract.Id, _store.ForOffice(OfficeA)!.Id);
        Assert.Null(_store.ForOffice(OfficeB));
    }

    [Fact]
    public void Add_UnboundContract_Throws()
    {
        Assert.Throws<ArgumentException>(() => _store.Add(MakeContract(Guid.Empty)));
    }

    [Fact]
    public void Add_SecondOpenContractForTheSameOffice_Throws()
    {
        _store.Add(MakeContract(OfficeA));
        Assert.Throws<InvalidOperationException>(() => _store.Add(MakeContract(OfficeA)));
    }

    [Fact]
    public void Add_ReplacesAClosedContract_SoTheOfficeCanHireAgain()
    {
        _store.Add(MakeContract(OfficeA, ContractStatus.Executed));

        var fresh = MakeContract(OfficeA);
        _store.Add(fresh);

        Assert.Equal(fresh.Id, _store.ForOffice(OfficeA)!.Id);
    }

    [Fact]
    public void EachOfficeHoldsItsOwnContract()
    {
        var a = MakeContract(OfficeA);
        var b = MakeContract(OfficeB);
        _store.Add(a);
        _store.Add(b);

        Assert.Equal(a.Id, _store.ForOffice(OfficeA)!.Id);
        Assert.Equal(b.Id, _store.ForOffice(OfficeB)!.Id);
        Assert.Equal(2, _store.List().Count);
    }

    // ── Revision + commit announcements ────────────────────────────────────

    [Fact]
    public void EveryMutation_BumpsRevisionAndAnnouncesTheCommit()
    {
        var contract = MakeContract(OfficeA);
        Assert.Equal(0, contract.Revision);

        Assert.Equal(1, _store.Add(contract).Revision);
        Assert.Equal(2, _store.Pause(OfficeA).Revision);
        Assert.Equal(3, _store.Resume(OfficeA).Revision);
        Assert.Equal(
            4,
            _store.ReplaceTermsSnapshot(OfficeA, PersistenceGenerators.CreateAlternateTermsSnapshot()).Revision);
        Assert.Equal(5, _store.Cancel(OfficeA).Revision);

        Assert.Equal(5, _commits.Count);
        Assert.All(_commits, commit => Assert.Equal(OfficeA, commit.OfficeId));
        Assert.All(_commits, commit => Assert.NotNull(commit.Contract));
    }

    [Fact]
    public void Hydration_IsNotAMutation_SoItNeitherBumpsRevisionNorCommits()
    {
        var contract = MakeContract(OfficeA) with { Revision = 7 };
        _store.HydrateOffice(OfficeA, contract);

        Assert.Equal(7, _store.ForOffice(OfficeA)!.Revision);
        Assert.Empty(_commits);
    }

    [Fact]
    public void RemoveOffice_AnnouncesANullCommitSoStoredDataIsCleared()
    {
        _store.Add(MakeContract(OfficeA));
        _commits.Clear();

        _store.RemoveOffice(OfficeA);

        Assert.Null(_store.ForOffice(OfficeA));
        Assert.Equal((OfficeA, (Contract?)null), Assert.Single(_commits));
    }

    [Fact]
    public void RemoveOffice_UnknownOffice_IsSilent()
    {
        _store.RemoveOffice(OfficeB);
        Assert.Empty(_commits);
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ReplacesTheOfficesContract()
    {
        var contract = MakeContract(OfficeA) with { Schedule = ContractSchedule.OneTime };
        _store.Add(contract);

        _store.Update(OfficeA, contract with { Schedule = ContractSchedule.Recurring });

        Assert.Equal(ContractSchedule.Recurring, _store.ForOffice(OfficeA)!.Schedule);
    }

    [Fact]
    public void Update_UnknownOffice_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _store.Update(OfficeB, MakeContract(OfficeB)));
    }

    [Fact]
    public void Update_WithADifferentContractId_Throws()
    {
        _store.Add(MakeContract(OfficeA));
        Assert.Throws<ArgumentException>(() => _store.Update(OfficeA, MakeContract(OfficeA)));
    }

    // ── Status transitions ─────────────────────────────────────────────────

    [Theory]
    [InlineData(ContractStatus.Active)]
    [InlineData(ContractStatus.Paused)]
    public void Cancel_OpenContract_SetsStatusCancelled(ContractStatus status)
    {
        _store.Add(MakeContract(OfficeA, status));
        _store.Cancel(OfficeA);
        Assert.Equal(ContractStatus.Cancelled, _store.ForOffice(OfficeA)!.Status);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        _store.Add(MakeContract(OfficeA, ContractStatus.Cancelled));
        Assert.Throws<InvalidOperationException>(() => _store.Cancel(OfficeA));
    }

    [Fact]
    public void Pause_Active_SetsStatusPaused()
    {
        _store.Add(MakeContract(OfficeA));
        _store.Pause(OfficeA);
        Assert.Equal(ContractStatus.Paused, _store.ForOffice(OfficeA)!.Status);
    }

    [Fact]
    public void Pause_AlreadyPaused_Throws()
    {
        _store.Add(MakeContract(OfficeA, ContractStatus.Paused));
        Assert.Throws<InvalidOperationException>(() => _store.Pause(OfficeA));
    }

    [Fact]
    public void Pause_Cancelled_Throws()
    {
        _store.Add(MakeContract(OfficeA, ContractStatus.Cancelled));
        Assert.Throws<InvalidOperationException>(() => _store.Pause(OfficeA));
    }

    [Fact]
    public void Resume_Paused_SetsStatusActive()
    {
        _store.Add(MakeContract(OfficeA, ContractStatus.Paused));
        _store.Resume(OfficeA);
        Assert.Equal(ContractStatus.Active, _store.ForOffice(OfficeA)!.Status);
    }

    [Fact]
    public void Resume_AlreadyActive_Throws()
    {
        _store.Add(MakeContract(OfficeA));
        Assert.Throws<InvalidOperationException>(() => _store.Resume(OfficeA));
    }

    [Fact]
    public void Resume_Cancelled_Throws()
    {
        _store.Add(MakeContract(OfficeA, ContractStatus.Cancelled));
        Assert.Throws<InvalidOperationException>(() => _store.Resume(OfficeA));
    }

    [Fact]
    public void ReplaceTermsSnapshot_UpdatesOnlyTermsSnapshot()
    {
        var replacementTerms = PersistenceGenerators.CreateAlternateTermsSnapshot();
        var added = _store.Add(MakeContract(OfficeA));

        var stored = _store.ReplaceTermsSnapshot(OfficeA, replacementTerms);

        // The revision bump is the mutation's own record, so it is expected here rather than
        // treated as a change to a non-terms field.
        Assert.Equal(added.Revision + 1, stored.Revision);
        Assert.True(ContractStructuralComparer.ContractsEqual(
            added with { TermsSnapshot = replacementTerms, Revision = stored.Revision },
            stored));
    }

    [Fact]
    public void ReplaceTermsSnapshot_UnknownOffice_Throws()
    {
        var terms = PersistenceGenerators.CreateAlternateTermsSnapshot();
        Assert.Throws<KeyNotFoundException>(() => _store.ReplaceTermsSnapshot(OfficeB, terms));
    }

    // ── Open / scheduled queries ───────────────────────────────────────────

    [Theory]
    [InlineData(ContractStatus.Active, true)]
    [InlineData(ContractStatus.Paused, true)]
    [InlineData(ContractStatus.Executed, false)]
    [InlineData(ContractStatus.Cancelled, false)]
    public void HasOpenContract_GatesHiringOnStatus(ContractStatus status, bool expected)
    {
        _store.Add(MakeContract(OfficeA, status));
        Assert.Equal(expected, _store.HasOpenContract(OfficeA));
    }

    [Fact]
    public void HasOpenContract_EmptyOffice_IsFalse()
    {
        Assert.False(_store.HasOpenContract(OfficeA));
    }

    [Fact]
    public void ScheduledForDate_ReturnsEveryOfficeDueToday()
    {
        // Two offices, both recurring, both due every day: two shifts, not one.
        _store.Add(MakeContract(OfficeA) with { Schedule = ContractSchedule.Recurring });
        _store.Add(MakeContract(OfficeB) with { Schedule = ContractSchedule.Recurring });

        Assert.Equal(2, _store.ScheduledForDate(5, Season.Spring, 1).Count);
    }

    [Fact]
    public void ScheduledForDate_OneTimeContract_RunsOnTheDayAfterItWasHired()
    {
        _store.Add(MakeContract(OfficeA) with
        {
            Schedule = ContractSchedule.OneTime,
            HireDate = new GameDate(1, Season.Spring, 1),
        });

        Assert.Single(_store.ScheduledForDate(2, Season.Spring, 1));
        Assert.Empty(_store.ScheduledForDate(3, Season.Spring, 1));
    }

    [Fact]
    public void ScheduledForDate_ExcludesPausedAndCancelledContracts()
    {
        _store.Add(MakeContract(OfficeA, ContractStatus.Paused) with { Schedule = ContractSchedule.Recurring });
        _store.Add(MakeContract(OfficeB, ContractStatus.Cancelled) with { Schedule = ContractSchedule.Recurring });

        Assert.Empty(_store.ScheduledForDate(5, Season.Spring, 1));
    }

    // ── Hydration ──────────────────────────────────────────────────────────

    [Fact]
    public void HydrateAll_LoadsEveryOfficesContractAndReplacesWhatWasThere()
    {
        _store.Add(MakeContract(OfficeA));

        var a = MakeContract(OfficeA);
        var b = MakeContract(OfficeB);
        _store.HydrateAll(new[]
        {
            new KeyValuePair<Guid, Contract>(OfficeA, a),
            new KeyValuePair<Guid, Contract>(OfficeB, b),
        });

        Assert.Equal(a.Id, _store.ForOffice(OfficeA)!.Id);
        Assert.Equal(b.Id, _store.ForOffice(OfficeB)!.Id);
    }

    [Fact]
    public void HydrateOffice_RebindsAContractThatDisagreesWithTheBuildingItWasFoundOn()
    {
        // The building is ground truth: a contract copied onto another office (a moved or rebuilt
        // building, a hand-edited save) belongs to the office holding it, not the one it names.
        _store.HydrateOffice(OfficeB, MakeContract(OfficeA));

        Assert.Equal(OfficeB, _store.ForOffice(OfficeB)!.OfficeId);
        Assert.Single(_warnings);
        Assert.Contains("re-binding", _warnings[0]);
    }

    [Fact]
    public void HydrateOffice_WithNull_ClearsTheOffice()
    {
        _store.Add(MakeContract(OfficeA));
        _store.HydrateOffice(OfficeA, null);

        Assert.Null(_store.ForOffice(OfficeA));
    }
}
