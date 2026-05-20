namespace DesktopAiTestAgent.Core;

using System.Collections.Generic;

public enum TestCategory
{
    Smoke,
    Monkey,
    Audit,
    Scenario
}

/// <summary>
/// Defines a test objective for one AgentLoop run.
/// </summary>
public sealed class AgentGoal
{
    /// <summary>Human-readable description of what the agent should accomplish.</summary>
    public string Description { get; set; } = "Explore the application and test its functionality.";

    /// <summary>
    /// Optional condition text to check against status labels or UI state.
    /// When the agent reads this text in the UI, the goal is considered achieved.
    /// </summary>
    public string? SuccessCondition { get; set; }

    /// <summary>Maximum number of steps before the agent gives up.</summary>
    public int MaxSteps { get; set; } = 30;

    /// <summary>Maximum retries on failure before abandoning the goal.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Delay between retries in milliseconds (base for exponential backoff).</summary>
    public int RetryBaseDelayMs { get; set; } = 1000;

    /// <summary>Short identifier for logging and artifact naming.</summary>
    public string? Identifier { get; set; }

    /// <summary>The category of the test, dictating the agent's priority behavior.</summary>
    public TestCategory Category { get; set; } = TestCategory.Smoke;

    /// <summary>Optional action allow-list for bounded test definitions.</summary>
    public List<string> AllowedActions { get; set; } = [];

    public override string ToString() => $"[{Identifier ?? "goal"}] {Description}";
}
