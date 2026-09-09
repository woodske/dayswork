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

    [Fact]
    public void Dayswork2Review_R7_ClientAppliesHostStateBeforeTheMenuCallbackRedraws()
    {
        var source = ReadSource("Dayswork", "Net", "ContractRequestClient.cs");
        var commit = MethodBody(source, "public void ReceiveCommitResponse", "public void ReceiveActionResponse");
        var action = MethodBody(source, "public void ReceiveActionResponse", "/// <summary>");
        var apply = MethodBody(source, "internal void ApplyAuthoritativeState", "public void OnUpdateTicked");

        AssertOrder(commit, "ApplyAuthoritativeState(response.State)", "pending.OnCommit");
        AssertOrder(action, "ApplyAuthoritativeState(response.State)", "pending.OnAction");
        AssertOrder(apply, "AuthoritativeContractCache.TryAccept", "_store.HydrateOffice");
        Assert.Contains("Authority.HostIsInThisProcess", apply, StringComparison.Ordinal);
    }

    [Fact]
    public void Dayswork2Review_R7_HostStampsEveryAnswerWithTheOfficeState()
    {
        var source = ReadSource("Dayswork", "Net", "ContractRequestHandler.cs");
        var reject = MethodBody(source, "private ContractCommitResponseMessage Reject", "private ContractActionResponseMessage RejectAction");
        var rejectAction = MethodBody(source, "private ContractActionResponseMessage RejectAction", "private ContractActionResponseMessage AcceptAction");
        var accept = source[source.IndexOf("private ContractActionResponseMessage AcceptAction", StringComparison.Ordinal)..];
        var build = MethodBody(source, "private AuthoritativeContractState? BuildState", "private ContractCommitResponseMessage Reject");

        Assert.Contains("State = BuildState(officeId)", reject, StringComparison.Ordinal);
        Assert.Contains("State = BuildState(officeId)", rejectAction, StringComparison.Ordinal);
        Assert.Contains("State = BuildState(officeId)", accept, StringComparison.Ordinal);
        Assert.Contains("Sequence = ++_stateSequence", build, StringComparison.Ordinal);
        Assert.Contains("_fleet.IsShiftRunning", build, StringComparison.Ordinal);
    }

    [Fact]
    public void Dayswork2Review_R7_EditsSubmitTheRevisionTheyWereAuthoredAgainst()
    {
        var source = ReadSource("Dayswork", "UI", "HiringFlowCoordinator.cs");
        var confirm = MethodBody(source, "private void ConfirmContract", "private void ShowStaleContractChoice");
        var stale = MethodBody(source, "private void ShowStaleContractChoice", "private static Contract BuildContract");
        var edit = MethodBody(source, "internal static ContractDraft CreateEditDraft", "private void MaybeCloseFlow");

        Assert.Contains("expectedRevision: draft.BaseRevision", confirm, StringComparison.Ordinal);
        Assert.Contains("isEdit: isEdit", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("expectedRevision: original?.Revision", confirm, StringComparison.Ordinal);
        AssertOrder(confirm, "ContractRejectionCode.Stale", "ShowStaleContractChoice");
        Assert.Contains("BaseRevision = contract.Revision", edit, StringComparison.Ordinal);
        // The player picks; nothing rebases the draft on its own.
        AssertOrder(stale, "onReviewTheirs: () => OpenEditFlow", "onKeepMine:", "draft.BaseRevision = current.Revision");
    }

    [Fact]
    public void Dayswork2Review_R7_RemoteCancelConsultsTheHostsShiftFlagNotTheEmptyLocalFleet()
    {
        var source = ReadSource("Dayswork", "UI", "ContractMenu.cs");
        var cancel = MethodBody(source, "private void TryCancel", "/// <summary>");
        var running = MethodBody(source, "private bool ShiftIsRunning", "/// <summary>");

        Assert.Contains("ShiftIsRunning(contract)", cancel, StringComparison.Ordinal);
        AssertOrder(running, "Authority.IsRemoteClient", "AuthoritativeContractCache.ShiftRunning", "ModEntry.Fleet.IsShiftRunning");
    }

    [Fact]
    public void Dayswork2Review_R8_EveryUpgradesEntryPathAsksTheHostForOwnership()
    {
        var source = ReadSource("Dayswork", "UI", "HiringFlowCoordinator.cs");
        var manage = MethodBody(source, "public void OpenManageFlow", "/// <summary>");
        var fromManage = MethodBody(source, "public void ShowUpgradesFromManage", "private void ShowUpgrades(");
        var request = MethodBody(source, "private void RequestMenuSnapshot", "// Hub-and-spoke");

        Assert.Contains("RequestMenuSnapshot(officeId, clearWorldSnapshot: false)", manage, StringComparison.Ordinal);
        Assert.Contains("RequestMenuSnapshot(officeId, clearWorldSnapshot: false)", fromManage, StringComparison.Ordinal);
        // Only the world half is dropped while a request is outstanding — clearing the upgrade
        // half is what made an owned upgrade read as unpurchased.
        Assert.Contains("MenuSnapshotCache.ClearWorldSnapshot()", request, StringComparison.Ordinal);
        Assert.DoesNotContain("MenuSnapshotCache.Current = null", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Dayswork2Review_R8_UpgradeStateIsHeldApartFromTheWorldSnapshotAndCorrelated()
    {
        var cache = ReadSource("Dayswork", "Net", "MenuSnapshotCache.cs");
        var client = ReadSource("Dayswork", "Net", "ContractRequestClient.cs");
        var network = ReadSource("Dayswork", "Net", "DaysworkNetwork.cs");
        var applyPurchase = MethodBody(cache, "public static void ApplyUpgradeState", "public static void ClearCurrentScreen");
        var applySnapshot = MethodBody(cache, "public static void ApplySnapshot", "/// <summary>");

        // A purchase acknowledgment updates ownership with no world snapshot in hand.
        Assert.DoesNotContain("Snapshot.Value is not", applyPurchase, StringComparison.Ordinal);
        Assert.Contains("Merge(", applyPurchase, StringComparison.Ordinal);
        Assert.Contains("PendingRequestId.Value", applySnapshot, StringComparison.Ordinal);
        Assert.Contains("MenuSnapshotCache.BeginRequest(requestId)", client, StringComparison.Ordinal);
        Assert.Contains("MenuSnapshotCache.ApplySnapshot(snapshot)", network, StringComparison.Ordinal);
    }

    [Fact]
    public void Dayswork2Review_R8_UpgradesPageWaitsRatherThanShowingAnEmptyPurchaseHistory()
    {
        var source = ReadSource("Dayswork", "UI", "UpgradesMenu.cs");
        var update = MethodBody(source, "public override void update", "protected override ILayoutElement BuildLayout");
        var layout = MethodBody(source, "protected override ILayoutElement BuildLayout", "private ILayoutElement BuildUpgradeRow");

        Assert.Contains("_state is null", layout, StringComparison.Ordinal);
        Assert.Contains("ui.net.waiting_for_host", layout, StringComparison.Ordinal);
        AssertOrder(update, "_readState()", "_state = current", "Rebuild()");
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
