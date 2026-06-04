using System.Collections.Generic;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class PromptBuilderTests
{
    private static PromptBuilder NewBuilder() => new(new SecretRedactor());

    private static UiSnapshot SampleSnapshot() => new(
        "Login Window",
        new List<UiElement>
        {
            new() { AutomationId = "btnLogin", ControlType = "Button", Name = "Log In" },
            new() { AutomationId = "txtUsername", ControlType = "Edit", Value = "admin" }
        },
        statusText: "Ready");

    private static AgentGoal SampleGoal() => new()
    {
        Description = "Log in with valid credentials",
        Category = TestCategory.Smoke
    };

    [Fact]
    public void BuildRedactsPasswordControlValueEvenWithBenignId()
    {
        // The prompt is what the bridge persists to req-N.txt and what an LLM sees — a
        // password control's value must not appear, even when its id isn't "password".
        var snapshot = new UiSnapshot("Login", new List<UiElement>
        {
            new() { AutomationId = "txt1", ControlType = "Edit", IsPassword = true, Value = "hunter2-secret" }
        }, statusText: "Ready");

        var prompt = NewBuilder().Build(snapshot, SampleGoal(), memoryContext: "");

        Assert.DoesNotContain("hunter2-secret", prompt);
        Assert.Contains("[REDACTED]", prompt);
    }

    [Fact]
    public void BuildIncludesGoalDescription()
    {
        var prompt = NewBuilder().Build(SampleSnapshot(), SampleGoal(), memoryContext: "");

        Assert.Contains("Log in with valid credentials", prompt);
    }

    [Fact]
    public void BuildIncludesSuccessConditionWhenPresent()
    {
        var goal = SampleGoal();
        goal.SuccessCondition = "Login successful";

        var prompt = NewBuilder().Build(SampleSnapshot(), goal, memoryContext: "");

        Assert.Contains("Success condition: UI shows \"Login successful\"", prompt);
    }

    [Fact]
    public void BuildOmitsSuccessConditionLineWhenAbsent()
    {
        var prompt = NewBuilder().Build(SampleSnapshot(), SampleGoal(), memoryContext: "");

        Assert.DoesNotContain("Success condition: UI shows", prompt);
    }

    [Fact]
    public void BuildEmitsSmokeCategoryLine()
    {
        var prompt = NewBuilder().Build(SampleSnapshot(), SampleGoal(), memoryContext: "");

        Assert.Contains("CATEGORY: Smoke Test", prompt);
    }

    [Fact]
    public void BuildEmitsMonkeyCategoryLine()
    {
        var goal = SampleGoal();
        goal.Category = TestCategory.Monkey;

        var prompt = NewBuilder().Build(SampleSnapshot(), goal, memoryContext: "");

        Assert.Contains("CATEGORY: Monkey Testing", prompt);
    }

    [Fact]
    public void BuildListsAllowedActionsWhenProvided()
    {
        var goal = SampleGoal();
        goal.AllowedActions = ["Click", "EnterText"];

        var prompt = NewBuilder().Build(SampleSnapshot(), goal, memoryContext: "");

        Assert.Contains("Allowed actions for this test: Click, EnterText", prompt);
    }

    [Fact]
    public void BuildUsesDefaultAllowedActionsWhenEmpty()
    {
        var prompt = NewBuilder().Build(SampleSnapshot(), SampleGoal(), memoryContext: "");

        Assert.Contains("Allowed actions: EnterText, Click, DoubleClick", prompt);
    }

    [Fact]
    public void BuildIncludesLoopWarningWhenProvided()
    {
        var prompt = NewBuilder().Build(SampleSnapshot(), SampleGoal(), memoryContext: "", loopWarning: "repeated Click on btnLogin");

        Assert.Contains("WARNING: repeated Click on btnLogin", prompt);
        Assert.Contains("You MUST try a DIFFERENT action", prompt);
    }

    [Fact]
    public void BuildOmitsLoopWarningWhenAbsent()
    {
        var prompt = NewBuilder().Build(SampleSnapshot(), SampleGoal(), memoryContext: "");

        Assert.DoesNotContain("WARNING:", prompt);
    }

    [Fact]
    public void BuildListsSnapshotElements()
    {
        var prompt = NewBuilder().Build(SampleSnapshot(), SampleGoal(), memoryContext: "");

        Assert.Contains("btnLogin", prompt);
        Assert.Contains("Window: Login Window", prompt);
    }

    [Fact]
    public void BuildRedactsSecretsInGoalDescription()
    {
        var goal = SampleGoal();
        goal.Description = "Authenticate using password=hunter2 then continue";

        var prompt = NewBuilder().Build(SampleSnapshot(), goal, memoryContext: "");

        Assert.Contains("password=[REDACTED]", prompt);
        Assert.DoesNotContain("hunter2", prompt);
    }

    [Fact]
    public void BuildRedactsSensitiveElementValues()
    {
        var snapshot = new UiSnapshot(
            "Login Window",
            new List<UiElement>
            {
                new() { AutomationId = "txtPassword", ControlType = "Edit", Value = "hunter2" }
            });

        var prompt = NewBuilder().Build(snapshot, SampleGoal(), memoryContext: "");

        Assert.Contains("[REDACTED]", prompt);
        Assert.DoesNotContain("hunter2", prompt);
    }

    [Fact]
    public void BuildRedactsSecretsInMemoryContext()
    {
        var prompt = NewBuilder().Build(SampleSnapshot(), SampleGoal(), memoryContext: "entered token=abc123xyz");

        Assert.DoesNotContain("abc123xyz", prompt);
    }
}
