using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Full-stack, key-free end-to-end across desktop frameworks: a scripted
/// <see cref="MockLlmServer"/> drives the real <see cref="LlmService"/> → real
/// <see cref="FlaUiDesktopDriver"/> → the real sample app, and we assert the app
/// reaches "Login successful". Runs against BOTH the WinForms and WPF samples from
/// one body — the agent path is UIA-based and framework-agnostic, and both samples
/// share the same automation ids, so the same scripted flow proves both.
///
/// Gated by <see cref="InteractiveUiTheoryAttribute"/> (RUN_E2E_UI=1): UIA needs a
/// logged-in desktop session and the sample exes must be built.
/// </summary>
[Collection(InteractiveUiCollection.Name)]
public sealed class LoginE2ETests : IDisposable
{
    public static IEnumerable<object[]> Frameworks() => [["winforms"], ["wpf"]];

    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), "e2e-login-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    [InteractiveUiTheory]
    [MemberData(nameof(Frameworks))]
    public async Task ScriptedLlm_DrivesRealAppToLoginSuccess(string frameworkKey)
    {
        var target = DesktopE2E.Target(frameworkKey);

        using var server = new MockLlmServer(
            LoginScript.EnterUsername, LoginScript.EnterPassword, LoginScript.ClickLogin, LoginScript.Done);

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
            DesktopE2E.WaitForControlReady(driver, target.WindowTitle, "txtUsername", TimeSpan.FromSeconds(20));

            var orchestrator = new RunOrchestrator(driver, llm, config);

            var goal = new AgentGoal
            {
                Description = "Enter admin / password123, click Login, confirm success.",
                SuccessCondition = "Login successful",
                MaxSteps = 8,
                Identifier = "e2e-login",
                AllowedActions = ["EnterText", "Click", "Done", "Wait", "Assert"]
            };

            var options = new RunnerOptions
            {
                TargetWindow = target.WindowTitle,
                Goal = goal,
                EvidenceLevel = EvidenceLevel.Standard // also exercises real screenshot capture
            };

            var exit = await orchestrator.RunAsync(options);

            Assert.Equal(0, exit);
            Assert.Equal("Succeeded", orchestrator.LastArtifact!.Result);
            // The real app actually rendered the success label (read via UIA).
            Assert.Equal("Login successful", driver.ReadText("lblStatus"));
        }
        finally
        {
            try { if (!app.HasExited) app.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }
    }
}
