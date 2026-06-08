namespace Dayswork.Tests.UI;

using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.UI;
using Xunit;

public sealed class CropPlanDraftTests
{
    private static CropDescriptor Parsnip() =>
        new("crop.parsnip", "seed.parsnip", null, 4, null, null, new[] { Season.Spring });

    private static CropDescriptor Pumpkin() =>
        new("crop.pumpkin", "seed.pumpkin", null, 13, null, null, new[] { Season.Fall });

    private static CropDescriptor Corn() =>
        new("crop.corn", "seed.corn", null, 14, null, 4, new[] { Season.Summer, Season.Fall });

    [Fact]
    public void AddGroup_CreatesEditableGroup()
    {
        var draft = new CropPlanDraft();

        var group = draft.AddGroup();
        var ok = group.TrySetCrop(Season.Spring, Parsnip(), "Parsnip", out var reason);

        Assert.True(ok);
        Assert.Null(reason);
        Assert.Single(draft.Groups);
        Assert.True(group.IsConfigured(Season.Spring));
        Assert.True(group.HasAnyConfiguredSeason);
        Assert.Equal("Parsnip", group.DisplayCropName(Season.Spring));
    }

    [Fact]
    public void TrySetCrop_MultiSeasonCrop_LocksLinkedSeasonInsideGroup()
    {
        var group = new CropPlanDraft().AddGroup();

        group.TrySetCrop(Season.Summer, Corn(), "Corn", out _);

        Assert.Equal(SeasonLockState.Open, group.LockState(Season.Summer));
        Assert.Equal(SeasonLockState.MultiSeasonLocked, group.LockState(Season.Fall));
        Assert.Equal(Season.Summer, group.LockOrigin(Season.Fall));
        Assert.Equal("Corn", group.DisplayCropName(Season.Fall));
    }

    [Fact]
    public void TrySetCrop_OnLockedSeason_IsRejected()
    {
        var group = new CropPlanDraft().AddGroup();
        group.TrySetCrop(Season.Summer, Corn(), "Corn", out _);

        var ok = group.TrySetCrop(Season.Fall, Pumpkin(), "Pumpkin", out var reason);

        Assert.False(ok);
        Assert.Equal(nameof(SeasonLockState.MultiSeasonLocked), reason);
        Assert.False(group.IsConfigured(Season.Fall));
    }

    [Fact]
    public void TrySetCrop_MultiSeasonConflictWithConfiguredSeason_IsRejected()
    {
        var group = new CropPlanDraft().AddGroup();
        group.TrySetCrop(Season.Fall, Pumpkin(), "Pumpkin", out _);

        var ok = group.TrySetCrop(Season.Summer, Corn(), "Corn", out var reason);

        Assert.False(ok);
        Assert.Equal(Season.Fall.ToString(), reason);
        Assert.True(group.IsConfigured(Season.Fall));
        Assert.False(group.IsConfigured(Season.Summer));
    }

    [Fact]
    public void ClearSeason_OnOrigin_ReleasesLockedSeasons()
    {
        var group = new CropPlanDraft().AddGroup();
        group.TrySetCrop(Season.Summer, Corn(), "Corn", out _);

        group.ClearSeason(Season.Summer);

        Assert.Equal(SeasonLockState.Open, group.LockState(Season.Fall));
        Assert.False(group.HasAnyConfiguredSeason);
    }

    [Fact]
    public void BuildAssignmentChoices_FoldsFertilizerAndReplantAcrossLockedSeasons()
    {
        var group = new CropPlanDraft().AddGroup();
        group.TrySetCrop(Season.Summer, Corn(), "Corn", out _);
        group.SetFertilizer(Season.Summer, "fert.basic", "Basic Fertilizer");
        group.ToggleAutoReplant(Season.Summer);

        var choices = group.BuildAssignmentChoices();

        var summer = choices.Single(choice => choice.Season == Season.Summer);
        var fall = choices.Single(choice => choice.Season == Season.Fall);
        Assert.Equal("fert.basic", summer.Crop.FertilizerItemId);
        Assert.True(summer.AutoReplant);
        Assert.True(fall.IsLocked);
        Assert.Equal("fert.basic", fall.Crop.FertilizerItemId);
        Assert.True(fall.AutoReplant);
    }

