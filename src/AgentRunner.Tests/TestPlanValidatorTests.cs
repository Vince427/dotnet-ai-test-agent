using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class TestPlanValidatorTests
{
    [Fact]
    public void ValidateAcceptsManualEditablePlan()
    {
        var plan = TestPlanLoader.Parse("""
suite: smoke

tests:
  LOGIN-001:
    title: "Login succeeds"
    priority: "P0"
    framework: "winforms"
    target_window: "Sample Login App (.NET 8)"
    goal: "Log in."
    max_steps: 8
    allowed_actions: ["EnterText", "Click", "Assert", "Done"]
    existing_tests:
      - "MyApp.Tests.Auth.LoginSucceeds"
""");

        var result = TestPlanValidator.Validate(plan, "tests/smoke.yaml");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateRejectsDuplicateIdsAndUnsupportedActions()
    {
        var plan = TestPlanLoader.Parse("""
suite: smoke

tests:
  LOGIN-001:
    goal: "Log in."
    allowed_actions: ["Click", "MagicClick"]
  login-001:
    goal: "Log in again."
    allowed_actions: ["Click"]
""");

        var result = TestPlanValidator.Validate(plan, "tests/smoke.yaml");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("duplicate test id"));
        Assert.Contains(result.Errors, error => error.Contains("MagicClick"));
    }

    [Fact]
    public void ValidateRejectsUnknownRisk()
    {
        var plan = TestPlanLoader.Parse("""
suite: smoke

tests:
  LOGIN-001:
    goal: "Log in."
    risk: "wild"
""");

        var result = TestPlanValidator.Validate(plan, "tests/smoke.yaml");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("risk"));
    }

    [Fact]
    public void ValidateEmitsPolicyWarningsWithoutFailing()
    {
        var plan = TestPlanLoader.Parse("""
suite: edge

tests:
  W-001:
    goal: "do it"
    framework: "qtwidgets"
    max_steps: 250
    allowed_actions: ["Click", "Done"]
""");

        var result = TestPlanValidator.Validate(plan, "tests/edge.yaml");

        Assert.True(result.IsValid); // warnings are non-fatal
        Assert.Contains(result.Warnings, w => w.Contains("framework 'qtwidgets'"));
        Assert.Contains(result.Warnings, w => w.Contains("max_steps 250"));
        Assert.Contains(result.Warnings, w => w.Contains("no success_condition"));
    }

    [Fact]
    public void ValidateEmitsNoWarningsForACleanPlan()
    {
        var plan = TestPlanLoader.Parse("""
suite: clean

tests:
  C-001:
    goal: "do it"
    framework: "wpf"
    success_condition: "Done"
    max_steps: 10
    allowed_actions: ["Click", "Done"]
""");

        var result = TestPlanValidator.Validate(plan, "tests/clean.yaml");

        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }
}
