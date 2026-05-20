using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Shifts;

public sealed class ShiftStateMachineTests
{
    private static readonly ShiftPhase[] AllPhases =
        Enum.GetValues<ShiftPhase>();

    private static readonly (ShiftPhase from, ShiftPhase to, ShiftIntent? intent)[] LegalSequence =
        new (ShiftPhase, ShiftPhase, ShiftIntent?)[]
        {
            (ShiftPhase.WaitingForSpawn, ShiftPhase.Working,    new IntentMoveToTile(new TileCoord(0, 0))),
            (ShiftPhase.Working,         ShiftPhase.Depositing, new IntentDepositInShippingBin()),
            (ShiftPhase.Depositing,      ShiftPhase.Exiting,    new IntentExitFarm()),
            (ShiftPhase.Exiting,         ShiftPhase.Done,       null),
        };

    // PBT-U10-01: once Done, any Transition always throws.
    [Property(MaxTest = 500, Replay = "")]
    public Property Done_Is_Terminal()
    {
        var targetPhaseGen = Gen.Elements(AllPhases);
        return Prop.ForAll(targetPhaseGen.ToArbitrary(), target =>
        {
            var sm = BuildDoneStateMachine();
            var threw = false;
            try { sm.Transition(target); }
            catch (InvalidOperationException) { threw = true; }
            return threw.Label($"Expected throw transitioning Done → {target}");
        });
    }

    // PBT-U10-02: any non-successor transition from any phase throws.
    [Property(MaxTest = 1000, Replay = "")]
    public Property Illegal_Transitions_Always_Throw()
    {
        // Generate (currentPhase, illegalTarget) pairs.
        var phaseGen = Gen.Elements(
            ShiftPhase.WaitingForSpawn,
            ShiftPhase.Working,
            ShiftPhase.Depositing,
            ShiftPhase.Exiting);

        return Prop.ForAll(phaseGen.ToArbitrary(), Gen.Elements(AllPhases).ToArbitrary(),
            (current, target) =>
            {
                var legalTarget = LegalSuccessor(current);
                if (target == legalTarget) return true.ToProperty(); // skip legal case

                var sm = BuildAt(current);
                var threw = false;
                try { sm.Transition(target); }
                catch (InvalidOperationException) { threw = true; }
                return threw.Label($"Expected throw: {current} → {target} (legal is {legalTarget})");
            });
    }

    // Sanity: full legal sequence completes without throwing.
    [Fact]
    public void Full_Legal_Sequence_Completes()
    {
        var sm = new ShiftStateMachine();
        foreach (var (_, to, intent) in LegalSequence)
            sm.Transition(to, intent);
        Assert.Equal(ShiftPhase.Done, sm.Phase);
        Assert.Null(sm.CurrentIntent);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static ShiftPhase LegalSuccessor(ShiftPhase phase) => phase switch
    {
        ShiftPhase.WaitingForSpawn => ShiftPhase.Working,
        ShiftPhase.Working         => ShiftPhase.Depositing,
        ShiftPhase.Depositing      => ShiftPhase.Exiting,
        ShiftPhase.Exiting         => ShiftPhase.Done,
        _                          => (ShiftPhase)(-1), // Done has no successor
    };

    private static ShiftStateMachine BuildDoneStateMachine()
    {
        var sm = new ShiftStateMachine();
        foreach (var (_, to, intent) in LegalSequence)
            sm.Transition(to, intent);
        return sm;
    }

    private static ShiftStateMachine BuildAt(ShiftPhase target)
    {
        var sm = new ShiftStateMachine();
        foreach (var (_, to, intent) in LegalSequence)
        {
            sm.Transition(to, intent);
            if (to == target) break;
        }
        return sm;
    }
}
