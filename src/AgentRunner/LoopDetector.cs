using System.Collections.Generic;
using System.Linq;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Detects when the agent is stuck in a loop by tracking recent actions.
/// Detects repeated AgentLoop actions that indicate stalled progress.
/// </summary>
public class LoopDetector(int windowSize = 6, int repeatThreshold = 3)
{
    private readonly Queue<string> _recentActions = [];
    private readonly int _windowSize = windowSize;
    private readonly int _repeatThreshold = repeatThreshold;

    /// <summary>
    /// Records an action and returns true if a loop is detected.
    /// </summary>
    public bool RecordAndCheck(string actionDescription)
    {
        _recentActions.Enqueue(actionDescription);

        while (_recentActions.Count > _windowSize)
            _recentActions.Dequeue();

        // Check if any single action dominates the window
        var groups = _recentActions.GroupBy(a => a).ToList();
        foreach (var g in groups)
        {
            if (g.Count() >= _repeatThreshold)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns a summary of the recent action pattern for the LLM to reason about.
    /// </summary>
    public string GetPatternSummary()
    {
        if (!_recentActions.Any())
            return "No recent actions.";

        var groups = _recentActions.GroupBy(a => a)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} (x{g.Count()})");

        return "Recent pattern: " + string.Join(", ", groups);
    }

    public void Reset()
    {
        _recentActions.Clear();
    }
}
