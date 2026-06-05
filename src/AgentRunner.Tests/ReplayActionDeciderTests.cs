using System.Collections.Generic;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Deterministic, key-free replay decider (V9.5 record -> replay): emits the recorded actions in
/// order, then Done. Pure — no driver, no LLM.
/// </summary>
public sealed class ReplayActionDeciderTests
{
    private static readonly UiSnapshot Snap = new("App", new List<UiElement>());
    private static readonly AgentGoal Goal = new() { Description = "replay" };

    private static List<RecordedAction> LoginScript() =>
    [
        new() { Verb = "EnterText", Target = "txtUsername", Name = "Username", Value = "admin" },
        new() { Verb = "EnterText", Target = "txtPassword", Name = "Password", Value = "[REDACTED]" },
        new() { Verb = "Click", Target = "btnLogin", Name = "Log In" },
    ];

    [Fact]
    public async Task ReplaysEachRecordedActionInOrder_ThenDone()
    {
        var d = new ReplayActionDecider(LoginScript());

        var a1 = await d.DecideActionAsync(Snap, Goal, "");
        Assert.Equal("EnterText", a1.ActionType);
        Assert.Equal("txtUsername", a1.AutomationId);
        Assert.Equal("admin", a1.Value);

        var a2 = await d.DecideActionAsync(Snap, Goal, "");
        Assert.Equal("txtPassword", a2.AutomationId);

        var a3 = await d.DecideActionAsync(Snap, Goal, "");
        Assert.Equal("Click", a3.ActionType);
        Assert.Equal("btnLogin", a3.AutomationId);

        // Script exhausted -> Done (terminates the loop).
        var a4 = await d.DecideActionAsync(Snap, Goal, "");
        Assert.Equal("Done", a4.ActionType);
        // Stays Done if asked again.
        Assert.Equal("Done", (await d.DecideActionAsync(Snap, Goal, "")).ActionType);
    }

    [Fact]
    public async Task SubstitutesARedactedSecretFromTheResolver()
    {
        // A recorded password is stored "[REDACTED]"; the resolver supplies the real value at replay.
        var d = new ReplayActionDecider(
            [new RecordedAction { Verb = "EnterText", Target = "txtPassword", Name = "Password", Value = "[REDACTED]" }],
            resolveSecret: (target, name) => target == "txtPassword" ? "realpw" : null);

        var a = await d.DecideActionAsync(Snap, Goal, "");
        Assert.Equal("realpw", a.Value);
    }

    [Fact]
    public async Task KeepsTheRedactedPlaceholderWhenNoSecretIsResolved()
    {
        var d = new ReplayActionDecider(
            [new RecordedAction { Verb = "EnterText", Target = "txtPassword", Value = "[REDACTED]" }],
            resolveSecret: (_, _) => null);

        Assert.Equal("[REDACTED]", (await d.DecideActionAsync(Snap, Goal, "")).Value);
    }

    [Fact]
    public async Task EmptyScript_ReturnsDoneImmediately()
    {
        var d = new ReplayActionDecider([]);
        Assert.Equal("Done", (await d.DecideActionAsync(Snap, Goal, "")).ActionType);
    }

    [Fact]
    public async Task ReplaysADriftedTargetVerbatim_SoTheLoopCanDetectAndHealIt()
    {
        // The recorded target may have drifted; replay emits it as-is (the executor then fails it,
        // SelectorHealer records a suggestion, --heal-apply can fix the session). No silent skipping.
        var d = new ReplayActionDecider([new RecordedAction { Verb = "Click", Target = "btnLogn" }]);
        var a = await d.DecideActionAsync(Snap, Goal, "");
        Assert.Equal("btnLogn", a.AutomationId);
    }
}
