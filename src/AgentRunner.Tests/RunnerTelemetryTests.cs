using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Verifies OBS-1 instrumentation without an OTLP collector: a plain
/// <see cref="ActivityListener"/> subscribes to the runner's <see cref="ActivitySource"/>
/// (which is what makes <c>StartActivity</c> return non-null), the orchestrator runs
/// over a fake driver + scripted decider, and we assert the expected spans, tags, and
/// that the run's trace id is persisted to the artifact. Always-run, key-free.
/// </summary>
public sealed class RunnerTelemetryTests : IDisposable
{
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), "telemetry-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    [Fact]
    public async Task Run_EmitsSpans_AndPersistsTraceId()
    {
        // Process-global listener + parallel test classes can interleave activities,
        // so use a thread-safe queue and filter to this run's trace below.
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == RunnerTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stopped.Enqueue
        };
        ActivitySource.AddActivityListener(listener);

        var driver = new TelemetryFakeDriver();
        var decider = new TelemetryScriptedDecider(new AgentAction { ActionType = "Done" });
        var config = new WorkflowConfig { WorkspaceRoot = _workspace, AbortThreshold = -1000, PollIntervalMs = 0 };
        var orchestrator = new RunOrchestrator(driver, decider, config, interStepDelayMs: 0, waitActionDelayMs: 0);

        var goal = new AgentGoal { Description = "x", MaxSteps = 3, Identifier = "g" };
        var exit = await orchestrator.RunAsync(
            new RunnerOptions { TargetWindow = "W", Goal = goal, EvidenceLevel = EvidenceLevel.Minimal });

        Assert.Equal(0, exit);

        // Trace id is persisted to the artifact so a recorded run links to its trace.
        var traceId = orchestrator.LastArtifact!.TraceId;
        Assert.False(string.IsNullOrEmpty(traceId));

        // Isolate just this run's spans (other parallel tests share the source).
        var mine = stopped.Where(a => a.TraceId.ToString() == traceId).ToList();
        Assert.Contains(mine, a => a.OperationName == "agentloop.run");
        Assert.Contains(mine, a => a.OperationName == "agentloop.step");
        Assert.Contains(mine, a => a.OperationName == "agentloop.observe");
        Assert.Contains(mine, a => a.OperationName == "agentloop.decide");

        // Root span carries the outcome and the step is nested under the run.
        var run = mine.Find(a => a.OperationName == "agentloop.run")!;
        var stepSpan = mine.Find(a => a.OperationName == "agentloop.step")!;
        Assert.Equal("Succeeded", (string?)run.GetTagItem("agentloop.result"));
        Assert.Equal(0, (int)run.GetTagItem("agentloop.exit_code")!);
        Assert.Equal(run.SpanId, stepSpan.Parent?.SpanId);
    }

    [Fact]
    public void TryStartExport_ReturnsNull_WhenNoEndpointConfigured()
    {
        var prior = Environment.GetEnvironmentVariable(RunnerTelemetry.EndpointEnvVar);
        Environment.SetEnvironmentVariable(RunnerTelemetry.EndpointEnvVar, null);
        try
        {
            using var handle = RunnerTelemetry.TryStartExport(new WorkflowConfig());
            Assert.Null(handle); // export is opt-in; no endpoint => no provider, no cost
        }
        finally
        {
            Environment.SetEnvironmentVariable(RunnerTelemetry.EndpointEnvVar, prior);
        }
    }

    [Fact]
    public async Task NoTraceId_WhenNoListener()
    {
        // With no ActivityListener subscribed, StartActivity returns null and the
        // instrumentation is a no-op — the artifact carries no trace id.
        var driver = new TelemetryFakeDriver();
        var decider = new TelemetryScriptedDecider(new AgentAction { ActionType = "Done" });
        var config = new WorkflowConfig { WorkspaceRoot = _workspace, AbortThreshold = -1000, PollIntervalMs = 0 };
        var orchestrator = new RunOrchestrator(driver, decider, config, interStepDelayMs: 0, waitActionDelayMs: 0);

        var goal = new AgentGoal { Description = "x", MaxSteps = 3, Identifier = "g" };
        await orchestrator.RunAsync(
            new RunnerOptions { TargetWindow = "W", Goal = goal, EvidenceLevel = EvidenceLevel.Minimal });

        Assert.Null(orchestrator.LastArtifact!.TraceId);
    }

    private sealed class TelemetryFakeDriver : IAutomationDriver
    {
        private readonly UiSnapshot _snapshot = new("W", [new UiElement { Name = "ok" }]);

        public bool AttachToWindow(string windowTitle, TimeSpan timeout) => true;
        public UiSnapshot Capture() => _snapshot;
        public void EnterText(string automationId, string value) { }
        public void Click(string automationId) { }
        public string ReadText(string automationId) => "";
        public List<UiElement> GetAllElements() => _snapshot.Elements;
        public byte[] CaptureScreenshot() => [];
        public void Scroll(string automationId, string direction) { }
        public void DoubleClick(string automationId) { }
    }

    private sealed class TelemetryScriptedDecider(AgentAction action) : IActionDecider
    {
        public Task<AgentAction> DecideActionAsync(
            UiSnapshot snapshot, AgentGoal goal, string memoryContext, string? loopWarning = null)
            => Task.FromResult(action);
    }
}
