using System;
using System.Collections.Generic;
using System.Linq;
using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class RunAnalyticsTests
{
    private static RunArtifact Run(string testId, string result, DateTime? started = null, DateTime? ended = null, List<RunStep>? steps = null)
    {
        return new RunArtifact
        {
            TestId = testId,
            Result = result,
            StartedAt = started ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndedAt = ended,
            Steps = steps ?? []
        };
    }

    [Fact]
    public void EmptyHistoryIsNullSafeAndZeroed()
    {
        var result = RunAnalytics.Compute(new List<RunArtifact>());

        Assert.Equal(0, result.TotalRuns);
        Assert.Equal(0, result.FlakyTestCount);
        Assert.Equal(0, result.SelectorDriftCount);
        Assert.Equal(0, result.RunsWithDuration);
        Assert.Equal(0d, result.AverageRunDurationSeconds);
        Assert.Equal(0d, result.MaxRunDurationSeconds);
        Assert.Equal(0d, result.AverageStepCount);
        Assert.Empty(result.Tests);
        Assert.Empty(result.MostFailingTests);
        Assert.Empty(result.SelectorDrift);
    }

    [Fact]
    public void NullListIsTreatedAsEmpty()
    {
        var result = RunAnalytics.Compute(null!);
        Assert.Equal(0, result.TotalRuns);
    }

    [Fact]
    public void CountsRunsAndPerTestPassFail()
    {
        var runs = new List<RunArtifact>
        {
            Run("A", "Passed"),
            Run("A", "Succeeded"),
            Run("B", "Failed"),
        };

        var result = RunAnalytics.Compute(runs);

        Assert.Equal(3, result.TotalRuns);
        var a = result.Tests.Single(t => t.TestId == "A");
        Assert.Equal(2, a.Runs);
        Assert.Equal(2, a.Passed);
        Assert.Equal(0, a.Failed);
        Assert.False(a.Flaky);

        var b = result.Tests.Single(t => t.TestId == "B");
        Assert.Equal(1, b.Failed);
        Assert.Equal(0, b.Passed);
    }

    [Fact]
    public void DetectsFlakyWhenSameTestHasPassingAndFailingRuns()
    {
        var runs = new List<RunArtifact>
        {
            Run("FLAKY", "Passed"),
            Run("FLAKY", "Failed"),
            Run("FLAKY", "Succeeded"),
            Run("STABLE", "Passed"),
            Run("STABLE", "Succeeded"),
            Run("BROKEN", "Failed"),
            Run("BROKEN", "Aborted"),
        };

        var result = RunAnalytics.Compute(runs);

        Assert.True(result.Tests.Single(t => t.TestId == "FLAKY").Flaky);
        Assert.False(result.Tests.Single(t => t.TestId == "STABLE").Flaky);
        Assert.False(result.Tests.Single(t => t.TestId == "BROKEN").Flaky);
        Assert.Equal(1, result.FlakyTestCount);
    }

    [Fact]
    public void TreatsLoopDetectedAndAbortedAsNonPassing()
    {
        var runs = new List<RunArtifact>
        {
            Run("X", "LoopDetected"),
            Run("X", "Aborted"),
            Run("X", "Running"),
        };

        var result = RunAnalytics.Compute(runs);
        var x = result.Tests.Single(t => t.TestId == "X");
        Assert.Equal(0, x.Passed);
        Assert.Equal(3, x.Failed);
        Assert.False(x.Flaky);
    }

    [Fact]
    public void RunsWithoutTestIdFoldIntoUnknownBucket()
    {
        var runs = new List<RunArtifact>
        {
            Run(null!, "Passed"),
            Run("  ", "Failed"),
        };

        var result = RunAnalytics.Compute(runs);
        var unknown = result.Tests.Single(t => t.TestId == "(unknown)");
        Assert.Equal(2, unknown.Runs);
        Assert.True(unknown.Flaky);
    }

    [Fact]
    public void MostFailingTestsAreOrderedByFailuresDescending()
    {
        var runs = new List<RunArtifact>
        {
            Run("LOW", "Passed"),
            Run("LOW", "Failed"),
            Run("HIGH", "Failed"),
            Run("HIGH", "Failed"),
            Run("HIGH", "Failed"),
            Run("CLEAN", "Passed"),
        };

        var result = RunAnalytics.Compute(runs);

        Assert.Equal(2, result.MostFailingTests.Count); // CLEAN has zero failures, excluded
        Assert.Equal("HIGH", result.MostFailingTests[0].TestId);
        Assert.Equal("LOW", result.MostFailingTests[1].TestId);
        Assert.DoesNotContain(result.MostFailingTests, t => t.TestId == "CLEAN");
    }

    [Fact]
    public void GroupsSelectorDriftByOldToNewTargetAndCounts()
    {
        RunStep StepWithHeal(string oldT, string newT, int confidence) => new()
        {
            HealingSuggestion = new HealingSuggestion
            {
                OldTarget = oldT,
                NewTarget = newT,
                Confidence = confidence
            }
        };

        var runs = new List<RunArtifact>
        {
            Run("A", "Failed", steps: new List<RunStep>
            {
                StepWithHeal("btnLogin", "btnSignIn", 80),
                new RunStep(), // no suggestion
            }),
            Run("B", "Failed", steps: new List<RunStep>
            {
                StepWithHeal("btnLogin", "btnSignIn", 90),
                StepWithHeal("txtUser", "txtUsername", 70),
            }),
        };

        var result = RunAnalytics.Compute(runs);

        Assert.Equal(3, result.SelectorDriftCount); // 3 steps carried a suggestion
        Assert.Equal(2, result.SelectorDrift.Count); // grouped into 2 old->new pairs

        var login = result.SelectorDrift.Single(d => d.OldTarget == "btnLogin");
        Assert.Equal("btnSignIn", login.NewTarget);
        Assert.Equal(2, login.Count);
        Assert.Equal(90, login.MaxConfidence); // max across the group

        // Most frequent drift first.
        Assert.Equal("btnLogin", result.SelectorDrift[0].OldTarget);
    }

    [Fact]
    public void ComputesDurationAndStepStatsIgnoringMissingEndedAt()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var runs = new List<RunArtifact>
        {
            Run("A", "Passed", started: start, ended: start.AddSeconds(10), steps: new List<RunStep> { new(), new() }),
            Run("A", "Failed", started: start, ended: start.AddSeconds(30), steps: new List<RunStep> { new() }),
            Run("B", "Passed", started: start, ended: null, steps: new List<RunStep> { new(), new(), new() }), // no EndedAt
        };

        var result = RunAnalytics.Compute(runs);

        // Only 2 of 3 runs have a usable duration.
        Assert.Equal(2, result.RunsWithDuration);
        Assert.Equal(20d, result.AverageRunDurationSeconds); // (10 + 30) / 2
        Assert.Equal(30d, result.MaxRunDurationSeconds);

        // Step count averages across ALL runs (2 + 1 + 3 = 6 over 3 runs).
        Assert.Equal(6, result.TotalSteps);
        Assert.Equal(2d, result.AverageStepCount);
    }

    [Fact]
    public void IgnoresEndedBeforeStarted()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var runs = new List<RunArtifact>
        {
            Run("A", "Passed", started: start, ended: start.AddSeconds(-5)), // clock skew / bad data
        };

        var result = RunAnalytics.Compute(runs);
        Assert.Equal(0, result.RunsWithDuration);
        Assert.Equal(0d, result.AverageRunDurationSeconds);
    }

    [Fact]
    public void IsDeterministicAcrossInputOrdering()
    {
        var runs = new List<RunArtifact>
        {
            Run("B", "Failed"),
            Run("A", "Passed"),
            Run("A", "Failed"),
        };

        var first = RunAnalytics.Compute(runs);
        var reordered = new List<RunArtifact> { runs[2], runs[0], runs[1] };
        var second = RunAnalytics.Compute(reordered);

        Assert.Equal(
            first.Tests.Select(t => t.TestId),
            second.Tests.Select(t => t.TestId));
        Assert.Equal(first.FlakyTestCount, second.FlakyTestCount);
    }
}
