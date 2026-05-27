namespace Dayswork.Core.Domain;

public static class AnimalCollectAudioCue
{
    public static string ForTool(WorkerTool collectTool) => collectTool switch
    {
        WorkerTool.Shears => "scissors",
        WorkerTool.MilkPail => "Milking",
        _ => "dwop",
    };
}
