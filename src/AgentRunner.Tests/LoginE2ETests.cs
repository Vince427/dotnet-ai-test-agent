using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Full-stack, key-free end-to-end: a scripted <see cref="MockLlmServer"/> drives
/// the real <see cref="LlmService"/> → real <see cref="FlaUiDesktopDriver"/> → real
/// WinForms sample app, and we assert the app reaches "Login successful". This is
/// the permanent integration test for an interactive Windows runner.
///
/// Gated by <see cref="InteractiveUiFactAttribute"/> (RUN_E2E_UI=1) because UIA
/// needs a logged-in desktop session and the sample exe must be built.
/// </summary>
[Collection(InteractiveUiCollection.Name)]
public sealed class LoginE2ETests : IDisposable
{
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), "e2e-login-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    [InteractiveUiFact]
    public async Task ScriptedLlm_DrivesRealAppToLoginSuccess()
    {
        using var server = new MockLlmServer(
            LoginScript.EnterUsername, LoginScript.EnterPassword, LoginScript.ClickLogin, LoginScript.Done);

        using var app = DesktopE2E.LaunchWinFormsSample();
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
            DesktopE2E.WaitForControlReady(driver, DesktopE2E.WinFormsWindowTitle, "txtUsername", TimeSpan.FromSeconds(20));

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
                TargetWindow = DesktopE2E.WinFormsWindowTitle,
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
