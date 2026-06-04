using System.Collections.Generic;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Covers multi-region status resolution (A6 / DISCOVERY_LOG 2026-06-02): a success condition
/// that lands in a non-first status label must still be detected. <see cref="UiSnapshot.FindStatusText"/>
/// keeps returning the first region (back-compat); <see cref="UiSnapshot.StatusContains"/> scans all.
/// </summary>
public sealed class UiSnapshotStatusTests
{
    private static UiElement StatusLabel(string id, string value) =>
        new() { AutomationId = id, ControlType = "Label", Value = value };

    [Fact]
    public void StatusContains_FindsConditionInANonFirstStatusRegion()
    {
        // The login status ("Waiting") would win FindStatusText, but the action result lands
        // in a separate region — the exact shape that broke success_condition before.
        var snapshot = new UiSnapshot("App", new List<UiElement>
        {
            StatusLabel("lblStatus", "Waiting for login"),
            StatusLabel("lblControlsStatus", "Protected action completed")
        });

        Assert.Equal("Waiting for login", snapshot.FindStatusText()); // first region, unchanged
        Assert.True(snapshot.StatusContains("Protected action completed")); // but all are scanned
        Assert.True(snapshot.StatusContains("Waiting for login"));
    }

    [Fact]
    public void StatusContains_IsCaseInsensitiveAndSubstring()
    {
        var snapshot = new UiSnapshot("App", new List<UiElement>
        {
            StatusLabel("lblStatus", "Login successful now")
        });

        Assert.True(snapshot.StatusContains("login SUCCESSFUL"));
    }

    [Fact]
    public void StatusContains_UsesExplicitStatusTextToo()
    {
        var snapshot = new UiSnapshot("App", new List<UiElement>(), statusText: "Done: saved");

        Assert.True(snapshot.StatusContains("saved"));
    }

    [Fact]
    public void StatusContains_FalseWhenAbsentEverywhere()
    {
        var snapshot = new UiSnapshot("App", new List<UiElement>
        {
            StatusLabel("lblStatus", "Waiting"),
            StatusLabel("lblOther", "ignored — not a status id")
        });

        Assert.False(snapshot.StatusContains("never shown"));
    }

    [Fact]
    public void StatusContains_FalseForEmptyQuery()
    {
        var snapshot = new UiSnapshot("App", new List<UiElement> { StatusLabel("lblStatus", "x") });
        Assert.False(snapshot.StatusContains(""));
    }
}
