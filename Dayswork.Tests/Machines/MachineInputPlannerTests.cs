namespace Dayswork.Tests.Machines;

using Dayswork.Core.Domain;
using Dayswork.Core.Machines;
using Xunit;

public sealed class MachineInputPlannerTests
{
    private static MachineRef Machine(int x, int y, string id = "(BC)16") =>
        new("Farm", new TileCoord(x, y), id);

    private static MachineLoadCandidate Candidate(MachineRef machine, params RecipeRequirement[] options) =>
        new(machine, options);

    [Fact]
    public void Plan_NoCandidates_ReturnsEmpty()
    {
        var plan = MachineInputPlanner.Plan(Array.Empty<MachineLoadCandidate>(), new Dictionary<string, int>());

        Assert.False(plan.HasWork);
        Assert.Empty(plan.Assignments);
        Assert.Empty(plan.Withdrawals);
        Assert.Empty(plan.Unsatisfied);
    }

    [Fact]
    public void Plan_SingleMachineSingleInput_AssignsAndWithdraws()
    {
        var machine = Machine(1, 1);
        var candidates = new[] { Candidate(machine, new RecipeRequirement("(O)254", 1)) };
        var supply = new Dictionary<string, int> { ["(O)254"] = 5 };

        var plan = MachineInputPlanner.Plan(candidates, supply);

        var assignment = Assert.Single(plan.Assignments);
        Assert.Equal(machine, assignment.Machine);
        Assert.Equal("(O)254", assignment.Requirement.InputQualifiedId);
        Assert.Equal(1, Assert.Single(plan.Withdrawals).Value);
        Assert.Empty(plan.Unsatisfied);
    }

    [Fact]
    public void Plan_RequiredCount_WithdrawsTheFullCount()
    {
        // Dehydrator: five fruit per load.
        var machine = Machine(2, 2);
        var candidates = new[] { Candidate(machine, new RecipeRequirement("(O)260", 5)) };
        var supply = new Dictionary<string, int> { ["(O)260"] = 12 };

        var plan = MachineInputPlanner.Plan(candidates, supply);

        Assert.Single(plan.Assignments);
        Assert.Equal(5, plan.Withdrawals["(O)260"]);
    }

    [Fact]
    public void Plan_NotEnoughForRequiredCount_LeavesMachineUnsatisfied()
    {
        var machine = Machine(3, 3);
        var candidates = new[] { Candidate(machine, new RecipeRequirement("(O)260", 5)) };
        var supply = new Dictionary<string, int> { ["(O)260"] = 4 };

        var plan = MachineInputPlanner.Plan(candidates, supply);

        Assert.Empty(plan.Assignments);
        Assert.Empty(plan.Withdrawals);
        Assert.Equal(machine, Assert.Single(plan.Unsatisfied));
    }

    [Fact]
    public void Plan_AtomicMultiInput_OnlyLoadsWhenEveryItemPresent()
    {
        // Fish smoker: one fish + one coal. With no coal the load must not consume the fish.
        var machine = Machine(4, 4);
        var recipe = new RecipeRequirement("(O)142", 1, new[] { new AdditionalInput("(O)382", 1) });
        var candidates = new[] { Candidate(machine, recipe) };
        var supply = new Dictionary<string, int> { ["(O)142"] = 3 }; // no coal

        var plan = MachineInputPlanner.Plan(candidates, supply);

        Assert.Empty(plan.Assignments);
        Assert.Empty(plan.Withdrawals);
        Assert.Equal(machine, Assert.Single(plan.Unsatisfied));
    }

    [Fact]
    public void Plan_AtomicMultiInput_WithdrawsBothWhenPresent()
    {
        var machine = Machine(5, 5);
        var recipe = new RecipeRequirement("(O)142", 1, new[] { new AdditionalInput("(O)382", 1) });
        var candidates = new[] { Candidate(machine, recipe) };
        var supply = new Dictionary<string, int> { ["(O)142"] = 3, ["(O)382"] = 3 };

        var plan = MachineInputPlanner.Plan(candidates, supply);

        Assert.Single(plan.Assignments);
        Assert.Equal(1, plan.Withdrawals["(O)142"]);
        Assert.Equal(1, plan.Withdrawals["(O)382"]);
    }

    [Fact]
    public void Plan_SupplyClamp_FirstMachinesWinScarceSupply()
    {
        // Three kegs want one wheat each, only two wheat available: greedy in order serves the
        // first two and skips the third.
        var first = Machine(1, 0);
        var second = Machine(2, 0);
        var third = Machine(3, 0);
        var candidates = new[]
        {
            Candidate(first, new RecipeRequirement("(O)262", 1)),
            Candidate(second, new RecipeRequirement("(O)262", 1)),
            Candidate(third, new RecipeRequirement("(O)262", 1)),
        };
        var supply = new Dictionary<string, int> { ["(O)262"] = 2 };

        var plan = MachineInputPlanner.Plan(candidates, supply);

        Assert.Equal(2, plan.Assignments.Count);
        Assert.Equal(2, plan.Withdrawals["(O)262"]);
        Assert.Equal(third, Assert.Single(plan.Unsatisfied));
    }

    [Fact]
    public void Plan_PrefersFirstSatisfiableOption_InOrder()
    {
        // Filter preference order: prefer hops, fall back to wheat. Only wheat is in the chest.
        var machine = Machine(6, 6);
        var candidates = new[]
        {
            Candidate(
                machine,
                new RecipeRequirement("(O)304", 1),  // hops — preferred, absent
                new RecipeRequirement("(O)262", 1)), // wheat — fallback, present
        };
        var supply = new Dictionary<string, int> { ["(O)262"] = 4 };

        var plan = MachineInputPlanner.Plan(candidates, supply);

        var assignment = Assert.Single(plan.Assignments);
        Assert.Equal("(O)262", assignment.Requirement.InputQualifiedId);
        Assert.False(plan.Withdrawals.ContainsKey("(O)304"));
    }

    [Fact]
    public void Plan_SharedInputAcrossMachines_AggregatesWithdrawals()
    {
        var first = Machine(1, 1);
        var second = Machine(2, 2);
        var candidates = new[]
        {
            Candidate(first, new RecipeRequirement("(O)260", 5)),
            Candidate(second, new RecipeRequirement("(O)260", 5)),
        };
        var supply = new Dictionary<string, int> { ["(O)260"] = 20 };

        var plan = MachineInputPlanner.Plan(candidates, supply);

        Assert.Equal(2, plan.Assignments.Count);
        Assert.Equal(10, plan.Withdrawals["(O)260"]);
    }

    [Fact]
    public void Plan_EmptySupply_AllUnsatisfied()
    {
        var first = Machine(1, 1);
        var second = Machine(2, 2);
        var candidates = new[]
        {
            Candidate(first, new RecipeRequirement("(O)254", 1)),
            Candidate(second, new RecipeRequirement("(O)254", 1)),
        };

        var plan = MachineInputPlanner.Plan(candidates, new Dictionary<string, int>());

        Assert.Empty(plan.Assignments);
        Assert.Equal(2, plan.Unsatisfied.Count);
    }
}
