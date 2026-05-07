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
}
