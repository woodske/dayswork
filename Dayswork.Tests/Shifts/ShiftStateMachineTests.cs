using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Shifts;

public sealed class ShiftStateMachineTests
{
    private static readonly ShiftPhase[] AllPhases = Enum.GetValues<ShiftPhase>();

    // ── Once Done, every Transition throws. ─────────────────────
    [Property(MaxTest = 500, Replay = "")]
    public Property Done_Is_Terminal()
    {
        var targetGen = Gen.Elements(AllPhases);
        return Prop.ForAll(targetGen.ToArbitrary(), target =>
        {
            var sm = BuildAt(ShiftPhase.Done);
            var threw = false;
            try { sm.Transition(target); }
            catch (InvalidOperationException) { threw = true; }
            return threw.Label($"Expected throw transitioning Done → {target}");
        });
    }

    // ── Any target NOT in the legal successor set throws. ────────
    // (Includes the Stuck/Recovering edges.)
    [Property(MaxTest = 2000, Replay = "")]
    public Property Illegal_Transitions_Always_Throw()
    {
        // Generate any non-Done phase (Done is covered above) and any target.
        var sourceGen = Gen.Elements(
            ShiftPhase.WaitingForSpawn,
            ShiftPhase.Working,
            ShiftPhase.Stuck,
            ShiftPhase.Recovering,
            ShiftPhase.Depositing,
            ShiftPhase.Exiting);

        return Prop.ForAll(sourceGen.ToArbitrary(), Gen.Elements(AllPhases).ToArbitrary(),
            (source, target) =>
            {
                if (LegalSuccessors(source).Contains(target))
                    return true.ToProperty(); // skip legal pairs

                var sm = BuildAt(source);
                var threw = false;
                try { sm.Transition(target); }
                catch (InvalidOperationException) { threw = true; }
                return threw.Label($"Expected throw: {source} → {target}");
            });
    }

    // ── Stuck/Recovering reachability. ──────────────────────────
    // Stuck is reachable only from Working; Recovering only from Stuck; neither from Done.
    [Property(MaxTest = 500, Replay = "")]
    public Property Stuck_Only_Reachable_From_Working()
    {
        var nonWorkingSource = Gen.Elements(
            ShiftPhase.WaitingForSpawn,
            ShiftPhase.Stuck,
            ShiftPhase.Recovering,
            ShiftPhase.Depositing,
            ShiftPhase.Exiting,
            ShiftPhase.Done);

        return Prop.ForAll(nonWorkingSource.ToArbitrary(), source =>
        {
            var sm = BuildAt(source);
            var threw = false;
            try { sm.Transition(ShiftPhase.Stuck, new IntentPlayEmote(8)); }
            catch (InvalidOperationException) { threw = true; }
            return threw.Label($"Expected Stuck to be unreachable from {source}");
        });
    }

    [Property(MaxTest = 500, Replay = "")]
    public Property Recovering_Only_Reachable_From_Stuck()
    {
        var nonStuckSource = Gen.Elements(
            ShiftPhase.WaitingForSpawn,
            ShiftPhase.Working,
            ShiftPhase.Recovering,
            ShiftPhase.Depositing,
            ShiftPhase.Exiting,
            ShiftPhase.Done);

        return Prop.ForAll(nonStuckSource.ToArbitrary(), source =>
        {
            var sm = BuildAt(source);
            var threw = false;
            try { sm.Transition(ShiftPhase.Recovering, new IntentTeleportToTile(new TileCoord(1, 1))); }
            catch (InvalidOperationException) { threw = true; }
            return threw.Label($"Expected Recovering to be unreachable from {source}");
        });
    }

    // ── Table-driven: all legal edges succeed. ───────────────────────────────
    [Theory]
    [MemberData(nameof(LegalEdges))]
    public void Legal_Transition_Succeeds(ShiftPhase from, ShiftPhase to, ShiftIntent? intent)
    {
        var sm = BuildAt(from);
        var ex = Record.Exception(() => sm.Transition(to, intent));
        Assert.Null(ex);
        Assert.Equal(to, sm.Phase);
    }

    public static IEnumerable<object?[]> LegalEdges() =>
        new object?[][]
        {
            new object?[] { ShiftPhase.WaitingForSpawn, ShiftPhase.Working,    new IntentMoveToTile(new TileCoord(0, 0)) },
            new object?[] { ShiftPhase.Working,         ShiftPhase.Depositing, new IntentDepositInShippingBin() },
            new object?[] { ShiftPhase.Working,         ShiftPhase.Stuck,      new IntentPlayEmote(8) },
            new object?[] { ShiftPhase.Stuck,           ShiftPhase.Recovering, new IntentTeleportToTile(new TileCoord(1, 1)) },
            new object?[] { ShiftPhase.Recovering,      ShiftPhase.Working,    new IntentMoveToTile(new TileCoord(2, 2)) },
            new object?[] { ShiftPhase.Recovering,      ShiftPhase.Depositing, new IntentDepositInShippingBin() },
            new object?[] { ShiftPhase.Depositing,      ShiftPhase.Exiting,    new IntentExitFarm() },
            new object?[] { ShiftPhase.Exiting,         ShiftPhase.Done,       null },
        };

