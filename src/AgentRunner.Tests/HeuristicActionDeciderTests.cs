using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Proves the real agent loop runs end-to-end with a no-LLM, no-key "brain":
/// <see cref="HeuristicActionDecider"/> drives a stateful fake driver (the UI reacts to
/// the agent's actions) through <see cref="RunOrchestrator"/> to success — no OpenRouter,
/// no scripted per-step sequence.
/// </summary>
public sealed class HeuristicActionDeciderTests : IDisposable
{
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), "heuristic-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch { /* best-effort */ }
    }

    private WorkflowConfig Config() =>
        new() { WorkspaceRoot = _workspace, AbortThreshold = -1000, PollIntervalMs = 0 };

    [Fact]
    public async Task DrivesLoginToSuccess_WithNoLlm()
    {
        var driver = new StatefulLoginDriver();
        var decider = new HeuristicActionDecider(
            new Dictionary<string, string> { ["txtUsername"] = "admin", ["txtPassword"] = "password123" },
            ["btnLogin"]);
        var orchestrator = new RunOrchestrator(driver, decider, Config(), interStepDelayMs: 0, waitActionDelayMs: 0);

        var goal = new AgentGoal
        {
            Description = "log in", SuccessCondition = "Login successful", MaxSteps = 8, Identifier = "g"
        };
        var exit = await orchestrator.RunAsync(
            new RunnerOptions { TargetWindow = "Login", Goal = goal, EvidenceLevel = EvidenceLevel.Minimal });

        Assert.Equal(0, exit);
        Assert.Equal("Succeeded", orchestrator.LastArtifact!.Result);
        Assert.Equal("admin", driver.Entered["txtUsername"]);
        Assert.Equal("password123", driver.Entered["txtPassword"]);
        Assert.Contains("btnLogin", driver.Clicked);
    }

    [Fact]
    public async Task WaitsForGatedButton_ThenClicksInOrder()
    {
        // btnProtectedAction is disabled until btnEnableProtectedAction is clicked.
        var driver = new GatedActionDriver();
        var decider = new HeuristicActionDecider(
            new Dictionary<string, string>(), ["btnEnableProtectedAction", "btnProtectedAction"]);
        var orchestrator = new RunOrchestrator(driver, decider, Config(), interStepDelayMs: 0, waitActionDelayMs: 0);

        var goal = new AgentGoal { Description = "run gated action", MaxSteps = 8, Identifier = "g" };
        var exit = await orchestrator.RunAsync(
            new RunnerOptions { TargetWindow = "Controls", Goal = goal, EvidenceLevel = EvidenceLevel.Minimal });

        Assert.Equal(0, exit);
        Assert.Equal("Succeeded", orchestrator.LastArtifact!.Result); // Done with no success condition
        Assert.Equal(new[] { "btnEnableProtectedAction", "btnProtectedAction" }, driver.Clicked);
    }

    private sealed class StatefulLoginDriver : IAutomationDriver
    {
        public Dictionary<string, string> Entered { get; } = new();
        public List<string> Clicked { get; } = new();
        private bool _loggedIn;

        private UiSnapshot Build()
        {
            var status = _loggedIn ? "Login successful" : "Waiting";
            return new UiSnapshot("Login", new List<UiElement>
            {
                new() { AutomationId = "txtUsername" },
                new() { AutomationId = "txtPassword" },
                new() { AutomationId = "btnLogin" },
                new() { AutomationId = "lblStatus", ControlType = "Text", Value = status }
            }, statusText: status);
        }

        public bool AttachToWindow(string windowTitle, TimeSpan timeout) => true;
        public UiSnapshot Capture() => Build();
        public void EnterText(string automationId, string value) => Entered[automationId] = value;
        public void Click(string automationId)
        {
            Clicked.Add(automationId);
            if (automationId == "btnLogin" &&
                Entered.GetValueOrDefault("txtUsername") == "admin" &&
                Entered.GetValueOrDefault("txtPassword") == "password123")
                _loggedIn = true;
        }
        public string ReadText(string automationId) => "";
        public List<UiElement> GetAllElements() => Build().Elements;
        public byte[] CaptureScreenshot() => [];
        public void Scroll(string automationId, string direction) { }
        public void DoubleClick(string automationId) { }
    }

    private sealed class GatedActionDriver : IAutomationDriver
    {
        public List<string> Clicked { get; } = new();
        private bool _enabled;

        private UiSnapshot Build() => new("Controls", new List<UiElement>
        {
            new() { AutomationId = "btnEnableProtectedAction", IsEnabled = true },
            new() { AutomationId = "btnProtectedAction", IsEnabled = _enabled }
        });

        public bool AttachToWindow(string windowTitle, TimeSpan timeout) => true;
        public UiSnapshot Capture() => Build();
        public void EnterText(string automationId, string value) { }
        public void Click(string automationId)
        {
            Clicked.Add(automationId);
            if (automationId == "btnEnableProtectedAction") _enabled = true;
        }
        public string ReadText(string automationId) => "";
        public List<UiElement> GetAllElements() => Build().Elements;
        public byte[] CaptureScreenshot() => [];
        public void Scroll(string automationId, string direction) { }
        public void DoubleClick(string automationId) { }
    }
}
