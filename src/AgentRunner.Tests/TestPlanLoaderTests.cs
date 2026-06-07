using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class TestPlanLoaderTests
{
    [Fact]
    public void DiscoverPlanPathsFindsYamlFilesRecursively()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-plans-" + Guid.NewGuid().ToString("N"));
        var testsDir = Path.Combine(tempDir, "tests");
        var examplesDir = Path.Combine(testsDir, "examples", "winforms");
        Directory.CreateDirectory(examplesDir);
        File.WriteAllText(Path.Combine(testsDir, "smoke.yaml"), "suite: smoke");
        File.WriteAllText(Path.Combine(examplesDir, "login.yml"), "suite: examples-winforms");

        var paths = TestPlanLoader.DiscoverPlanPaths(tempDir);

        Assert.Equal(2, paths.Count);
        Assert.Contains(paths, path => path.EndsWith(Path.Combine("tests", "smoke.yaml"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(paths, path => path.EndsWith(Path.Combine("tests", "examples", "winforms", "login.yml"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseLoadsVersionedTestDefinitions()
    {
        var plan = TestPlanLoader.Parse("""
suite: smoke

tests:
  LOGIN-001:
    title: "Login succeeds"
    priority: "P0"
    framework: "winforms"
    target_window: "Sample Login App (.NET 8)"
    source_issue: "GH-123"
    source_pr: "GH-456"
    authoring_agent: "codex"
    risk: "medium"
    ci_profile: "github-windows"
    category: "Scenario"
    goal: "Log in with valid credentials."
    success_condition: "Login successful"
    max_steps: 8
    allowed_actions: ["EnterText", "Click", "Assert", "Done"]
    tags: ["smoke", "login"]
    blocked_if:
      - "Target window not found"
    existing_tests:
      - "MyApp.Tests.Auth.LoginSucceeds"
""");

        var test = Assert.Single(plan.Tests);
        Assert.Equal("smoke", plan.Suite);
        Assert.Equal("LOGIN-001", test.Id);
        Assert.Equal("Login succeeds", test.Title);
        Assert.Equal("P0", test.Priority);
        Assert.Equal("winforms", test.Framework);
        Assert.Equal("Sample Login App (.NET 8)", test.TargetWindow);
        Assert.Equal("GH-123", test.SourceIssue);
        Assert.Equal("GH-456", test.SourcePr);
        Assert.Equal("codex", test.AuthoringAgent);
        Assert.Equal("medium", test.Risk);
        Assert.Equal("github-windows", test.CiProfile);
        Assert.Equal(TestCategory.Scenario, test.Category);
        Assert.Equal("Log in with valid credentials.", test.Goal);
        Assert.Equal("Login successful", test.SuccessCondition);
        Assert.Equal(8, test.MaxSteps);
        Assert.Equal(["EnterText", "Click", "Assert", "Done"], test.AllowedActions);
        Assert.Equal(["smoke", "login"], test.Tags);
        Assert.Equal(["Target window not found"], test.BlockedIf);
        Assert.Equal(["MyApp.Tests.Auth.LoginSucceeds"], test.ExistingTests);
    }

    [Fact]
    public void ToAgentGoalCarriesAllowedActions()
    {
        var definition = new TestDefinition
        {
            Id = "LOGIN-001",
            Goal = "Log in.",
            SuccessCondition = "Login successful",
            MaxSteps = 8,
            Category = TestCategory.Scenario,
            AllowedActions = ["EnterText", "Click", "Done"]
        };

        var goal = definition.ToAgentGoal();

        Assert.Equal("LOGIN-001", goal.Identifier);
        Assert.Equal("Log in.", goal.Description);
        Assert.Equal("Login successful", goal.SuccessCondition);
        Assert.Equal(8, goal.MaxSteps);
        Assert.Equal(TestCategory.Scenario, goal.Category);
        Assert.Equal(["EnterText", "Click", "Done"], goal.AllowedActions);
    }

    [Fact]
    public void ParseRejectsInvalidMaxSteps()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TestPlanLoader.Parse("""
suite: smoke

tests:
  LOGIN-001:
    goal: "Log in."
    max_steps: nope
"""));

        Assert.Contains("max_steps", ex.Message);
    }

    [Fact]
    public void ParseReadsSchemaVersionFromYaml()
    {
        var plan = TestPlanLoader.Parse("""
schema_version: "1.2.3"
suite: smoke

tests:
  LOGIN-001:
    goal: "Log in."
""");

        Assert.Equal("1.2.3", plan.SchemaVersion);
        Assert.Equal("smoke", plan.Suite);
    }

    [Fact]
    public void ParseIgnoresUnknownFieldsAndFillsDefaultsInYaml()
    {
        var plan = TestPlanLoader.Parse("""
schema_version: "1.0"
suite: smoke
unknown_root_property: "some_val"

tests:
  LOGIN-001:
    goal: "Log in."
    unknown_test_property: "some_val"
    unknown_test_list:
      - item1
      - item2
""");

        var test = Assert.Single(plan.Tests);
        Assert.Equal("LOGIN-001", test.Id);
        Assert.Equal("Log in.", test.Goal);
        // Defaults filled
        Assert.Equal(30, test.MaxSteps);
        Assert.Equal(TestCategory.Scenario, test.Category);
        Assert.Empty(test.AllowedActions);
    }

    [Fact]
    public void RunArtifactLoaderIgnoresUnknownFieldsInJson()
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentloop-loader-unknown-" + Guid.NewGuid().ToString("N"));
        var runDir = Path.Combine(dir, "runs", "r2");
        Directory.CreateDirectory(runDir);
        File.WriteAllText(Path.Combine(runDir, "report.json"), """
{
  "version": "1.0",
  "runId": "r2",
  "evidenceLevel": "Standard",
  "testId": "LOGIN-001",
  "result": "Succeeded",
  "finalScore": 100,
  "startedAt": "2026-05-05T22:00:00Z",
  "unknown_property_at_root": "hello",
  "steps": [
    {
      "stepNumber": 1,
      "actionType": "Click",
      "unknown_property_in_step": 123
    }
  ]
}
""");

        try
        {
            var runs = RunArtifactLoader.LoadFromDirectory(Path.Combine(dir, "runs"));

            Assert.Single(runs);
            Assert.Equal("r2", runs[0].RunId);
            Assert.Equal("1.0", runs[0].Version);
            Assert.Equal("Succeeded", runs[0].Result);
            var step = Assert.Single(runs[0].Steps);
            Assert.Equal(1, step.StepNumber);
            Assert.Equal("Click", step.ActionType);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
