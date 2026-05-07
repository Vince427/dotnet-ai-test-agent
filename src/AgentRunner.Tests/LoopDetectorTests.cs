using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class LoopDetectorTests
{
    [Fact]
    public void DifferentActionsDoNotTriggerLoop()
    {
        var detector = new LoopDetector();

        Assert.False(detector.RecordAndCheck("Click:username"));
        Assert.False(detector.RecordAndCheck("EnterText:username"));
        Assert.False(detector.RecordAndCheck("Click:password"));
        Assert.False(detector.RecordAndCheck("EnterText:password"));
        Assert.False(detector.RecordAndCheck("Click:login"));
    }

    [Fact]
    public void RepeatedActionTriggersLoop()
    {
        var detector = new LoopDetector();

        Assert.False(detector.RecordAndCheck("Click:login"));
        Assert.False(detector.RecordAndCheck("Wait:"));
        Assert.False(detector.RecordAndCheck("Click:login"));
        Assert.True(detector.RecordAndCheck("Click:login"));
    }
}
