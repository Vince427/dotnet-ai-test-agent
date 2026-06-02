using System;
using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class JUnitCliOptionsTests
{
    [Fact]
    public void ParsesToJunitWithExplicitPath()
    {
        var options = RunnerOptions.Parse(["--to-junit", "artifacts/ci/results.xml"], new WorkflowConfig());

        Assert.True(options.ToJUnitOnly);
        Assert.NotNull(options.JUnitOutputPath);
        Assert.EndsWith("results.xml", options.JUnitOutputPath!.Replace('\\', '/'));
    }

    [Fact]
    public void ParsesToJunitWithDefaultPath()
    {
        var options = RunnerOptions.Parse(["--to-junit"], new WorkflowConfig());

        Assert.True(options.ToJUnitOnly);
        Assert.NotNull(options.JUnitOutputPath);
        Assert.EndsWith("junit-results.xml", options.JUnitOutputPath!.Replace('\\', '/'));
    }

    [Fact]
    public void RejectsToJunitCombinedWithAnotherMode()
    {
        Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--to-junit", "x.xml", "--validate-plan"], new WorkflowConfig()));
    }
}
