namespace DesktopAiTestAgent.Core;

/// <summary>
/// Defines a test objective for the agent, inspired by Symphony's per-issue prompt model.
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

    public override string ToString() => $"[{Identifier ?? "goal"}] {Description}";
}
