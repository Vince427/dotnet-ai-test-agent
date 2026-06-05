using System.Collections.Generic;
using System.Linq;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// V8 inc.2 `--heal-apply`: apply a run's selector-drift suggestions to a test's `selectors`, as a
/// surgical YAML edit verified by the fact-gate. Pure tests (no disk, no CLI).
/// </summary>
public sealed class HealApplierTests
{
    private static RunArtifact RunWithHeal(string oldT, string newT, int confidence = 90) => new()
    {
        RunId = "r1", TestId = "LOGIN-001", Result = "Failed",
        Steps =
        [
            new RunStep
            {
                StepNumber = 1, ActionType = "Click", ActionTarget = oldT, Outcome = "Failed",
                FailureCode = "action_target_not_found",
                HealingSuggestion = new HealingSuggestion { OldTarget = oldT, NewTarget = newT, Confidence = confidence },
            },
        ],
    };

    private static TestDefinition TestWithSelectors(params string[] selectors) => new()
    {
        Id = "LOGIN-001", Framework = "winforms", Goal = "g", SuccessCondition = "ok", MaxSteps = 8,
        AllowedActions = ["Click", "Done"], Selectors = selectors.ToList(),
    };

    [Fact]
    public void Plan_ProposesAReplacementWhenTheDriftedSelectorIsDeclared()
    {
        var plan = HealApplier.Plan(RunWithHeal("btnLogn", "btnLogin"), TestWithSelectors("txtUser", "btnLogn"));
        Assert.True(plan.HasChanges);
        var r = Assert.Single(plan.Replacements);
        Assert.Equal("btnLogn", r.Old);
        Assert.Equal("btnLogin", r.New);
    }

    [Fact]
    public void Plan_IgnoresDriftForSelectorsTheTestDoesNotDeclare()
    {
        // The suggestion is for btnLogn, but the test doesn't list it -> nothing to heal (no spurious add).
        var plan = HealApplier.Plan(RunWithHeal("btnLogn", "btnLogin"), TestWithSelectors("txtUser", "txtPass"));
        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void RewriteSelectorsInYaml_InlineForm_ReplacesWholeTokenOnly()
    {
        var yaml = """
suite: x
tests:
  LOGIN-001:
    goal: "g"
    success_condition: "ok"
    selectors: ["txtUser", "btnLogn", "btnLognExtra"]
    tags: ["btnLogn"]
""";
        var outp = HealApplier.RewriteSelectorsInYaml(yaml, [new SelectorReplacement { Old = "btnLogn", New = "btnLogin" }]);
        Assert.Contains("\"btnLogin\"", outp);          // replaced
        Assert.Contains("\"btnLognExtra\"", outp);      // NOT a substring match
        Assert.Contains("tags: [\"btnLogn\"]", outp);   // only the selectors line is touched, not tags
    }

    [Fact]
    public void RewriteAndReparse_PassesTheFactGate_OnlySelectorsChanged()
    {
        var before = TestPlanLoader.Parse("""
suite: x
tests:
  LOGIN-001:
    goal: "g"
    framework: "winforms"
    success_condition: "ok"
    max_steps: 8
    allowed_actions: ["Click", "Done"]
    selectors: ["txtUser", "btnLogn"]
""").Tests.Single();

        var yaml = """
suite: x
tests:
  LOGIN-001:
    goal: "g"
    framework: "winforms"
    success_condition: "ok"
    max_steps: 8
    allowed_actions: ["Click", "Done"]
    selectors: ["txtUser", "btnLogn"]
""";
        var rewritten = HealApplier.RewriteSelectorsInYaml(yaml, [new SelectorReplacement { Old = "btnLogn", New = "btnLogin" }]);
        var after = TestPlanLoader.Parse(rewritten).Tests.Single();

        // The fact-gate confirms the surgical edit changed ONLY selectors.
        Assert.True(TestFactGuard.Verify(before, after, allowedToChange: ["selectors"]).Ok);
        Assert.Contains("btnLogin", after.Selectors);
        Assert.DoesNotContain("btnLogn", after.Selectors.Where(s => s != "btnLogin"));
    }
}
