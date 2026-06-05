using System.Linq;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// V9.5 recording mode (increment 2): the pure capture core — mapping normalized UIA events to
/// RecordedActions and smoothing them into a session. No desktop required.
/// </summary>
public sealed class SessionRecorderTests
{
    private static CapturedUiEvent Ev(UiEventKind kind, string? id, string? name = null, string? value = null) =>
        new() { Kind = kind, AutomationId = id, Name = name, Value = value };

    [Fact]
    public void Map_InvokedBecomesClick_ValueChangedBecomesEnterText()
    {
        var click = RecordedActionMapper.Map(Ev(UiEventKind.Invoked, "btnLogin", "Log In"));
        Assert.NotNull(click);
        Assert.Equal("Click", click!.Verb);
        Assert.Equal("btnLogin", click.Target);

        var type = RecordedActionMapper.Map(Ev(UiEventKind.ValueChanged, "txtUser", "Username", "admin"));
        Assert.Equal("EnterText", type!.Verb);
        Assert.Equal("admin", type.Value);
    }

    [Fact]
    public void Map_ToggleAndSelectionReplayAsClick()
    {
        Assert.Equal("Click", RecordedActionMapper.Map(Ev(UiEventKind.Toggled, "chkRemember"))!.Verb);
        Assert.Equal("Click", RecordedActionMapper.Map(Ev(UiEventKind.SelectionChanged, "cboCountry"))!.Verb);
    }

    [Fact]
    public void Map_ReturnsNullWhenNoTargetIdentifier()
    {
        Assert.Null(RecordedActionMapper.Map(Ev(UiEventKind.Invoked, null, null)));
        Assert.Null(RecordedActionMapper.Map(null!));
    }

    [Fact]
    public void Observe_CollapsesConsecutiveTextEditsOnSameFieldToFinalValue()
    {
        var rec = new SessionRecorder();
        // Per-keystroke ValueChanged events on the same field.
        rec.Observe(Ev(UiEventKind.ValueChanged, "txtUser", "Username", "a"));
        rec.Observe(Ev(UiEventKind.ValueChanged, "txtUser", "Username", "ad"));
        rec.Observe(Ev(UiEventKind.ValueChanged, "txtUser", "Username", "admin"));

        var actions = rec.ToSession().Actions;
        Assert.Single(actions);
        Assert.Equal("admin", actions[0].Value); // only the final value survives
    }

    [Fact]
    public void Observe_DeduplicatesARepeatedClickOnTheSameControl()
    {
        var rec = new SessionRecorder();
        rec.Observe(Ev(UiEventKind.Invoked, "btnLogin", "Log In"));
        rec.Observe(Ev(UiEventKind.Invoked, "btnLogin", "Log In")); // doubled event

        Assert.Equal(1, rec.Count);
    }

    [Fact]
    public void Observe_PreservesANonAdjacentRepeatedClick()
    {
        var rec = new SessionRecorder();
        rec.Observe(Ev(UiEventKind.Invoked, "btnAdd", "Add"));
        rec.Observe(Ev(UiEventKind.ValueChanged, "txtQty", "Qty", "2")); // something in between
        rec.Observe(Ev(UiEventKind.Invoked, "btnAdd", "Add"));           // intentional repeat — survives

        var actions = rec.ToSession().Actions;
        Assert.Equal(3, actions.Count);
        Assert.Equal("Click", actions[0].Verb);
        Assert.Equal("Click", actions[2].Verb);
    }

    [Fact]
    public void Observe_KeepsDistinctStepsAndComposesToValidYaml()
    {
        var rec = new SessionRecorder { Window = "Sample Login App (.NET 8)", Framework = "winforms", Title = "Login" };
        rec.Observe(Ev(UiEventKind.ValueChanged, "txtUser", "Username", "admin"));
        rec.Observe(Ev(UiEventKind.ValueChanged, "txtPassword", "Password", "password123"));
        rec.Observe(Ev(UiEventKind.Invoked, "btnLogin", "Log In"));

        var session = rec.ToSession();
        Assert.Equal(3, session.Actions.Count);

        // The recorded session feeds increment 1: it composes to a valid YAML draft.
        var result = RecordingComposer.Compose(session);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var test = TestPlanLoader.Parse(result.Yaml, result.TestId).Tests.Single();
        Assert.Equal("Sample Login App (.NET 8)", test.TargetWindow);
        Assert.Contains("Click", test.AllowedActions);
        Assert.Contains("EnterText", test.AllowedActions);
        Assert.DoesNotContain("password123", test.Goal); // secret redacted in the synthesised goal
    }
}
