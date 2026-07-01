using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class TriageBaselineTests
{
    [Fact]
    public void Classify_MapsEachBucket_ElseNewRegression()
    {
        var baseline = new TriageBaseline
        {
            KnownFlakes = [new BaselineEntry { TestId = "flake" }],
            DataDriftScenarios = [new BaselineEntry { TestId = "drift" }],
            PreservedBugs = [new BaselineEntry { TestId = "bug" }]
        };

        Assert.Equal("knownFlake", baseline.Classify("flake"));
        Assert.Equal("knownFlake", baseline.Classify("FLAKE")); // case-insensitive
        Assert.Equal("dataDrift", baseline.Classify("drift"));
        Assert.Equal("preservedBug", baseline.Classify("bug"));
        Assert.Equal("newRegression", baseline.Classify("something-else"));
    }

    [Fact]
    public void Compute_WithBaseline_CountsOnlyNewRegressions()
    {
        var runs = new List<RunArtifact>
        {
            new() { TestId = "known", Result = "Failed" },
            new() { TestId = "newbug", Result = "Failed" },
            new() { TestId = "green", Result = "Passed" }
        };
        var baseline = new TriageBaseline { KnownFlakes = [new BaselineEntry { TestId = "known" }] };

        var r = RunAnalytics.Compute(runs, baseline);

        Assert.True(r.BaselineApplied);
        Assert.Equal(1, r.NewRegressionCount);
        Assert.Equal("knownFlake", r.Tests.Single(t => t.TestId == "known").Classification);
        Assert.Equal("newRegression", r.Tests.Single(t => t.TestId == "newbug").Classification);
        Assert.Null(r.Tests.Single(t => t.TestId == "green").Classification); // passing -> unclassified
    }

    [Fact]
    public void Compute_WithoutBaseline_LeavesTriageUnset()
    {
        var runs = new List<RunArtifact> { new() { TestId = "x", Result = "Failed" } };

        var r = RunAnalytics.Compute(runs);

        Assert.False(r.BaselineApplied);
        Assert.Equal(0, r.NewRegressionCount);
        Assert.Null(r.Tests.Single().Classification);
    }

    [Fact]
    public void Load_MissingPath_ReturnsNull()
    {
        Assert.Null(TriageBaseline.Load(null));
        Assert.Null(TriageBaseline.Load(""));
        Assert.Null(TriageBaseline.Load(Path.Combine(Path.GetTempPath(), "no-such-baseline-" + Guid.NewGuid().ToString("N") + ".json")));
    }

    [Fact]
    public void Load_TolerantJson_WithCommentsAndTrailingCommas()
    {
        var path = Path.Combine(Path.GetTempPath(), "baseline-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """
        {
          "schemaVersion": "1.0",
          "knownFlakes": [ { "testId": "T-1", "reason": "race", "rate": 0.1 }, ],
          "unknownFutureField": 42
        }
        """);
        try
        {
            var baseline = TriageBaseline.Load(path);
            Assert.NotNull(baseline);
            Assert.Equal("knownFlake", baseline!.Classify("T-1"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
