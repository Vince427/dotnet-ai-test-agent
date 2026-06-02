using System;
using System.IO;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Always-run, key-free coverage of the decide pipeline end-to-end through HTTP:
/// the real <see cref="LlmService"/> (PromptBuilder + OpenAI client) talks to the
/// in-process <see cref="MockLlmServer"/>, and the response is parsed back into an
/// <see cref="AgentAction"/>. No API key, no network egress, no desktop session —
/// so this runs in any CI and locks in the contract the gated UI E2E depends on.
/// </summary>
public sealed class MockLlmServerTests : IDisposable
{
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), "mockllm-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    private LlmService NewLlmService(MockLlmServer server) => new(new WorkflowConfig
    {
        LlmEndpoint = server.BaseUrl,
        LlmApiKey = "test-key",   // non-null so LlmService doesn't fail fast; never used by the mock
        LlmModel = "mock-model",
        WorkspaceRoot = _workspace
    });

    private static UiSnapshot Snapshot() =>
        new("Sample Login App (.NET 8)", [new UiElement { AutomationId = "txtUsername" }]);

    private static AgentGoal Goal() =>
        new() { Description = "log in", SuccessCondition = "Login successful", Identifier = "e2e" };

    [Fact]
    public async Task LlmService_ParsesScriptedActionFromMock()
    {
        using var server = new MockLlmServer(LoginScript.EnterUsername);
        var llm = NewLlmService(server);

        var action = await llm.DecideActionAsync(Snapshot(), Goal(), memoryContext: "");

        Assert.Equal("EnterText", action.ActionType);
        Assert.Equal("txtUsername", action.AutomationId);
        Assert.Equal("admin", action.Value);
        Assert.Equal(1, server.RequestCount);
    }

    [Fact]
    public async Task MockServer_ReturnsScriptedSequenceInOrder()
    {
        using var server = new MockLlmServer(
            LoginScript.EnterUsername, LoginScript.EnterPassword, LoginScript.ClickLogin, LoginScript.Done);
        var llm = NewLlmService(server);
        var goal = Goal();
        var snapshot = Snapshot();

        var a1 = await llm.DecideActionAsync(snapshot, goal, "");
        var a2 = await llm.DecideActionAsync(snapshot, goal, "");
        var a3 = await llm.DecideActionAsync(snapshot, goal, "");
        var a4 = await llm.DecideActionAsync(snapshot, goal, "");

        Assert.Equal("EnterText", a1.ActionType);
        Assert.Equal("txtUsername", a1.AutomationId);
        Assert.Equal("EnterText", a2.ActionType);
        Assert.Equal("txtPassword", a2.AutomationId);
        Assert.Equal("Click", a3.ActionType);
        Assert.Equal("btnLogin", a3.AutomationId);
        Assert.Equal("Done", a4.ActionType);
        Assert.Equal(4, server.RequestCount);
    }

    [Fact]
    public async Task MockServer_RepeatsLastResponseAfterExhaustion()
    {
        using var server = new MockLlmServer(LoginScript.Done);
        var llm = NewLlmService(server);
        var goal = Goal();
        var snapshot = Snapshot();

        var first = await llm.DecideActionAsync(snapshot, goal, "");
        var second = await llm.DecideActionAsync(snapshot, goal, "");

        Assert.Equal("Done", first.ActionType);
        Assert.Equal("Done", second.ActionType);
    }
}
