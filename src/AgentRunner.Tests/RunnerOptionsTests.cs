using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class RunnerOptionsTests
{
    [Fact]
    public void ParseUsesWorkflowGoalThenAppliesCliOverrides()
    {
        var config = new WorkflowConfig();
        config.Goals["default"] = new AgentGoal
        {
            Description = "Default login",
            SuccessCondition = "Login successful",
            MaxSteps = 30,
            Identifier = "login",
            Category = TestCategory.Scenario
        };
        config.Goals["audit"] = new AgentGoal
        {
            Description = "Audit controls",
            MaxSteps = 20,
            Identifier = "a11y",
            Category = TestCategory.Audit
        };

        var options = RunnerOptions.Parse(
            [
                "--goal-name", "audit",
                "--window", "Custom Window",
                "--goal", "Override goal",
                "--success", "",
                "--goal-id", "override",
                "--max-steps", "7"
            ],
            config);

        Assert.Equal("Custom Window", options.TargetWindow);
        Assert.Equal("audit", options.GoalName);
        Assert.Equal("Override goal", options.Goal.Description);
        Assert.Null(options.Goal.SuccessCondition);
        Assert.Equal("override", options.Goal.Identifier);
        Assert.Equal(7, options.Goal.MaxSteps);
        Assert.Equal(TestCategory.Audit, options.Goal.Category);
    }

    [Fact]
    public void ParseRejectsInvalidMaxSteps()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--max-steps", "nope"], new WorkflowConfig()));

        Assert.Contains("--max-steps", ex.Message);
    }

    [Fact]
    public void ParseSupportsEvidenceLevel()
    {
        var options = RunnerOptions.Parse(["--evidence-level", "full"], new WorkflowConfig());

        Assert.Equal(EvidenceLevel.Full, options.EvidenceLevel);
    }

    [Fact]
    public void ParseSupportsVisionFlag()
    {
        Assert.True(RunnerOptions.Parse(["--vision"], new WorkflowConfig()).Vision);
        Assert.False(RunnerOptions.Parse([], new WorkflowConfig()).Vision); // off by default
    }

    [Fact]
    public void ParseShowPrompt_DoesNotRunSinglePlanSelection()
    {
        // --show-prompt resolves its own test across plans, so a lone --test-id must NOT throw
        // the single-plan "test id not found" selection error here.
        var o = RunnerOptions.Parse(["--show-prompt", "--test-id", "ANY-ID"], new WorkflowConfig());
        Assert.True(o.ShowPromptOnly);
        Assert.Equal("ANY-ID", o.TestId);
    }

    [Fact]
    public void ParseRejectsInvalidEvidenceLevel()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--evidence-level", "verbose"], new WorkflowConfig()));

        Assert.Contains("--evidence-level", ex.Message);
    }

    [Fact]
    public void ParseSupportsJsonFormatForManualCommands()
    {
        var options = RunnerOptions.Parse(["--list-tests", "--format", "json"], new WorkflowConfig());

        Assert.True(options.ListTestsOnly);
        Assert.Equal(CommandOutputFormat.Json, options.OutputFormat);
    }

    [Fact]
    public void ParseRejectsJsonFormatForRuntimeCommands()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--format", "json"], new WorkflowConfig()));

        Assert.Contains("--format json", ex.Message);
    }

    [Fact]
    public void ParseRejectsInvalidOutputFormat()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--list-tests", "--format", "xml"], new WorkflowConfig()));

        Assert.Contains("--format", ex.Message);
    }

    [Fact]
    public void ParseSupportsRenderUiOnly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-render-" + Guid.NewGuid().ToString("N"));
        var config = new WorkflowConfig { WorkflowDirectory = tempDir };

        var options = RunnerOptions.Parse(["--render-ui", "docs/agentloop.html"], config);

        Assert.True(options.RenderUiOnly);
        Assert.Equal(Path.Combine(tempDir, "docs", "agentloop.html"), options.UiOutputPath);
    }

    [Fact]
    public void ParseSupportsGuardDemoManualModeWithDefaultRunsRoot()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-guard-demos-" + Guid.NewGuid().ToString("N"));
        var config = new WorkflowConfig
        {
            WorkflowDirectory = tempDir,
            WorkspaceRoot = Path.Combine(tempDir, "runs")
        };

        var options = RunnerOptions.Parse(["--write-guard-demos"], config);

        Assert.True(options.WriteGuardDemosOnly);
        Assert.Equal(Path.Combine(tempDir, "runs"), options.GuardDemoOutputRoot);
    }

    [Fact]
    public void ParseSupportsGuardDemoManualModeWithCustomOutput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-guard-demos-" + Guid.NewGuid().ToString("N"));
        var config = new WorkflowConfig
        {
            WorkflowDirectory = tempDir,
            WorkspaceRoot = Path.Combine(tempDir, "runs")
        };

        var options = RunnerOptions.Parse(["--write-guard-demos", "artifacts/guard-demos"], config);

        Assert.True(options.WriteGuardDemosOnly);
        Assert.Equal(Path.Combine(tempDir, "artifacts", "guard-demos"), options.GuardDemoOutputRoot);
    }

    [Fact]
    public void ParseRejectsGuardDemoModeCombinedWithAnotherManualMode()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--write-guard-demos", "--list-tests"], new WorkflowConfig()));

        Assert.Contains("Use only one", ex.Message);
    }

    [Fact]
    public void ParseSupportsManualValidationWithoutSelectingRuntimeTest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-manual-" + Guid.NewGuid().ToString("N"));
        var config = new WorkflowConfig { WorkflowDirectory = tempDir };

        var options = RunnerOptions.Parse(["--validate-plan", "tests/smoke.yaml"], config);

        Assert.True(options.ValidatePlanOnly);
        Assert.False(options.ListTestsOnly);
        Assert.Null(options.Test);
        Assert.Equal(Path.Combine(tempDir, "tests", "smoke.yaml"), options.PlanPath);
    }

    [Fact]
    public void ParseSupportsManualListingWithoutPlan()
    {
        var options = RunnerOptions.Parse(["--list-tests"], new WorkflowConfig());

        Assert.True(options.ListTestsOnly);
        Assert.False(options.ValidatePlanOnly);
        Assert.Null(options.Test);
        Assert.Null(options.PlanPath);
    }

    [Fact]
    public void ParseRejectsMultipleManualModes()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--list-tests", "--validate-plan"], new WorkflowConfig()));

        Assert.Contains("Use only one", ex.Message);
    }

    [Fact]
    public void ParseSupportsRecordSessionWithWindowAndSeconds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-record-" + Guid.NewGuid().ToString("N"));
        var config = new WorkflowConfig { WorkflowDirectory = tempDir };

        var options = RunnerOptions.Parse(
            ["--record", "--window", "My App", "--seconds", "30", "--out", "runs/session.json"],
            config);

        Assert.True(options.RecordSessionOnly);
        Assert.Equal("My App", options.TargetWindow);
        Assert.Equal(30, options.RecordSeconds);
        Assert.Equal(Path.Combine(tempDir, "runs", "session.json"), options.RecordOutputPath);
    }

    [Fact]
    public void ParseRecordSessionDefaultsSecondsAndStdout()
    {
        var options = RunnerOptions.Parse(["--record", "--window", "My App"], new WorkflowConfig());

        Assert.True(options.RecordSessionOnly);
        Assert.Equal(RunnerOptions.DefaultRecordSeconds, options.RecordSeconds);
        Assert.Null(options.RecordOutputPath); // stdout when no --out
    }

    [Fact]
    public void ParseRejectsSecondsWithoutRecord()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--list-tests", "--seconds", "10"], new WorkflowConfig()));

        Assert.Contains("--seconds", ex.Message);
    }

    [Fact]
    public void ParseRejectsRecordCombinedWithAnotherManualMode()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--record", "--list-tests"], new WorkflowConfig()));

        Assert.Contains("Use only one", ex.Message);
    }

    [Fact]
    public void ParseOutFlagFeedsTheActiveRecordingMode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sessionPath = Path.Combine(tempDir, "in.json");
        File.WriteAllText(sessionPath, "{\"window\":\"x\",\"actions\":[]}");
        var config = new WorkflowConfig { WorkflowDirectory = tempDir };

        // --out binds to --compose-recording when that's the active mode (and not to --record).
        var compose = RunnerOptions.Parse(
            ["--compose-recording", sessionPath, "--out", "draft.yaml"], config);
        Assert.True(compose.ComposeRecordingOnly);
        Assert.Equal(Path.Combine(tempDir, "draft.yaml"), compose.RecordingOutputPath);
        Assert.Null(compose.RecordOutputPath);
    }

    [Fact]
    public void ParseRejectsOutWithoutRecordingMode()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--list-tests", "--out", "x.json"], new WorkflowConfig()));

        Assert.Contains("--out", ex.Message);
    }

    [Fact]
    public void ParseSupportsAnalyticsAndJsonFormat()
    {
        var text = RunnerOptions.Parse(["--analytics"], new WorkflowConfig());
        Assert.True(text.AnalyticsOnly);
        Assert.Equal(CommandOutputFormat.Text, text.OutputFormat);

        var json = RunnerOptions.Parse(["--analytics", "--format", "json"], new WorkflowConfig());
        Assert.True(json.AnalyticsOnly);
        Assert.Equal(CommandOutputFormat.Json, json.OutputFormat);
    }

    [Fact]
    public void ParseRejectsAnalyticsWithAnotherMode()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            RunnerOptions.Parse(["--analytics", "--list-tests"], new WorkflowConfig()));

        Assert.Contains("only one of", ex.Message);
    }

    [Fact]
    public void ParseLoadsSelectedTestFromPlan()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-plan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var planPath = Path.Combine(tempDir, "smoke.yaml");
        File.WriteAllText(planPath, """
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
    allowed_actions: ["EnterText", "Click", "Assert", "Done"]
""");

        var config = new WorkflowConfig { WorkflowDirectory = tempDir };

        var options = RunnerOptions.Parse(["--plan", planPath, "--test-id", "LOGIN-001"], config);

        Assert.Equal(planPath, options.PlanPath);
        Assert.Equal("smoke", options.Suite);
        Assert.Equal("LOGIN-001", options.TestId);
        Assert.NotNull(options.Test);
        Assert.Equal("Sample Login App (.NET 8)", options.TargetWindow);
        Assert.Equal("LOGIN-001", options.Goal.Identifier);
        Assert.Equal("Log in with valid credentials.", options.Goal.Description);
        Assert.Equal(8, options.Goal.MaxSteps);
        Assert.Equal(["EnterText", "Click", "Assert", "Done"], options.Goal.AllowedActions);
    }
}