    // Sanity: primary happy path (no stuck) still completes end-to-end.
    [Fact]
    public void Full_Happy_Path_Completes()
    {
        var sm = new ShiftStateMachine();
        sm.Transition(ShiftPhase.Working,    new IntentMoveToTile(new TileCoord(0, 0)));
        sm.Transition(ShiftPhase.Depositing, new IntentDepositInShippingBin());
        sm.Transition(ShiftPhase.Exiting,    new IntentExitFarm());
        sm.Transition(ShiftPhase.Done);
        Assert.Equal(ShiftPhase.Done, sm.Phase);
        Assert.Null(sm.CurrentIntent);
    }

    // Sanity: stuck-and-recover path.
    [Fact]
    public void Stuck_Recover_Then_Complete_Path()
    {
        var sm = new ShiftStateMachine();
        sm.Transition(ShiftPhase.Working,    new IntentMoveToTile(new TileCoord(0, 0)));
        sm.Transition(ShiftPhase.Stuck,      new IntentPlayEmote(8));
        sm.Transition(ShiftPhase.Recovering, new IntentTeleportToTile(new TileCoord(5, 5)));
        sm.Transition(ShiftPhase.Working,    new IntentMoveToTile(new TileCoord(5, 5)));
        sm.Transition(ShiftPhase.Depositing, new IntentDepositInShippingBin());
        sm.Transition(ShiftPhase.Exiting,    new IntentExitFarm());
        sm.Transition(ShiftPhase.Done);
        Assert.Equal(ShiftPhase.Done, sm.Phase);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HashSet<ShiftPhase> LegalSuccessors(ShiftPhase phase) => phase switch
    {
        ShiftPhase.WaitingForSpawn => new() { ShiftPhase.Working },
        ShiftPhase.Working         => new() { ShiftPhase.Depositing, ShiftPhase.Stuck },
        ShiftPhase.Stuck           => new() { ShiftPhase.Recovering },
        ShiftPhase.Recovering      => new() { ShiftPhase.Working, ShiftPhase.Depositing },
        ShiftPhase.Depositing      => new() { ShiftPhase.Exiting },
        ShiftPhase.Exiting         => new() { ShiftPhase.Done },
        ShiftPhase.Done            => new() { },
        _                          => new() { },
    };

    /// <summary>Build a state machine that is currently at <paramref name="target"/>.</summary>
    private static ShiftStateMachine BuildAt(ShiftPhase target)
    {
        var sm = new ShiftStateMachine();
        // Walk the shortest valid path to reach each phase.
        switch (target)
        {
            case ShiftPhase.WaitingForSpawn:
                break;
            case ShiftPhase.Working:
                sm.Transition(ShiftPhase.Working, new IntentMoveToTile(new TileCoord(0, 0)));
                break;
            case ShiftPhase.Stuck:
                sm.Transition(ShiftPhase.Working,  new IntentMoveToTile(new TileCoord(0, 0)));
                sm.Transition(ShiftPhase.Stuck,    new IntentPlayEmote(8));
                break;
            case ShiftPhase.Recovering:
                sm.Transition(ShiftPhase.Working,    new IntentMoveToTile(new TileCoord(0, 0)));
                sm.Transition(ShiftPhase.Stuck,      new IntentPlayEmote(8));
                sm.Transition(ShiftPhase.Recovering, new IntentTeleportToTile(new TileCoord(1, 1)));
                break;
            case ShiftPhase.Depositing:
                sm.Transition(ShiftPhase.Working,    new IntentMoveToTile(new TileCoord(0, 0)));
                sm.Transition(ShiftPhase.Depositing, new IntentDepositInShippingBin());
                break;
            case ShiftPhase.Exiting:
                sm.Transition(ShiftPhase.Working,    new IntentMoveToTile(new TileCoord(0, 0)));
                sm.Transition(ShiftPhase.Depositing, new IntentDepositInShippingBin());
                sm.Transition(ShiftPhase.Exiting,    new IntentExitFarm());
                break;
            case ShiftPhase.Done:
                sm.Transition(ShiftPhase.Working,    new IntentMoveToTile(new TileCoord(0, 0)));
                sm.Transition(ShiftPhase.Depositing, new IntentDepositInShippingBin());
                sm.Transition(ShiftPhase.Exiting,    new IntentExitFarm());
                sm.Transition(ShiftPhase.Done);
                break;
        }
        return sm;
    }
}
