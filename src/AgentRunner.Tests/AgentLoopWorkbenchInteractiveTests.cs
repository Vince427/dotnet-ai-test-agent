using System.Collections.Generic;
using System.IO;
using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class AgentLoopWorkbenchInteractiveTests
{
    private static string Render(IReadOnlyList<RunArtifact> runs) =>
        AgentLoopWorkbenchGenerator.RenderHtml(
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "docs", "agentloop.html"),
            new List<string>(),
            new List<TestDefinition>(),
            runs);

    [Fact]
    public void IncludesInteractiveControlsAndDataIsland()
    {
        var html = Render(new List<RunArtifact>());

        Assert.Contains("id=\"run-search\"", html);
        Assert.Contains("id=\"run-filter\"", html);
        Assert.Contains("id=\"agentloop-data\"", html);
    }

    [Fact]
    public void MarksRunRowsWithMetadataForClientSideDrilldown()
    {
        var html = Render(new List<RunArtifact> { new() { RunId = "r1", Result = "Passed" } });

        Assert.Contains("class=\"run-row\"", html);
        Assert.Contains("data-runid=\"r1\"", html);
        Assert.Contains("data-result=\"Passed\"", html);
    }

    [Fact]
    public void EmbedsStepDetailInDataIsland()
    {
        var runs = new List<RunArtifact>
        {
            new()
            {
                RunId = "r1",
                Result = "Failed",
                Steps = new List<RunStep>
                {
                    new() { StepNumber = 1, ActionType = "Click", ActionTarget = "btnMissing", FailureCode = "action_target_not_found" }
                }
            }
        };

        var html = Render(runs);

        Assert.Contains("action_target_not_found", html);
        Assert.Contains("btnMissing", html);
    }

    [Fact]
    public void EmbedsTraceIdAndTemplateInDataIsland()
    {
        var html = Render(new List<RunArtifact>
        {
            new() { RunId = "r1", Result = "Succeeded", TraceId = "0af7651916cd43dd8448eb211c80319c" }
        });

        // OBS-1b: the run's trace id and the (baked) trace-UI template field both flow
        // into the data island, so the client can render a results -> live-trace link.
        Assert.Contains("0af7651916cd43dd8448eb211c80319c", html);
        Assert.Contains("traceUiTemplate", html);
    }

    [Fact]
    public void ShowsAlertBannerWhenFailuresExist()
    {
        var html = Render(new List<RunArtifact>
        {
            new() { RunId = "r1", Result = "Failed" },
            new() { RunId = "r2", Result = "Aborted" }
        });

        Assert.Contains("data-alert=\"1\"", html);
    }

    [Fact]
    public void OmitsAlertBannerWhenAllPass()
    {
        var html = Render(new List<RunArtifact> { new() { RunId = "r1", Result = "Passed" } });

        Assert.DoesNotContain("data-alert", html);
    }

    [Fact]
    public void RendersPassRateBarWhenRunsExist()
    {
        var html = Render(new List<RunArtifact> { new() { RunId = "r1", Result = "Passed" } });

        Assert.Contains("class=\"bar\"", html);
        Assert.Contains("Pass rate", html);
    }

    [Fact]
    public void EscapesScriptInjectionInsideDataIsland()
    {
        var runs = new List<RunArtifact>
        {
            new()
            {
                RunId = "r1",
                Result = "Failed",
                Steps = new List<RunStep>
                {
                    new() { StepNumber = 1, FailureMessage = "<script>alert(1)</script>" }
                }
            }
        };

        var html = Render(runs);

        // The raw injection must never appear unescaped in the document.
        Assert.DoesNotContain("<script>alert(1)", html);
    }

    [Fact]
    public void LoadsRunsWhenEvidenceLevelIsSerializedAsString()
    {
        // Regression: ArtifactWriter writes enums as strings (JsonStringEnumConverter).
        // The workbench reader must accept that, otherwise every real run is dropped.
        var tempDir = Path.Combine(Path.GetTempPath(), "agentloop-wb-" + System.Guid.NewGuid().ToString("N"));
        var runDir = Path.Combine(tempDir, "runs", "r1");
        Directory.CreateDirectory(runDir);
        File.WriteAllText(Path.Combine(runDir, "report.json"), """
{
  "runId": "r1",
  "evidenceLevel": "Full",
  "testId": "GUARD-CRASH-001",
  "result": "Aborted",
  "finalScore": -55,
  "startedAt": "2026-05-05T22:00:00Z"
}
""");

        var result = AgentLoopWorkbenchGenerator.Generate(new AgentLoopWorkbenchOptions
        {
            RepoRoot = tempDir,
            OutputPath = Path.Combine(tempDir, "docs", "agentloop.html"),
            RunsRoot = Path.Combine(tempDir, "runs")
        });

        Assert.Equal(1, result.RunCount);
        Assert.Contains("data-alert", File.ReadAllText(result.OutputPath));
    }
}
