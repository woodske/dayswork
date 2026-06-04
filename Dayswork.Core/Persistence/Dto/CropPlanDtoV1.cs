namespace Dayswork.Core.Persistence.Dto;

public sealed class CropPlanDtoV1
{
    public List<CropZoneAssignmentDtoV1> Assignments { get; set; } = new();
}
