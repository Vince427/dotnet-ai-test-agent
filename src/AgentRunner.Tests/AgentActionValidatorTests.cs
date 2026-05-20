using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class AgentActionValidatorTests
{
    [Fact]
    public void ValidateTargetExistsPassesWhenAutomationIdIsPresent()
    {
        var snapshot = new UiSnapshot("Window", [new UiElement { AutomationId = "btnLogin", Name = "Login" }]);
        var action = new AgentAction { ActionType = "Click", AutomationId = "btnLogin" };

        var result = AgentActionValidator.ValidateTargetExists(action, snapshot);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateTargetExistsPassesWhenNameIsPresent()
    {
        var snapshot = new UiSnapshot("Window", [new UiElement { AutomationId = "button1", Name = "Login" }]);
        var action = new AgentAction { ActionType = "Click", AutomationId = "Login" };

        var result = AgentActionValidator.ValidateTargetExists(action, snapshot);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateTargetExistsFailsWhenTargetIsMissingFromSnapshot()
    {
        var snapshot = new UiSnapshot("Window", [new UiElement { AutomationId = "btnLogin", Name = "Login" }]);
        var action = new AgentAction { ActionType = "EnterText", AutomationId = "txtPassword", Value = "hunter2" };

        var result = AgentActionValidator.ValidateTargetExists(action, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal("action_target_not_found", result.Code);
    }

    [Theory]
    [InlineData("Wait")]
    [InlineData("Done")]
    [InlineData("Explore")]
    public void ValidateTargetExistsDoesNotRequireTargetForUntargetedActions(string actionType)
    {
        var snapshot = new UiSnapshot("Window", []);
        var action = new AgentAction { ActionType = actionType };

        var result = AgentActionValidator.ValidateTargetExists(action, snapshot);

        Assert.True(result.IsValid);
    }
}
