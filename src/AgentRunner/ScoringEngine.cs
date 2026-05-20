using System;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Scores agent actions to track progress and detect failure.
/// Tracks AgentLoop run progress through deterministic action scoring.
/// </summary>
public class ScoringEngine
{
    public int TotalScore { get; private set; }
    public int StepCount { get; private set; }
    public int ErrorCount { get; private set; }
    public int SuccessCount { get; private set; }

    /// <summary>Minimum score before the agent is considered failing.</summary>
    public int AbortThreshold { get; set; } = -20;

    /// <summary>
    /// Scores an action and returns the delta score.
    /// </summary>
    public int ScoreAction(string actionType, bool succeeded, bool isLoop, string? outcome = null)
    {
        StepCount++;
        int delta = 0;

        // Base score by action type
        if (string.Equals(actionType, "Click", StringComparison.OrdinalIgnoreCase) && succeeded)
            delta += 2;
        else if (string.Equals(actionType, "EnterText", StringComparison.OrdinalIgnoreCase) && succeeded)
            delta += 2;
        else if (string.Equals(actionType, "Wait", StringComparison.OrdinalIgnoreCase))
            delta += 0; // neutral
        else if (string.Equals(actionType, "Assert", StringComparison.OrdinalIgnoreCase) && succeeded)
            delta += 3; // strong reward for correct assertion
        else if (string.Equals(actionType, "Done", StringComparison.OrdinalIgnoreCase) && succeeded)
            delta += 5; // goal achieved
        else if (string.Equals(actionType, "Explore", StringComparison.OrdinalIgnoreCase) && succeeded)
            delta += 1;

        // Penalties
        if (!succeeded)
        {
            delta -= 5;
            ErrorCount++;
        }

        if (isLoop)
            delta -= 10;

        var outcomeText = outcome ?? "";

        if (outcomeText.IndexOf("guard_force_reject", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            delta -= 20;
        }

        if (outcomeText.IndexOf("guard_abort", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            delta -= 50;
        }

        // Outcome bonus
        if (outcomeText.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            delta += 10;
            SuccessCount++;
        }

        TotalScore += delta;
        return delta;
    }

    /// <summary>Returns true if the agent should abort due to too many failures.</summary>
    public bool ShouldAbort()
    {
        return TotalScore <= AbortThreshold;
    }

    public string GetSummary()
    {
        return $"Score={TotalScore} Steps={StepCount} Errors={ErrorCount} Successes={SuccessCount}";
    }

    public void Reset()
    {
        TotalScore = 0;
        StepCount = 0;
        ErrorCount = 0;
        SuccessCount = 0;
    }
}
