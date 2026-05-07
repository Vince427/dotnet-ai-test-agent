using System.Collections.Generic;

namespace DesktopAiTestAgent.AgentRunner;

public sealed class PlanValidationOutput
{
    public string Kind { get; set; } = "planValidation";
    public bool Valid { get; set; }
    public int PlanCount { get; set; }
    public int TestCount { get; set; }
    public int ErrorCount { get; set; }
    public List<PlanValidationPlanOutput> Plans { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class PlanValidationPlanOutput
{
    public string Path { get; set; } = "";
    public string? Suite { get; set; }
    public int TestCount { get; set; }
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class TestListOutput
{
    public string Kind { get; set; } = "testList";
    public bool Valid { get; set; }
    public int Count { get; set; }
    public List<ListedTestOutput> Tests { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class ListedTestOutput
{
    public string PlanPath { get; set; } = "";
    public string? Suite { get; set; }
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
    public int MaxSteps { get; set; }
    public List<string> AllowedActions { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<string> ExistingTests { get; set; } = [];
}
