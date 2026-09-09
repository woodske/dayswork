using Xunit;

namespace Dayswork.Tests.Orchestration;

public sealed class ReviewFixWiringTests
{
    [Fact]
    public void Dayswork2Review_R2_WrapUpInterruptsShoppingBeforeStartingTerminalDeposit()
    {
        var source = ReadSource("Dayswork", "Orchestration", "ShiftOrchestrator.cs");
        var method = MethodBody(source, "internal void QueueWrapUpNow", "private void SpendStaminaForBeat");

        AssertOrder(method, "RequestBoundaryStop", "InterruptForWrapUp", "BeginDeposit");
    }

    [Fact]
    public void Dayswork2Review_R2_ShoppingInterruptionDisablesPurchasesBeforeReturnTravel()
    {
        var source = ReadSource("Dayswork", "Orchestration", "ManagedShoppingCoordinator.cs");
        var method = MethodBody(source, "public bool InterruptForWrapUp", "public void SettleForShutdown");

        AssertOrder(method, "_wrapAfterReturn = true", "_groups.Clear()", "_group = null", "BeginReturnToFarm()");
    }

    [Fact]
    public void Dayswork2Review_R2_SleepUsesIdempotentShoppingShutdownSettlement()
    {
        var source = ReadSource("Dayswork", "Orchestration", "ShiftOrchestrator.cs");
        var method = MethodBody(source, "public void StopForSleepAndSettle", "public void OnTimeChanged");

        Assert.Contains("Shopping.SettleForShutdown()", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Shopping.SettleCarriedItems", method, StringComparison.Ordinal);
    }

    [Fact]
    public void Dayswork2Review_R3_IncompatiblePeerSuspendsAndCleansBeforeAnyKickAttempt()
    {
        var source = ReadSource("Dayswork", "Net", "DaysworkNetwork.cs");
        var method = MethodBody(source, "private void HandleIncompatiblePeer", "private void IssuePendingKick");
        var suspensionCallback = MethodBody(source, "private void OnSuspensionBegan", "private void OnSuspensionEnded");

        AssertOrder(method, "_peers.MarkIncompatible", "_suspension.MarkIncompatible", "IssuePendingKick");
        Assert.Contains("_fleet.StopForSleepAndSettle()", suspensionCallback, StringComparison.Ordinal);
    }

    [Fact]
    public void Dayswork2Review_R4_RemoteMutationsAreCheckedAgainstCompletedHandshake()
    {
        var source = ReadSource("Dayswork", "Net", "DaysworkNetwork.cs");
        var dispatch = MethodBody(source, "private void Dispatch", "private static bool IsFromHost");

        Assert.Contains("_peers.IsCompatible(e.FromPlayerID)", dispatch, StringComparison.Ordinal);
        AssertOrder(dispatch, "RejectUnverifiedCommit", "_handler.HandleCommit");
        AssertOrder(dispatch, "RejectUnverifiedAction", "_handler.HandleAction");
    }

    [Fact]
    public void Dayswork2Review_R5_OwnerAvailabilityPrecedesFestivalConsumptionAndCharging()
    {
        var source = ReadSource("Dayswork", "Orchestration", "RecurringContractScheduler.cs");
        var method = MethodBody(source, "private void StartOne", "private void HandleFestival");

        AssertOrder(method, "CanRunForOwner", "if (holidaySkip", "ContractSchedule.OneTime", "StartRecurring");
    }

    [Fact]
    public void Dayswork2Review_R6_ExactTreeHookIsRegisteredAndHistoricalSweepsAreGone()
    {
        var entry = ReadSource("Dayswork", "ModEntry.cs");
        var attribution = ReadSource("Dayswork", "Orchestration", "TreeDropAttribution.cs");
        var debris = ReadSource("Dayswork", "Orchestration", "ShiftOrchestrator.Debris.cs");
        var actions = ReadSource("Dayswork", "Orchestration", "ShiftOrchestrator.TaskActions.cs");

        Assert.Contains("TreeDropAttribution.Install", entry, StringComparison.Ordinal);
        Assert.Contains("nameof(Tree.tickUpdate)", attribution, StringComparison.Ordinal);
        Assert.Contains("typeof(GameTime)", attribution, StringComparison.Ordinal);
        Assert.Contains("Priority.Last", attribution, StringComparison.Ordinal);
        Assert.Contains("Priority.First", attribution, StringComparison.Ordinal);
        Assert.Contains("TreeDropAttribution.Register", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("PendingDebrisSweep", debris, StringComparison.Ordinal);
        Assert.DoesNotContain("QueueDelayedDebrisSweep", actions, StringComparison.Ordinal);
    }

    [Fact]
    public void Dayswork2Review_R6_RealDebrisPreservesQualityAndFlavorBeforeWorldRemoval()
    {
        var source = ReadSource("Dayswork", "Orchestration", "ShiftOrchestrator.Debris.cs");
        var transfer = MethodBody(source, "private bool TryBufferDebris", "internal void CaptureAttributedTreeDebris");
        var attributed = MethodBody(source, "internal void CaptureAttributedTreeDebris", "private static void LogInvalidDebris");

        AssertOrder(transfer, "debris.item.Stack", "Quality", "Session.Flavors.Register", "Session.Ctx.Buffer.Add");
        Assert.Contains("debris.Chunks.Count", transfer, StringComparison.Ordinal);
        AssertOrder(attributed, "TryBufferDebris", "location.debris.Remove");
    }

    [Fact]
    public void Dayswork2Review_R6_TeardownFlushesBeforeDiscardingTheSession()
    {
        var source = ReadSource("Dayswork", "Orchestration", "ShiftOrchestrator.cs");
        var sleep = MethodBody(source, "public void StopForSleepAndSettle", "public void OnTimeChanged");
        var reset = MethodBody(source, "public void ResetForSessionBoundary", "public void StartShift");

        AssertOrder(sleep, "TreeDropAttribution.FlushFor", "AppendUndeliveredToOverflow", "_session = null");
        AssertOrder(reset, "TreeDropAttribution.ClearFor", "_session = null");
    }

    private static void AssertOrder(string source, params string[] tokens)
    {
        var previous = -1;
        foreach (var token in tokens)
        {
            var current = source.IndexOf(token, StringComparison.Ordinal);
            Assert.True(current > previous, $"Expected '{token}' after the preceding operation.");
            previous = current;
        }
    }

    private static string MethodBody(string source, string startToken, string endToken)
    {
        var start = source.IndexOf(startToken, StringComparison.Ordinal);
        var end = source.IndexOf(endToken, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not isolate method between '{startToken}' and '{endToken}'.");
        return source[start..end];
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { FindWorkspaceRoot() }.Concat(parts).ToArray()));

    private static string FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dayswork.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the Dayswork workspace root.");
    }
}
