using System.Linq;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// The fact-gate for test rewrites: a rewrite must preserve every declared fact except those the
/// caller explicitly allows to change. Pure, key-free (the desktop analogue of drift-guard).
/// </summary>
public sealed class TestFactGuardTests
{
    private static TestDefinition Login() => new()
    {
        Id = "LOGIN-001",
        Title = "Login happy path",
        Framework = "winforms",
        TargetWindow = "Sample Login App (.NET 8)",
        Category = TestCategory.Scenario,
        Goal = "Log in with admin / password123 and confirm success.",
        SuccessCondition = "Login successful",
        MaxSteps = 8,
        AllowedActions = ["EnterText", "Click", "Done"],
        Tags = ["smoke", "login"],
    };

    private static TestDefinition Clone(TestDefinition t) => new()
    {
        Id = t.Id, Title = t.Title, Priority = t.Priority, Framework = t.Framework,
        TargetWindow = t.TargetWindow, Risk = t.Risk, CiProfile = t.CiProfile,
        AuthoringAgent = t.AuthoringAgent, SourceIssue = t.SourceIssue, SourcePr = t.SourcePr,
        Category = t.Category, Goal = t.Goal, SuccessCondition = t.SuccessCondition,
        MaxSteps = t.MaxSteps,
        AllowedActions = t.AllowedActions.ToList(), Tags = t.Tags.ToList(),
        BlockedIf = t.BlockedIf.ToList(), ExistingTests = t.ExistingTests.ToList(),
    };

    [Fact]
    public void Verify_IdenticalRewrite_IsOk()
    {
        Assert.True(TestFactGuard.Verify(Login(), Clone(Login())).Ok);
    }

    [Fact]
    public void Verify_DroppedFact_IsAViolation()
    {
        var after = Clone(Login());
        after.SuccessCondition = null; // silently dropped
        var r = TestFactGuard.Verify(Login(), after);
        Assert.False(r.Ok);
        Assert.Contains(r.Violations, v => v.Field == "success_condition" && v.Kind == FactChangeKind.Dropped);
    }

    [Fact]
    public void Verify_ChangedGoal_IsAViolation()
    {
        var after = Clone(Login());
        after.Goal = "Do something completely different.";
        var r = TestFactGuard.Verify(Login(), after);
        Assert.False(r.Ok);
        Assert.Contains(r.Violations, v => v.Field == "goal" && v.Kind == FactChangeKind.Changed);
    }

    [Fact]
    public void Verify_AllowedFieldChange_IsNotAViolation()
    {
        // A rewrite that ONLY changes an allowed field (e.g. a heal-apply targeting a control name in
        // target_window) passes; an unrelated drop in the same rewrite still fails.
        var after = Clone(Login());
        after.TargetWindow = "Sample Login App (renamed)";   // intended
        Assert.True(TestFactGuard.Verify(Login(), after, allowedToChange: ["target_window"]).Ok);

        after.MaxSteps = 99;                                  // NOT allowed
        var r = TestFactGuard.Verify(Login(), after, allowedToChange: ["target_window"]);
        Assert.False(r.Ok);
        Assert.Contains(r.Violations, v => v.Field == "max_steps");
        Assert.DoesNotContain(r.Violations, v => v.Field == "target_window");
    }

    [Fact]
    public void Verify_ListReorderIsNotAChange_ButDropIs()
    {
        var reordered = Clone(Login());
        reordered.AllowedActions = ["Done", "Click", "EnterText"]; // same set, different order
        Assert.True(TestFactGuard.Verify(Login(), reordered).Ok);

        var dropped = Clone(Login());
        dropped.AllowedActions = ["EnterText", "Click"]; // dropped "Done"
        var r = TestFactGuard.Verify(Login(), dropped);
        Assert.False(r.Ok);
        Assert.Contains(r.Violations, v => v.Field == "allowed_actions");
    }

    [Fact]
    public void Diff_ReportsAddedFact()
    {
        var before = Clone(Login());
        before.SuccessCondition = null;
        var changes = TestFactGuard.Diff(before, Login());
        Assert.Contains(changes, c => c.Field == "success_condition" && c.Kind == FactChangeKind.Added);
    }
}