    [Fact]
    public void BuildCropPlan_FlattensMultipleGroupsWithPerGroupOutputChest()
    {
        var draft = new CropPlanDraft();
        var first = draft.AddGroup();
        var second = draft.AddGroup();
        first.TrySetCrop(Season.Spring, Parsnip(), "Parsnip", out _);
        second.TrySetCrop(Season.Fall, Pumpkin(), "Pumpkin", out _);
        first.OutputChest = new ChestRef("Farm", new TileCoord(3, 4));
        second.OutputChest = new ChestRef("Farm", new TileCoord(9, 10));
        draft.SetGroupZones(first.Id, new[] { new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1)) });
        draft.SetGroupZones(second.Id, new[] { new Zone("Farm", new TileCoord(5, 5), new TileCoord(6, 6)) });

        var plan = draft.BuildCropPlan();

        Assert.True(plan.IsEnabled);
        Assert.Equal(2, plan.Assignments.Count);
        Assert.Contains(plan.Assignments, assignment =>
            assignment.GroupId == first.Id
            && assignment.OutputChest == new ChestRef("Farm", new TileCoord(3, 4))
            && assignment.Choices.Single().Crop.SeedItemId == "seed.parsnip");
        Assert.Contains(plan.Assignments, assignment =>
            assignment.GroupId == second.Id
            && assignment.OutputChest == new ChestRef("Farm", new TileCoord(9, 10))
            && assignment.Choices.Single().Crop.SeedItemId == "seed.pumpkin");
    }

    [Fact]
    public void ProtectedZones_ExcludesActiveGroup()
    {
        var draft = new CropPlanDraft();
        var active = draft.AddGroup();
        var other = draft.AddGroup();
        var activeZone = new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1));
        var otherZone = new Zone("Farm", new TileCoord(4, 4), new TileCoord(5, 5));
        draft.SetGroupZones(active.Id, new[] { activeZone });
        draft.SetGroupZones(other.Id, new[] { otherZone });

        var protectedZones = draft.ProtectedZones(active.Id);

        Assert.Single(protectedZones);
        Assert.Equal(otherZone, protectedZones[0]);
    }

    [Fact]
    public void SeasonAgnosticGroup_ProjectsYearRoundChoiceWithLocationAndOutputChest()
    {
        var draft = new CropPlanDraft();
        var group = draft.AddGroup();
        group.SetLocation("Greenhouse");
        group.SetYearRoundCrop(Parsnip(), "Parsnip");
        group.SetYearRoundFertilizer("fert.basic", "Basic Fertilizer");
        group.ToggleYearRoundAutoReplant();
        group.OutputChest = new ChestRef("Greenhouse", new TileCoord(8, 8));
        draft.SetGroupZones(group.Id, new[] { new Zone("Greenhouse", new TileCoord(1, 1), new TileCoord(2, 2)) });

        var plan = draft.BuildCropPlan();

        var assignment = Assert.Single(plan.Assignments);
        Assert.Equal("Greenhouse", assignment.Zone.LocationName);
        Assert.Equal(CropAssignmentMode.SeasonAgnostic, assignment.Mode);
        Assert.Equal(group.Id, assignment.GroupId);
        Assert.Equal(new ChestRef("Greenhouse", new TileCoord(8, 8)), assignment.OutputChest);
        var choice = Assert.Single(assignment.Choices);
        Assert.Equal("seed.parsnip", choice.Crop.SeedItemId);
        Assert.Equal("fert.basic", choice.Crop.FertilizerItemId);
        Assert.True(choice.AutoReplant);
    }

    [Fact]
    public void HydrateFrom_RestoresSeasonAgnosticGroup()
    {
        var crop = Parsnip().WithFertilizer("fert.basic");
        var plan = new CropPlan(new[]
        {
            new CropZoneAssignment(
                new Zone("Greenhouse", new TileCoord(1, 1), new TileCoord(2, 2)),
                CropAssignmentMode.SeasonAgnostic,
                new[] { new SeasonCropChoice(Season.Spring, crop, autoReplant: true) },
                new ChestRef("Greenhouse", new TileCoord(8, 8)),
                "group-greenhouse"),
        });

        var draft = new CropPlanDraft();
        draft.HydrateFrom(plan);

        var group = draft.GetGroup("group-greenhouse");
        Assert.Equal("Greenhouse", group.LocationName);
        Assert.True(group.IsSeasonAgnostic);
        Assert.True(group.YearRoundSlot.HasCrop);
        Assert.Equal("seed.parsnip", group.YearRoundSlot.Crop!.SeedItemId);
        Assert.Equal("fert.basic", group.YearRoundSlot.FertilizerItemId);
        Assert.True(group.YearRoundSlot.AutoReplant);
        Assert.Equal(new ChestRef("Greenhouse", new TileCoord(8, 8)), group.OutputChest);
        Assert.Single(group.Zones);
    }

    [Fact]
    public void SetLocation_ClearsStaleZones()
    {
        var draft = new CropPlanDraft();
        var group = draft.AddGroup();
        draft.SetGroupZones(group.Id, new[] { new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1)) });

        group.SetLocation("Greenhouse");

        Assert.Empty(group.Zones);
        Assert.True(group.IsSeasonAgnostic);
    }

    [Fact]
    public void ProtectedZones_FiltersByLocation()
    {
        var draft = new CropPlanDraft();
        var active = draft.AddGroup();
        active.SetLocation("Greenhouse");
        var farmOther = draft.AddGroup();
        var greenhouseOther = draft.AddGroup();
        greenhouseOther.SetLocation("Greenhouse");
        var farmZone = new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1));
        var greenhouseZone = new Zone("Greenhouse", new TileCoord(4, 4), new TileCoord(5, 5));
        draft.SetGroupZones(farmOther.Id, new[] { farmZone });
        draft.SetGroupZones(greenhouseOther.Id, new[] { greenhouseZone });

        var protectedZones = draft.ProtectedZones(active.Id, "Greenhouse");

        var zone = Assert.Single(protectedZones);
        Assert.Equal(greenhouseZone, zone);
    }

    [Fact]
    public void DeleteGroup_RemovesOnlyThatGroup()
    {
        var draft = new CropPlanDraft();
        var first = draft.AddGroup();
        var second = draft.AddGroup();
        first.TrySetCrop(Season.Spring, Parsnip(), "Parsnip", out _);
        second.TrySetCrop(Season.Fall, Pumpkin(), "Pumpkin", out _);
        draft.SetGroupZones(first.Id, new[] { new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1)) });
        draft.SetGroupZones(second.Id, new[] { new Zone("Farm", new TileCoord(5, 5), new TileCoord(6, 6)) });

        draft.DeleteGroup(first.Id);

        Assert.Single(draft.Groups);
        Assert.Equal(second.Id, draft.Groups[0].Id);
        Assert.Single(draft.BuildCropPlan().Assignments);
    }

    [Fact]
    public void HydrateFrom_RestoresGroupsByPersistedGroupId()
    {
        var source = new CropPlanDraft();
        var first = source.AddGroup();
        var second = source.AddGroup();
        first.TrySetCrop(Season.Spring, Parsnip(), "Parsnip", out _);
        first.ToggleAutoReplant(Season.Spring);
        first.OutputChest = new ChestRef("Farm", new TileCoord(3, 4));
        second.TrySetCrop(Season.Fall, Pumpkin(), "Pumpkin", out _);
        source.SetGroupZones(first.Id, new[]
        {
            new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1)),
            new Zone("Farm", new TileCoord(2, 2), new TileCoord(3, 3)),
        });
        source.SetGroupZones(second.Id, new[] { new Zone("Farm", new TileCoord(6, 6), new TileCoord(7, 7)) });
        var plan = source.BuildCropPlan();

        var hydrated = new CropPlanDraft();
        hydrated.HydrateFrom(plan);

        Assert.Equal(2, hydrated.Groups.Count);
        var hydratedFirst = hydrated.GetGroup(first.Id);
        Assert.True(hydratedFirst.IsConfigured(Season.Spring));
        Assert.True(hydratedFirst.Slot(Season.Spring).AutoReplant);
        Assert.Equal(new ChestRef("Farm", new TileCoord(3, 4)), hydratedFirst.OutputChest);
        Assert.Equal(2, hydratedFirst.Zones.Count);
        Assert.Single(hydrated.GetGroup(second.Id).Zones);
    }

    [Fact]
    public void HydrateFrom_LegacyAssignmentsWithoutGroupId_BecomeSeparateGroups()
    {
        var crop = Parsnip();
        var plan = new CropPlan(new[]
        {
            new CropZoneAssignment(
                new Zone("Farm", new TileCoord(0, 0), new TileCoord(1, 1)),
                CropAssignmentMode.Seasonal,
                new[] { new SeasonCropChoice(Season.Spring, crop) }),
            new CropZoneAssignment(
                new Zone("Farm", new TileCoord(5, 5), new TileCoord(6, 6)),
                CropAssignmentMode.Seasonal,
                new[] { new SeasonCropChoice(Season.Spring, crop) }),
        });

        var draft = new CropPlanDraft();
        draft.HydrateFrom(plan);

        Assert.Equal(2, draft.Groups.Count);
        Assert.All(draft.Groups, group => Assert.Single(group.Zones));
    }

    [Fact]
    public void HydrateFrom_EmptyPlan_LeavesDraftEmpty()
    {
        var draft = new CropPlanDraft();
        draft.HydrateFrom(CropPlan.Empty);

        Assert.Empty(draft.Groups);
        Assert.False(draft.HasAnyAssignment);
    }
}
