using Dayswork.Core.Domain;
using Xunit;

namespace Dayswork.Tests.Orchestration;

public sealed class AnimalCollectSoundTests
{
    [Theory]
    [InlineData(WorkerTool.Shears, "scissors")]
    [InlineData(WorkerTool.MilkPail, "Milking")]
    [InlineData(WorkerTool.None, "dwop")]
    public void GetAnimalCollectSound_ReturnsExpectedCue(WorkerTool collectTool, string expectedCue)
    {
        Assert.Equal(expectedCue, AnimalCollectAudioCue.ForTool(collectTool));
    }
}
