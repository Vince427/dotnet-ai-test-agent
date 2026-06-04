using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Direct coverage of the act-stage dispatch extracted into <see cref="ActionExecutor"/>:
/// validation (allow-list + target existence), each verb, the Done success/fail branch, and
/// the exception → <c>action_failed</c> path. Behaviour must match the previous inline loop.
/// </summary>
public sealed class ActionExecutorTests
{
    private readonly RecordingDriver _driver = new();
    private readonly AgentMemory _memory = new(new SecretRedactor());

    private ActionExecutor NewExecutor() =>
        new(_driver, new SecretRedactor(), new StructuredLogger(), _memory, waitActionDelayMs: 0);

    private static UiSnapshot SnapshotWith(params UiElement[] elements) =>
        new("Window", new List<UiElement>(elements));

    private static AgentGoal Goal(string? successCondition = null, params string[] allowed)
    {
        var g = new AgentGoal { Description = "test" };
        if (successCondition != null) g.SuccessCondition = successCondition;
        foreach (var a in allowed) g.AllowedActions.Add(a);
        return g;
    }

    [Fact]
    public async Task EnterText_DispatchesToDriverAndMemory()
    {
        var snapshot = SnapshotWith(new UiElement { AutomationId = "txtUser" });
        var action = new AgentAction { ActionType = "EnterText", AutomationId = "txtUser", Value = "alice" };

        var result = await NewExecutor().ExecuteAsync(action, snapshot, Goal());

        Assert.True(result.Succeeded);
        Assert.Equal("alice", _driver.EnteredText["txtUser"]);
    }

    [Fact]
    public async Task Click_DispatchesToDriver()
    {
        var snapshot = SnapshotWith(new UiElement { AutomationId = "btnGo" });
        var action = new AgentAction { ActionType = "Click", AutomationId = "btnGo" };

        var result = await NewExecutor().ExecuteAsync(action, snapshot, Goal());

        Assert.True(result.Succeeded);
        Assert.Contains("btnGo", _driver.Clicked);
    }

    [Fact]
    public async Task ActionNotInAllowList_IsRejectedBeforeDispatch()
    {
        var snapshot = SnapshotWith(new UiElement { AutomationId = "btnGo" });
        var action = new AgentAction { ActionType = "Click", AutomationId = "btnGo" };

        var result = await NewExecutor().ExecuteAsync(action, snapshot, Goal(allowed: "EnterText"));

        Assert.False(result.Succeeded);
        Assert.Equal("action_not_allowed", result.FailureCode);
        Assert.Empty(_driver.Clicked); // never reached the driver
    }

    [Fact]
    public async Task TargetMissingFromSnapshot_FailsValidation()
    {
        var snapshot = SnapshotWith(new UiElement { AutomationId = "somethingElse" });
        var action = new AgentAction { ActionType = "Click", AutomationId = "ghost" };

        var result = await NewExecutor().ExecuteAsync(action, snapshot, Goal());

        Assert.False(result.Succeeded);
        Assert.Equal("action_target_not_found", result.FailureCode);
        Assert.Empty(_driver.Clicked);
    }

    [Fact]
    public async Task Assert_PassesWhenTextMatches()
    {
        _driver.ReadTextResult = "Welcome";
        var snapshot = SnapshotWith(new UiElement { AutomationId = "lbl" });
        var action = new AgentAction { ActionType = "Assert", AutomationId = "lbl", Value = "Welcome" };

        var result = await NewExecutor().ExecuteAsync(action, snapshot, Goal());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Assert_FailsWhenTextDiffers()
    {
        _driver.ReadTextResult = "Goodbye";
        var snapshot = SnapshotWith(new UiElement { AutomationId = "lbl" });
        var action = new AgentAction { ActionType = "Assert", AutomationId = "lbl", Value = "Welcome" };

        var result = await NewExecutor().ExecuteAsync(action, snapshot, Goal());

        Assert.False(result.Succeeded);
        Assert.Equal("assertion_failed", result.FailureCode);
    }

    [Fact]
    public async Task Done_SucceedsWhenNoSuccessConditionConfigured()
    {
        var action = new AgentAction { ActionType = "Done" };

        var result = await NewExecutor().ExecuteAsync(action, SnapshotWith(), Goal());

        Assert.True(result.Succeeded);
        Assert.True(result.DoneSucceeded);
        Assert.Equal("agent_done", result.OutcomeDetail);
    }

    [Fact]
    public async Task Done_FailsWhenSuccessConditionNotVisible()
    {
        var action = new AgentAction { ActionType = "Done" };
        var snapshot = new UiSnapshot("Window", new List<UiElement>(), statusText: "still working");

        var result = await NewExecutor().ExecuteAsync(action, snapshot, Goal(successCondition: "Login successful"));

        Assert.False(result.Succeeded);
        Assert.False(result.DoneSucceeded);
        Assert.Equal("done_without_success_condition", result.FailureCode);
    }

    [Fact]
    public async Task Done_SucceedsWhenSuccessConditionVisibleInStatus()
    {
        var action = new AgentAction { ActionType = "Done" };
        var snapshot = new UiSnapshot("Window", new List<UiElement>(), statusText: "Login successful now");

        var result = await NewExecutor().ExecuteAsync(action, snapshot, Goal(successCondition: "Login successful"));

        Assert.True(result.DoneSucceeded);
    }

    [Fact]
    public async Task UnknownVerb_IsUnsupported()
    {
        var action = new AgentAction { ActionType = "Teleport" };

        var result = await NewExecutor().ExecuteAsync(action, SnapshotWith(), Goal());

        Assert.False(result.Succeeded);
        Assert.Equal("unsupported_action", result.FailureCode);
    }

    [Fact]
    public async Task DriverException_BecomesActionFailed()
    {
        _driver.ThrowOnClick = new InvalidOperationException("boom");
        var snapshot = SnapshotWith(new UiElement { AutomationId = "btnGo" });
        var action = new AgentAction { ActionType = "Click", AutomationId = "btnGo" };

        var result = await NewExecutor().ExecuteAsync(action, snapshot, Goal());

        Assert.False(result.Succeeded);
        Assert.Equal("action_failed", result.FailureCode);
    }

    private sealed class RecordingDriver : IAutomationDriver
    {
        public string ReadTextResult { get; set; } = "";
        public Dictionary<string, string> EnteredText { get; } = new();
        public List<string> Clicked { get; } = new();
        public Exception? ThrowOnClick { get; set; }

        public bool AttachToWindow(string windowTitle, TimeSpan timeout) => true;
        public UiSnapshot Capture() => new("Window", new List<UiElement>());
        public void EnterText(string automationId, string value) => EnteredText[automationId] = value;
        public void Click(string automationId)
        {
            if (ThrowOnClick != null) throw ThrowOnClick;
            Clicked.Add(automationId);
        }
        public string ReadText(string automationId) => ReadTextResult;
        public List<UiElement> GetAllElements() => new();
        public byte[] CaptureScreenshot() => [];
        public void Scroll(string automationId, string direction) { }
        public void DoubleClick(string automationId) { }
    }
}
