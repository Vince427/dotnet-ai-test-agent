using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class AgentLoopWorkbenchGeneratorTests
{
    [Fact]
    public void RenderHtmlIncludesBacklogAndRunSummary()
    {
        var tests = new List<TestDefinition>
        {
            new()
            {
                Id = "LOGIN-001",
                Title = "Login succeeds",
                Priority = "P0",
                Framework = "winforms",
                Goal = "Log in with valid credentials.",
                SuccessCondition = "Login successful",
                MaxSteps = 8,
                AllowedActions = ["EnterText", "Click", "Done"]
            }
        };
        var runs = new List<RunArtifact>
        {
            new()
            {
                RunId = "abc12345",
                TestId = "LOGIN-001",
                Result = "Passed",
                FinalScore = 12,
                TargetWindow = "Sample Login App (.NET 8)"
            }
        };

        var html = AgentLoopWorkbenchGenerator.RenderHtml(
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "docs", "symphony.html"),
            [Path.Combine(Directory.GetCurrentDirectory(), "tests", "smoke.yaml")],
            tests,
            runs);

        Assert.Contains("AgentLoop Workbench", html);
        Assert.Contains("LOGIN-001", html);
        Assert.Contains("Login succeeds", html);
        Assert.Contains("Passed", html);
        Assert.Contains("EnterText", html);
    }

    [Fact]
    public void GenerateWritesStaticHtmlFromPlansAndRuns()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-ui-" + Guid.NewGuid().ToString("N"));
        var testsDir = Path.Combine(tempDir, "tests");
        var runDir = Path.Combine(tempDir, "runs", "abc12345");
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(runDir);

        File.WriteAllText(Path.Combine(testsDir, "smoke.yaml"), """
suite: smoke

tests:
  LOGIN-001:
    title: "Login succeeds"
    priority: "P0"
    framework: "winforms"
    target_window: "Sample Login App (.NET 8)"
    category: "Scenario"
    goal: "Log in with valid credentials."
    success_condition: "Login successful"
    max_steps: 8
    allowed_actions: ["EnterText", "Click", "Done"]
""");
        File.WriteAllText(Path.Combine(runDir, "report.json"), """
{
  "runId": "abc12345",
  "testId": "LOGIN-001",
  "result": "Passed",
  "finalScore": 12,
  "targetWindow": "Sample Login App (.NET 8)",
  "startedAt": "2026-05-05T22:00:00Z"
}
""");

        var outputPath = Path.Combine(tempDir, "docs", "symphony.html");
        var result = AgentLoopWorkbenchGenerator.Generate(new AgentLoopWorkbenchOptions
        {
            RepoRoot = tempDir,
            OutputPath = outputPath,
            RunsRoot = Path.Combine(tempDir, "runs")
        });

        Assert.Equal(1, result.TestCount);
        Assert.Equal(1, result.RunCount);
        Assert.True(File.Exists(outputPath));
        Assert.Contains("LOGIN-001", File.ReadAllText(outputPath));
    }
}
