using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class ScoringEngineTests
{
    [Fact]
    public void AssertAndDoneAreRewardedOnlyWhenSuccessful()
    {
        var scoring = new ScoringEngine();

        Assert.Equal(3, scoring.ScoreAction("Assert", succeeded: true, isLoop: false));
        Assert.Equal(5, scoring.ScoreAction("Done", succeeded: true, isLoop: false));
        Assert.Equal(-5, scoring.ScoreAction("Done", succeeded: false, isLoop: false));
        Assert.Equal(3, scoring.TotalScore);
        Assert.Equal(1, scoring.ErrorCount);
    }

    [Fact]
    public void LoopPenaltyCanTriggerAbortThreshold()
    {
        var scoring = new ScoringEngine { AbortThreshold = -10 };

        Assert.Equal(-15, scoring.ScoreAction("Click", succeeded: false, isLoop: true));

        Assert.True(scoring.ShouldAbort());
    }

    [Fact]
    public void GuardOutcomesApplyHeavyPenalties()
    {
        var scoring = new ScoringEngine();

        Assert.Equal(-25, scoring.ScoreAction("Click", succeeded: false, isLoop: false, outcome: "guard_force_reject:uia_tree_empty"));
        Assert.Equal(-55, scoring.ScoreAction("Click", succeeded: false, isLoop: false, outcome: "guard_abort:uia_capture_failed"));
    }
}
