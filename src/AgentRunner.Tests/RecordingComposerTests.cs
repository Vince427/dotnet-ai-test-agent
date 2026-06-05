using System.Collections.Generic;
using System.Linq;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// V9.5 recording mode (increment 1): a recorded manual session composes into a validated,
/// goal-based YAML test draft. Pure and key-free — no app, no LLM.
/// </summary>
public sealed class RecordingComposerTests
{
    private static RecordedSession LoginSession() => new()
    {
        Window = "Sample Login App (.NET 8)",
        Framework = "winforms",
        Title = "Login happy path",
        Actions =
        [
            new RecordedAction { Verb = "EnterText", Target = "txtUsername", Name = "Username", Value = "admin" },
            new RecordedAction { Verb = "EnterText", Target = "txtPassword", Name = "Password", Value = "password123" },
            new RecordedAction { Verb = "Click", Target = "btnLogin", Name = "Log In" }
        ]
    };

    [Fact]
    public void Compose_ProducesValidYamlDraftFromSession()
    {
        var result = RecordingComposer.Compose(LoginSession());

        Assert.True(result.IsValid, string.Join("; ", result.Errors));

        // It parses + the metadata carried over from the session.
        var plan = TestPlanLoader.Parse(result.Yaml, result.TestId);
        var test = plan.Tests.Single();
        Assert.Equal(result.TestId, test.Id);
        Assert.Equal("winforms", test.Framework);
        Assert.Equal("Sample Login App (.NET 8)", test.TargetWindow);
        Assert.Equal("recorder", test.AuthoringAgent);

        // allowed_actions = the distinct verbs used + Done so the agent can finish.
        Assert.Contains("EnterText", test.AllowedActions);
        Assert.Contains("Click", test.AllowedActions);
        Assert.Contains("Done", test.AllowedActions);

        // The goal is synthesised in plain language from the steps.
        Assert.Contains("Recorded flow", test.Goal);
        Assert.Contains("Username", test.Goal);
        Assert.Contains("Log In", test.Goal);
    }

    [Fact]
    public void Compose_RedactsSecretFieldValuesInTheGoal()
    {
        var test = TestPlanLoader.Parse(RecordingComposer.Compose(LoginSession()).Yaml, "x").Tests.Single();

        Assert.Contains("admin", test.Goal);            // a non-secret value is kept
        Assert.DoesNotContain("password123", test.Goal); // the password field value is redacted
        Assert.Contains("[REDACTED]", test.Goal);
    }

    [Fact]
    public void Compose_WarnsAboutMissingSuccessCondition()
    {
        // A recorded draft has no success_condition yet (the author adds it) — surface the advisory.
        var result = RecordingComposer.Compose(LoginSession());
        Assert.Contains(result.Warnings, w => w.Contains("success_condition"));
    }

    [Fact]
    public void Compose_DerivesASafeIdAndHonorsAHint()
    {
        Assert.Equal("LOGIN-HAPPY-PATH-001", RecordingComposer.Compose(LoginSession()).TestId);
        Assert.Equal("MY-FLOW-001", RecordingComposer.Compose(LoginSession(), idHint: "my flow").TestId);
    }

    [Fact]
    public void Compose_DoesNotLetACraftedNameInjectSiblingYamlKeys()
    {
        // A control name with embedded newlines + forged keys must not become real YAML fields:
        // the quoting strips control chars, so it stays inside the goal scalar.
        var session = new RecordedSession
        {
            Window = "App",
            Framework = "wpf",
            Actions = [new RecordedAction { Verb = "Click", Name = "ok\n    target_window: HIJACKED\n    success_condition: pwned" }]
        };

        var result = RecordingComposer.Compose(session);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));

        var test = TestPlanLoader.Parse(result.Yaml, result.TestId).Tests.Single();
        Assert.Equal("App", test.TargetWindow);     // not hijacked — stays the real window
        Assert.Null(test.SuccessCondition);         // the forged success_condition didn't become a key
        // The forged text is flattened into the goal scalar, never a sibling YAML key.
        Assert.Contains("HIJACKED", test.Goal);
    }

    [Fact]
    public void Compose_HandlesAnEmptySessionWithoutCrashing()
    {
        var result = RecordingComposer.Compose(new RecordedSession { Window = "App", Framework = "wpf" });

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var test = TestPlanLoader.Parse(result.Yaml, result.TestId).Tests.Single();
        Assert.Contains("Done", test.AllowedActions);     // still finishable
        Assert.Contains("no actions captured", test.Goal); // honest placeholder goal
    }
}
