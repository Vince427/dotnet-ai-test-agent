using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class TestPlanLoaderTests
{
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
}
