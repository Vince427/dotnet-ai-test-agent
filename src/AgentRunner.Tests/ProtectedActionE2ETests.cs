using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Proves the thesis "rich target, flat contract": a deliberately complex flow on
/// the WinForms sample — a button that is DISABLED until another action enables it,
/// plus a separate status region — is authored with the SAME flat YAML shape as the
/// simple login (goal + allowed actions + an Assert). The goal here is loaded from
/// <c>tests/examples/demo/protected-action.yaml</c> so the contract under test is the
/// real user-facing artifact, not an inline fabrication.
///
/// Gated by <see cref="InteractiveUiFactAttribute"/> (RUN_E2E_UI=1): interactive UIA.
/// </summary>
[Collection(InteractiveUiCollection.Name)]
public sealed class ProtectedActionE2ETests : IDisposable
{
    // Scripted decisions mirroring the YAML's intent: enable the gated action, run
    // it, verify the controls status, finish.
    private const string ClickEnable =
        "{\"actionType\":\"Click\",\"automationId\":\"btnEnableProtectedAction\",\"reason\":\"enable gated action\",\"confidence\":95}";
    private const string ClickProtected =
        "{\"actionType\":\"Click\",\"automationId\":\"btnProtectedAction\",\"reason\":\"run gated action\",\"confidence\":95}";
    private const string AssertStatus =
        "{\"actionType\":\"Assert\",\"automationId\":\"lblControlsStatus\",\"value\":\"Protected action completed\",\"reason\":\"verify completion\",\"confidence\":95}";
    private const string Done =
        "{\"actionType\":\"Done\",\"reason\":\"flow complete\",\"confidence\":95}";

    public static IEnumerable<object[]> Frameworks() => [["winforms"], ["wpf"]];

    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), "e2e-protected-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    [InteractiveUiTheory]
    [MemberData(nameof(Frameworks))]
    public async Task FlatYaml_DrivesComplexGatedActionFlow(string frameworkKey)
    {
        var target = DesktopE2E.Target(frameworkKey);

        // The flat user-facing contract is the source of the goal under test. The
        // goal/allowed-actions are framework-neutral; only the target window varies.
        var goal = LoadGoalFromYaml("DEMO-PROTECTED-001");
        Assert.Null(goal.SuccessCondition);                       // verified by Assert, not a status string
        Assert.Contains("Assert", goal.AllowedActions);

        using var server = new MockLlmServer(ClickEnable, ClickProtected, AssertStatus, Done);
        using var app = DesktopE2E.LaunchSample(target);
        try
        {
            var config = new WorkflowConfig
            {
                LlmEndpoint = server.BaseUrl,
                LlmApiKey = "test-key",
                LlmModel = "mock-model",
                WorkspaceRoot = _workspace,
                AbortThreshold = -1000,
                PollIntervalMs = 0
            };

            var llm = new LlmService(config);
            using var driver = new FlaUiDesktopDriver();
            DesktopE2E.WaitForControlReady(
                driver, target.WindowTitle, "btnEnableProtectedAction", TimeSpan.FromSeconds(20));

            var orchestrator = new RunOrchestrator(driver, llm, config);
            var options = new RunnerOptions
            {
                TargetWindow = target.WindowTitle,
                Goal = goal,
                EvidenceLevel = EvidenceLevel.Minimal
            };

            var exit = await orchestrator.RunAsync(options);

            Assert.Equal(0, exit);
            Assert.Equal("Succeeded", orchestrator.LastArtifact!.Result);

            // The Assert step ran against the gated control's status and passed.
            var assertStep = orchestrator.LastArtifact!.Steps.Single(s => s.ActionType == "Assert");
            Assert.Equal("Succeeded", assertStep.Outcome);
            Assert.Null(assertStep.FailureCode);

            // And the real app actually reached completion (read via UIA).
            Assert.Equal("Protected action completed", driver.ReadText("lblControlsStatus"));
        }
        finally
        {
            try { if (!app.HasExited) app.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }
    }

    private static AgentGoal LoadGoalFromYaml(string testId)
    {
        var repoRoot = DesktopE2E.FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Could not locate repo root.");
        var yamlPath = Path.Combine(repoRoot, "tests", "examples", "demo", "protected-action.yaml");
        var plan = TestPlanLoader.Load(yamlPath);
        var test = plan.FindById(testId)
            ?? throw new InvalidOperationException($"Test '{testId}' not found in {yamlPath}.");
        return test.ToAgentGoal();
    }
}
