using System;
using System.Collections.Generic;
using System.IO;
using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class WatchModeTests
{
    [Fact]
    public void ParseAcceptsWatchWithRenderUi()
    {
        var options = RunnerOptions.Parse(["--render-ui", "docs/symphony.html", "--watch"], new WorkflowConfig());

        Assert.True(options.RenderUiOnly);
        Assert.True(options.Watch);
    }

    [Fact]
    public void ParseRejectsWatchWithoutRenderUi()
    {
        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(["--watch"], new WorkflowConfig()));
    }

    [Fact]
    public void RenderHtmlEmitsMetaRefreshWhenAutoRefreshSet()
    {
        var html = AgentLoopWorkbenchGenerator.RenderHtml(
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "docs", "symphony.html"),
            new List<string>(),
            new List<TestDefinition>(),
            new List<RunArtifact>(),
            autoRefreshSeconds: 3);

        Assert.Contains("http-equiv=\"refresh\"", html);
        Assert.Contains("content=\"3\"", html);
    }

    [Fact]
    public void RenderHtmlOmitsMetaRefreshByDefault()
    {
        var html = AgentLoopWorkbenchGenerator.RenderHtml(
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "docs", "symphony.html"),
            new List<string>(),
            new List<TestDefinition>(),
            new List<RunArtifact>());

        Assert.DoesNotContain("http-equiv=\"refresh\"", html);
    }
}
