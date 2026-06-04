using System.Collections.Generic;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// V8 self-healing (evidence-only): deterministic closest-selector suggestion when a target
/// drifted. Pure, no LLM — proposes a replacement but never applies it.
/// </summary>
public sealed class SelectorHealerTests
{
    private static UiSnapshot Snapshot(params UiElement[] els) => new("App", new List<UiElement>(els));

    [Fact]
    public void Suggest_FindsCloseAutomationIdMatch()
    {
        var snap = Snapshot(
            new UiElement { AutomationId = "btnLogin", Name = "Log In", ControlType = "Button" },
            new UiElement { AutomationId = "txtUser", ControlType = "Edit" });

        var s = SelectorHealer.Suggest("btnLogn", snap); // typo'd / drifted id

        Assert.NotNull(s);
        Assert.Equal("btnLogin", s!.NewTarget);
        Assert.Equal("btnLogn", s.OldTarget);
        Assert.True(s.Confidence >= 80);
        Assert.Contains("btnLogin", s.Rationale);
    }

    [Fact]
    public void Suggest_IgnoresCaseAndPunctuationWhenMatching()
    {
        var snap = Snapshot(new UiElement { AutomationId = "btnLogin", ControlType = "Button" });

        var s = SelectorHealer.Suggest("BTN_LOGIN", snap);

        Assert.NotNull(s);
        Assert.Equal("btnLogin", s!.NewTarget);
        Assert.Equal(100, s.Confidence); // identical after normalization
    }

    [Fact]
    public void Suggest_MatchesAgainstNameWhenIdDiffers()
    {
        var snap = Snapshot(new UiElement { AutomationId = "x1", Name = "Submit Order", ControlType = "Button" });

        var s = SelectorHealer.Suggest("Submit Ordr", snap);

        Assert.NotNull(s);
        Assert.Equal("x1", s!.NewTarget); // still proposes the id as the new selector
        Assert.Contains("name", s.Rationale);
    }

    [Fact]
    public void Suggest_ReturnsNullWhenNothingIsCloseEnough()
    {
        var snap = Snapshot(new UiElement { AutomationId = "completelyDifferent", ControlType = "Edit" });

        Assert.Null(SelectorHealer.Suggest("btnLogin", snap));
    }

    [Fact]
    public void Suggest_ReturnsNullForEmptyTargetOrSnapshot()
    {
        Assert.Null(SelectorHealer.Suggest(null, Snapshot(new UiElement { AutomationId = "btnLogin" })));
        Assert.Null(SelectorHealer.Suggest("", Snapshot(new UiElement { AutomationId = "btnLogin" })));
        Assert.Null(SelectorHealer.Suggest("btnLogin", Snapshot()));
    }

    [Fact]
    public void Suggest_PicksTheClosestAmongSeveralCandidates()
    {
        var snap = Snapshot(
            new UiElement { AutomationId = "btnCancel", ControlType = "Button" },
            new UiElement { AutomationId = "btnLoginNow", ControlType = "Button" },
            new UiElement { AutomationId = "btnLogon", ControlType = "Button" });

        var s = SelectorHealer.Suggest("btnLogin", snap);

        Assert.NotNull(s);
        Assert.Equal("btnLogon", s!.NewTarget); // closest by edit distance (1 char)
    }
}
