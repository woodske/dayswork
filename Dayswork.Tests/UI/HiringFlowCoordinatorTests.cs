namespace Dayswork.Tests.UI;

using Dayswork.Core.Domain;
using Dayswork.Tests.Persistence.Generators;
using Dayswork.UI;
using Xunit;

public sealed class HiringFlowCoordinatorTests
{
    [Fact]
    public void CreateEditDraft_HydratesSavedTierAndCategoryPriority()
    {
        var id = ContractId.New();
        var contract = PersistenceGenerators.CreateExampleCurrentSchemaContract() with
        {
            Id = id,
            Tier = EnergyTier.Overtime,
            CategoryPriority = new[]
            {
                TaskCategory.Fieldwork,
                TaskCategory.Crops,
                TaskCategory.AnimalCare,
            },
        };

        var draft = HiringFlowCoordinator.CreateEditDraft(id, contract);

        Assert.Equal(id, draft.EditingId);
        Assert.Equal(ContractSchedule.Recurring, draft.Schedule);
        Assert.Equal(EnergyTier.Overtime, draft.Tier);
        Assert.Equal(contract.CategoryPriority, draft.CategoryPriority);
        Assert.True(contract.EnabledTasks.SetEquals(draft.EnabledTasks));
    }
}
