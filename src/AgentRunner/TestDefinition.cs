using System.Collections.Generic;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

public sealed class TestPlan
{
    public string? Suite { get; set; }
    public List<TestDefinition> Tests { get; } = [];

    public TestDefinition? FindById(string testId)
    {
        foreach (var test in Tests)
        {
            if (string.Equals(test.Id, testId, System.StringComparison.OrdinalIgnoreCase))
                return test;
        }

        return null;
    }
}

public sealed class TestDefinition
{
    public string Id { get; set; } = "";
    public string? Title { get; set; }
    public string? Priority { get; set; }
    public string? Framework { get; set; }
    public string? TargetWindow { get; set; }
    public string? SourceIssue { get; set; }
    public string? SourcePr { get; set; }
    public string? AuthoringAgent { get; set; }
    public string? Risk { get; set; }
    public string? CiProfile { get; set; }
    public string Goal { get; set; } = "";
    public string? SuccessCondition { get; set; }
    public int MaxSteps { get; set; } = 30;
    public TestCategory Category { get; set; } = TestCategory.Scenario;
    public List<string> AllowedActions { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    /// <summary>Concrete control selectors (AutomationIds) this test targets — optional inventory,
    /// populated by recording and maintained by <c>--heal-apply</c> on selector drift.</summary>
    public List<string> Selectors { get; set; } = [];
    public List<string> BlockedIf { get; set; } = [];
    public List<string> ExistingTests { get; set; } = [];

    public AgentGoal ToAgentGoal()
    {
        return new AgentGoal
        {
            Description = Goal,
            SuccessCondition = SuccessCondition,
            MaxSteps = MaxSteps,
            Identifier = Id,
            Category = Category,
            AllowedActions = new List<string>(AllowedActions)
        };
    }
}
