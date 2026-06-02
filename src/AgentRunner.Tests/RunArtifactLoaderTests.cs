using System;
using System.IO;
using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class RunArtifactLoaderTests
{
    [Fact]
    public void LoadsRunWithStringEnumEvidenceLevel()
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentloop-loader-" + Guid.NewGuid().ToString("N"));
        var runDir = Path.Combine(dir, "runs", "r1");
        Directory.CreateDirectory(runDir);
        File.WriteAllText(Path.Combine(runDir, "report.json"), """
{
  "runId": "r1",
  "evidenceLevel": "Full",
  "testId": "LOGIN-001",
  "result": "Aborted",
  "finalScore": -55,
  "startedAt": "2026-05-05T22:00:00Z"
}
""");

        var runs = RunArtifactLoader.LoadFromDirectory(Path.Combine(dir, "runs"));

        Assert.Single(runs);
        Assert.Equal("r1", runs[0].RunId);
        Assert.Equal("Aborted", runs[0].Result);
        Assert.Equal(EvidenceLevel.Full, runs[0].EvidenceLevel);
    }

    [Fact]
    public void ReturnsEmptyWhenDirectoryMissing()
    {
        var runs = RunArtifactLoader.LoadFromDirectory(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N")));

        Assert.Empty(runs);
    }

    [Fact]
    public void SkipsBrokenReportsWithoutThrowing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentloop-loader-" + Guid.NewGuid().ToString("N"));
        var good = Path.Combine(dir, "runs", "good");
        var bad = Path.Combine(dir, "runs", "bad");
        Directory.CreateDirectory(good);
        Directory.CreateDirectory(bad);
        File.WriteAllText(Path.Combine(good, "report.json"), """{ "runId": "good", "result": "Passed" }""");
        File.WriteAllText(Path.Combine(bad, "report.json"), "{ not valid json");

        var runs = RunArtifactLoader.LoadFromDirectory(Path.Combine(dir, "runs"));

        Assert.Single(runs);
        Assert.Equal("good", runs[0].RunId);
    }
}
