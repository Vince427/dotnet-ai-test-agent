using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Deterministic loop tests for <see cref="RunOrchestrator"/>: no LLM key, no
/// FlaUI, no target app. A scripted <see cref="IActionDecider"/> drives the loop
/// over a fake <see cref="IAutomationDriver"/>; assertions read
/// <see cref="RunOrchestrator.LastArtifact"/> and the returned exit code.
///
/// This is the payoff of WB-2: the observe → decide → act → score → record loop
/// is now unit-testable in isolation.
/// </summary>
public sealed class RunOrchestratorTests : IDisposable
{
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), "orch-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workspace))
                Directory.Delete(_workspace, recursive: true);
        }
        catch
        {
            // Best-effort cleanup of a temp dir; never fail a test on this.
        }
    }

    private WorkflowConfig NewConfig(int abortThreshold = -1000) => new()
    {
        WorkspaceRoot = _workspace,
        AbortThreshold = abortThreshold,
        PollIntervalMs = 0
    };

    private static RunnerOptions OptionsFor(AgentGoal goal, bool asTest = false) => new()
    {
        TargetWindow = "Fake Window",
        Goal = goal,
        EvidenceLevel = EvidenceLevel.Minimal, // skip screenshot writes
        Test = asTest ? new TestDefinition { Id = "T-1", Title = "fake" } : null,
        TestId = asTest ? "T-1" : null
    };

    private RunOrchestrator NewOrchestrator(
        IAutomationDriver driver, IActionDecider decider, WorkflowConfig? config = null)
        => new(driver, decider, config ?? NewConfig(), interStepDelayMs: 0, waitActionDelayMs: 0);

    [Fact]
    public async Task AttachFailure_WithTest_ReturnsBlockedExit4()
    {
        var driver = new FakeDriver { AttachResult = false };
        var orchestrator = NewOrchestrator(driver, new ScriptedDecider());

        var goal = new AgentGoal { Description = "x", MaxSteps = 3, Identifier = "g" };
        var exit = await orchestrator.RunAsync(OptionsFor(goal, asTest: true));

        Assert.Equal(4, exit);
        Assert.Equal("Blocked", orchestrator.LastArtifact!.Result);
        Assert.Contains("Window not found", orchestrator.LastArtifact!.ErrorMessage);
    }

    [Fact]
    public async Task AttachFailure_NoTest_ReturnsFailedExit1()
    {
        var driver = new FakeDriver { AttachResult = false };
        var orchestrator = NewOrchestrator(driver, new ScriptedDecider());

        var goal = new AgentGoal { Description = "x", MaxSteps = 3, Identifier = "g" };
        var exit = await orchestrator.RunAsync(OptionsFor(goal, asTest: false));

        Assert.Equal(1, exit);
        Assert.Equal("Failed", orchestrator.LastArtifact!.Result);
    }

    [Fact]
    public async Task SuccessConditionVisibleOnObserve_Succeeds()
    {
        var driver = new FakeDriver
        {
            Snapshot = new UiSnapshot("Fake Window", [new UiElement { Name = "ok" }], statusText: "Login successful")
        };
        // Decider should never be consulted; success is detected on observe.
        var orchestrator = NewOrchestrator(driver, new ScriptedDecider());

        var goal = new AgentGoal
        {
            Description = "log in",
            SuccessCondition = "Login successful",
            MaxSteps = 5,
            Identifier = "login"
        };
        var exit = await orchestrator.RunAsync(OptionsFor(goal));

        Assert.Equal(0, exit);
        Assert.Equal("Succeeded", orchestrator.LastArtifact!.Result);
    }

    [Fact]
    public async Task EnterTextThenDone_NoSuccessCondition_Succeeds()
    {
        var driver = new FakeDriver
        {
            Snapshot = new UiSnapshot("Fake Window", [new UiElement { AutomationId = "txtUser", Name = "User" }])
        };
        var decider = new ScriptedDecider(
            new AgentAction { ActionType = "EnterText", AutomationId = "txtUser", Value = "admin" },
            new AgentAction { ActionType = "Done" });
        var orchestrator = NewOrchestrator(driver, decider);

        var goal = new AgentGoal { Description = "fill", MaxSteps = 5, Identifier = "g" };
        var exit = await orchestrator.RunAsync(OptionsFor(goal));

        Assert.Equal(0, exit);
        Assert.Equal("Succeeded", orchestrator.LastArtifact!.Result);
        Assert.Equal(2, orchestrator.LastArtifact!.Steps.Count);
        Assert.Equal("admin", driver.EnteredText["txtUser"]);
        Assert.True(orchestrator.LastArtifact!.FinalScore >= 2); // EnterText reward
    }

    [Fact]
    public async Task DoneBeforeSuccessConditionVisible_FailsToMaxSteps()
    {
        var driver = new FakeDriver
        {
            Snapshot = new UiSnapshot("Fake Window", [new UiElement { Name = "ok" }]) // no status text
        };
        var decider = new ScriptedDecider { Repeat = new AgentAction { ActionType = "Done" } };
        var orchestrator = NewOrchestrator(driver, decider);

        var goal = new AgentGoal
        {
            Description = "log in",
            SuccessCondition = "Login successful",
            MaxSteps = 2,
            Identifier = "login"
        };
        var exit = await orchestrator.RunAsync(OptionsFor(goal));

        Assert.Equal(3, exit);
        Assert.Equal("Failed", orchestrator.LastArtifact!.Result);
        Assert.Equal(2, orchestrator.LastArtifact!.Steps.Count);
        Assert.All(orchestrator.LastArtifact!.Steps,
            s => Assert.Equal("done_without_success_condition", s.FailureCode));
    }

    [Fact]
    public async Task ActionNotInAllowedList_IsRejected()
    {
        var driver = new FakeDriver
        {
            Snapshot = new UiSnapshot("Fake Window", [new UiElement { AutomationId = "txtUser" }])
        };
        var decider = new ScriptedDecider
        {
            Repeat = new AgentAction { ActionType = "EnterText", AutomationId = "txtUser", Value = "x" }
        };
        var orchestrator = NewOrchestrator(driver, decider);

        var goal = new AgentGoal
        {
            Description = "click only",
            MaxSteps = 1,
            Identifier = "g",
            AllowedActions = ["Click"]
        };
        var exit = await orchestrator.RunAsync(OptionsFor(goal));

        Assert.Equal(3, exit);
        Assert.Equal("action_not_allowed", orchestrator.LastArtifact!.Steps[0].FailureCode);
    }

    [Fact]
    public async Task UnsupportedAction_IsRejected()
    {
        var driver = new FakeDriver
        {
            Snapshot = new UiSnapshot("Fake Window", [new UiElement { Name = "ok" }])
        };
        var decider = new ScriptedDecider { Repeat = new AgentAction { ActionType = "Frobnicate" } };
        var orchestrator = NewOrchestrator(driver, decider);

        var goal = new AgentGoal { Description = "x", MaxSteps = 1, Identifier = "g" };
        var exit = await orchestrator.RunAsync(OptionsFor(goal));

        Assert.Equal(3, exit);
        Assert.Equal("unsupported_action", orchestrator.LastArtifact!.Steps[0].FailureCode);
    }

    [Fact]
    public async Task DeciderThrows_BelowThreshold_Aborts()
    {
        var driver = new FakeDriver
        {
            Snapshot = new UiSnapshot("Fake Window", [new UiElement { Name = "ok" }])
        };
        var decider = new ScriptedDecider { Throw = new InvalidOperationException("429 rate limited") };
        // One LlmCall failure = -5; abort threshold -5 trips immediately.
        var orchestrator = NewOrchestrator(driver, decider, NewConfig(abortThreshold: -5));

        var goal = new AgentGoal { Description = "x", MaxSteps = 5, Identifier = "g" };
        var exit = await orchestrator.RunAsync(OptionsFor(goal));

        Assert.Equal(3, exit);
        Assert.Equal("Aborted", orchestrator.LastArtifact!.Result);
        Assert.Equal("llm_call_failed", orchestrator.LastArtifact!.Steps[0].FailureCode);
    }

    [Fact]
    public async Task AssertAction_MismatchedText_Fails()
    {
        var driver = new FakeDriver
        {
            Snapshot = new UiSnapshot("Fake Window", [new UiElement { AutomationId = "lbl" }]),
            ReadTextResult = "actual value"
        };
        var decider = new ScriptedDecider
        {
            Repeat = new AgentAction { ActionType = "Assert", AutomationId = "lbl", Value = "expected value" }
        };
        var orchestrator = NewOrchestrator(driver, decider);

        var goal = new AgentGoal { Description = "x", MaxSteps = 1, Identifier = "g" };
        var exit = await orchestrator.RunAsync(OptionsFor(goal));

        Assert.Equal(3, exit);
        Assert.Equal("assertion_failed", orchestrator.LastArtifact!.Steps[0].FailureCode);
    }

    // --- Test doubles ---

    private sealed class FakeDriver : IAutomationDriver
    {
        public bool AttachResult { get; set; } = true;
        public UiSnapshot Snapshot { get; set; } = new("Fake Window", [new UiElement { Name = "ok" }]);
        public string ReadTextResult { get; set; } = "";
        public Dictionary<string, string> EnteredText { get; } = new();
        public List<string> Clicked { get; } = new();

        public bool AttachToWindow(string windowTitle, TimeSpan timeout) => AttachResult;
        public UiSnapshot Capture() => Snapshot;
        public void EnterText(string automationId, string value) => EnteredText[automationId] = value;
        public void Click(string automationId) => Clicked.Add(automationId);
        public string ReadText(string automationId) => ReadTextResult;
        public List<UiElement> GetAllElements() => Snapshot.Elements;
        public byte[] CaptureScreenshot() => [];
        public void Scroll(string automationId, string direction) { }
        public void DoubleClick(string automationId) { }
    }

    private sealed class ScriptedDecider : IActionDecider
    {
        private readonly Queue<AgentAction> _actions;

        public ScriptedDecider(params AgentAction[] actions) => _actions = new Queue<AgentAction>(actions);

        /// <summary>Returned for every step once the scripted queue is exhausted.</summary>
        public AgentAction? Repeat { get; set; }

        /// <summary>If set, every decide call throws this exception.</summary>
        public Exception? Throw { get; set; }

        public Task<AgentAction> DecideActionAsync(
            UiSnapshot snapshot, AgentGoal goal, string memoryContext, string? loopWarning = null)
        {
            if (Throw != null)
                throw Throw;
            if (_actions.Count > 0)
                return Task.FromResult(_actions.Dequeue());
            if (Repeat != null)
                return Task.FromResult(Repeat);
            throw new InvalidOperationException("ScriptedDecider ran out of actions.");
        }
    }
}
